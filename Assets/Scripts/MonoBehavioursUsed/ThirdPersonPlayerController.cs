using UnityEngine;

/// <summary>
/// Contrôleur avancé à la troisième personne pour Lucian en mode exploration.
/// Cette refonte s'inspire directement de "Clair Obscur Expedition 33" et met
/// l'accent sur un ressenti fluide :
/// - Détection robuste du sol via sphere casts.
/// - Gestion des pentes avec glissade contrôlée.
/// - Inertie et amortissement des vitesses pour des transitions naturelles.
/// - Aides au saut (coyote time / jump buffer) pour limiter la frustration.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Transform de la WorldCamera utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    private CharacterController controller;                  // Référence au CharacterController Unity.
    private Animator animator;                               // Référence à l'Animator pour déclencher les animations.

    private Vector3 horizontalVelocity;                      // Vitesse appliquée sur le plan XZ.
    private float verticalVelocity;                          // Vitesse verticale isolée pour un contrôle précis.
    private Vector3 lastMoveDirection = Vector3.forward;     // Mémoire de la dernière direction significative (utile en l'air).

    // Verrouillage utilisé lorsque l'animation d'atterrissage est en cours pour éviter toute interruption.
    private bool landingAnimationLocked;                  // Verrou utilisé pour empêcher toute transition prématurée hors des animations d'atterrissage.
    private Vector3 landingVelocitySnapshot;              // Vitesse horizontale capturée au moment de l'impact au sol.
    private float landingSpeedSnapshot;                   // Vitesse scalaire correspondante pour synchroniser currentSpeed.
    private float landingAnimationEndTime;                // Moment auquel l'animation d'atterrissage est terminée (calculé via la durée du clip).
    private int activeLandingStateHash;                   // Hash de l'état d'animation de landing en cours (immobile ou en mouvement).

    // Hashes réutilisés pour identifier rapidement les états d'animations dans l'Animator.
    private static readonly int landingStateHash = Animator.StringToHash("Landing");
    private static readonly int landingOnMoveStateHash = Animator.StringToHash("Landing_OnMove");

    // Durées des animations d'atterrissage récupérées dynamiquement pour attendre leur fin exacte.
    private float landingClipLength = 0.5f;               // Valeur par défaut de secours si le clip n'est pas trouvé.
    private float landingOnMoveClipLength = 0.5f;         // Idem pour la version en mouvement.

    [Header("Lien avec Munin")]
    [Tooltip("Détermine si Lucian est actuellement lié à Munin.")]
    public bool linkToMunin;                          // État du lien avec Munin, modifié depuis l'extérieur.
    private bool previousLinkToMunin;                 // Permet de détecter un changement d'état d'une frame à l'autre.
    private Camera worldCamera;                       // Référence directe à la WorldCamera pour modifier son culling mask.
    private int worldBaseMask;                        // Sauvegarde du culling mask d'origine de la WorldCamera.
    private int revealInteractableLayer;              // ID de la couche "World_ReveLink_Interactable".
    private int revealObjectLayer;                    // ID de la couche "World_ReveLink_Object".
    private int revealUILayer;                        // ID de la couche "World_ReveLink_UI".
    private int revealMask;                           // Masque combinant les couches révélées par le lien.

    /// <summary>
    /// Énumération interne pour piloter l'animation adéquate sans paramètres Animator.
    /// </summary>
    private enum MovementAnimation
    {
        Idle,
        Walk,
        Run,
        IsLanding
    }

    [SerializeField] private MovementAnimation currentAnimState = MovementAnimation.Idle;

    // États exposés si un AnimationHandler externe souhaite les lire.
    public bool isWalking;
    public bool isRunning;
    public bool isJumping;
    public bool isFalling;
    public bool isSliding;

    /// <summary>
    /// Autorise ou non la course. Peut être modifié par d'autres composants (zones, scripts...).
    /// </summary>
    [Tooltip("Si faux, l'input de sprint est ignoré et Lucian marche uniquement.")]
    public bool canRun = true;

    /// <summary>Indique si le personnage touche le sol de manière fiable.</summary>
    public bool isGrounded => groundedStable;

    [Header("Vitesses de base")]
    [Tooltip("Vitesse de marche en unités/seconde.")]
    public float walkSpeed = 5f;
    [Tooltip("Vitesse de course en unités/seconde.")]
    public float runSpeed = 8f;

    [Header("Saut & Gravité")]
    [Tooltip("Hauteur du saut en unités.")]
    public float jumpHeight = 2f;
    [Tooltip("Gravité appliquée au personnage.")]
    public float gravity = -9.81f;
    [Tooltip("Multiplicateur appliqué à la vitesse initiale du saut pour simuler une impulsion.")]
    public float jumpBoost = 1.1f;
    [Tooltip("Multiplicateur de gravité durant la montée pour une courbe de saut moins linéaire.")]
    public float ascentGravityMultiplier = 1f;
    [Tooltip("Multiplicateur de gravité durant la descente pour accentuer l'inertie.")]
    public float fallGravityMultiplier = 2f;
    [Tooltip("Limite de vitesse de chute pour éviter des valeurs extrêmes.")]
    public float terminalVelocity = -35f;
    [Tooltip("Force dirigée vers le bas appliquée à l'atterrissage pour renforcer l'impact.")]
    public float landingForce = 5f;

    [Header("Inertie du déplacement")]
    [Tooltip("Taux d'accélération en unités/seconde².")]
    public float acceleration = 20f;
    [Tooltip("Taux de décélération en unités/seconde².")]
    public float deceleration = 25f;
    [Tooltip("Vitesse de rotation du personnage pour suivre la direction de déplacement.")]
    public float orientationLerpSpeed = 12f;
    [Range(0f, 1f), Tooltip("Pourcentage d'influence conservé en l'air.")]
    public float airControlPercent = 0.35f;

    private float currentSpeed; // Vitesse courante (accélération/décélération progressive).

    [Header("Gestion du sprint")]
    [Tooltip("Durée durant laquelle la course reste active après avoir relâché l'input (secondes).")]
    public float runReleaseDelay = 1f;

    private float runReleaseTimer;

    [Header("Détection du sol & des pentes")]
    [Tooltip("Décalage vertical pour la détection de sol (évite que le SphereCast parte trop bas).")]
    public float groundCheckOffset = 0.3f;
    [Tooltip("Distance maximale de détection du sol sous le personnage.")]
    public float groundCheckDistance = 0.6f;
    [Tooltip("Facteur appliqué au rayon du CharacterController pour le SphereCast.")]
    [Range(0.5f, 1f)] public float groundProbeRadiusFactor = 0.9f;
    [Tooltip("Force qui plaque le personnage au sol quand il y retouche.")]
    public float groundStickForce = 6f;
    [Tooltip("Couches reconnues comme du sol.")]
    public LayerMask groundLayer = ~0; // Par défaut : toutes les couches.
    [Tooltip("Vitesse maximale atteinte lors d'un glissement sur une pente trop raide.")]
    public float slopeSlideSpeed = 6f;
    [Tooltip("Vitesse à laquelle la glissade rejoint la vitesse cible.")]
    public float slopeSlideAcceleration = 30f;

    [Header("Sécurité contre le vide")]
    [Tooltip("Distance avant le personnage utilisée pour vérifier la présence du sol.")]
    public float voidCheckDistance = 1f;
    [Tooltip("Profondeur minimale pour considérer qu'il y a du sol lors du raycast avant.")]
    public float voidCheckDepth = 2f;

    [Header("Aides au saut")]
    [Tooltip("Temps pendant lequel un saut reste possible après avoir quitté le sol (coyote time).")]
    public float coyoteTime = 0.15f;
    [Tooltip("Temps pendant lequel un appui sur Saut est mémorisé avant l'atterrissage (jump buffer).")]
    public float jumpBufferTime = 0.2f;

    private bool groundedStable;                            // Résultat consolidé de la détection de sol.
    private bool wasGroundedLastFrame;                      // Permet d'identifier les transitions sol/air.
    private Vector3 groundNormal = Vector3.up;              // Normale actuelle du sol sous le joueur.
    private Vector3 slopeSlideDirection = Vector3.zero;     // Direction naturelle de glissade sur pentes trop raides.
    private float lastGroundedTimestamp = float.NegativeInfinity;   // Dernière fois où le joueur était considéré comme au sol.
    private float lastJumpPressedTimestamp = float.NegativeInfinity;// Dernière fois où le bouton de saut a été pressé.

    // Cache des inputs lus à chaque frame pour éviter de multiples accès au système d'inputs.
    private Vector2 moveInput;
    private bool runPressed;

    private const float locomotionCrossFadeDuration = 0.1f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = transform.GetChild(0).GetComponent<Animator>();

        if (animator != null)
        {
            // Déplacement géré par le code : animations in-place sans root motion.
            animator.applyRootMotion = false;
            animator.Play("Idle_World");

            // Pré-calcul des durées d'animations d'atterrissage pour verrouiller précisément les transitions.
            CacheLandingClipDurations();
        }

        // Si aucune caméra n'est assignée, on cherche d'abord la WorldCamera, sinon la MainCamera.
        if (cameraTransform == null)
        {
            Camera worldCam = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
            if (worldCam != null) cameraTransform = worldCam.transform;
            else if (Camera.main != null) cameraTransform = Camera.main.transform;
            else Debug.LogWarning("[ThirdPersonPlayerController] Aucune WorldCamera ou MainCamera trouvée.");
        }

        // Une fois la caméra récupérée, on stocke sa référence et son culling mask d'origine.
        if (cameraTransform != null)
        {
            worldCamera = cameraTransform.GetComponent<Camera>();
            if (worldCamera != null) worldBaseMask = worldCamera.cullingMask;
        }

        // Initialisation des couches utilisées lors du lien avec Munin.
        revealInteractableLayer = LayerMask.NameToLayer("World_ReveLink_Interactable");
        revealObjectLayer = LayerMask.NameToLayer("World_ReveLink_Object");
        revealUILayer = LayerMask.NameToLayer("World_ReveLink_UI");
        if (revealInteractableLayer != -1) revealMask |= 1 << revealInteractableLayer; // Ajout de la couche interactable
        if (revealObjectLayer != -1) revealMask |= 1 << revealObjectLayer;             // Ajout de la couche objet
        if (revealUILayer != -1) revealMask |= 1 << revealUILayer;                     // Ajout de la couche d'UI

        previousLinkToMunin = !linkToMunin; // Force un rafraîchissement au démarrage
        ApplyMuninLinkState();              // Applique immédiatement l'état visuel adéquat

        wasGroundedLastFrame = controller.isGrounded;
        groundedStable = wasGroundedLastFrame;
    }

    void Update()
    {
        // Vérifie si l'état de lien avec Munin a changé afin de mettre à jour l'affichage.
        if (linkToMunin != previousLinkToMunin)
            ApplyMuninLinkState();

        CacheInputs();           // Centralise la lecture des entrées.
        EvaluateGroundState();   // Analyse précise du sol et des pentes.
        HandleHorizontalMove();  // Gestion de l'inertie sur le plan XZ.
        HandleVerticalMove();    // Application des aides au saut et de la gravité.
        UpdateMovementAnimation();
        UpdateJumpAnimation();
        UpdateLandingLock();
    }

    /// <summary>
    /// Mémorise les entrées de la frame pour alimenter les différentes étapes du cycle de mise à jour.
    /// </summary>
    private void CacheInputs()
    {
        moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        runPressed = canRun && InputsManager.Instance.playerInputs.World.Run.IsPressed();

        // Buffer de relâchement du sprint (évite ping-pong marche/course).
        if (runPressed) runReleaseTimer = runReleaseDelay;
        else runReleaseTimer = Mathf.Max(runReleaseTimer - Time.deltaTime, 0f);

        // Si la course est désactivée par une zone, on vide immédiatement le buffer.
        if (!canRun)
        {
            runReleaseTimer = 0f;
            runPressed = false;
        }

        if (InputsManager.Instance.playerInputs.World.Jump.triggered)
        {
            lastJumpPressedTimestamp = Time.time;
        }
    }

    /// <summary>
    /// Analyse le sol sous le personnage pour déterminer un état "grounded" stable et détecter les pentes.
    /// </summary>
    private void EvaluateGroundState()
    {
        bool controllerGrounded = controller.isGrounded;
        Vector3 sphereOrigin = controller.bounds.center + Vector3.up * groundCheckOffset;
        float sphereRadius = controller.radius * groundProbeRadiusFactor;
        float castDistance = groundCheckDistance + controller.skinWidth;

        int mask = groundLayer.value == 0 ? Physics.DefaultRaycastLayers : groundLayer;

        bool hitGround = Physics.SphereCast(
            sphereOrigin,
            sphereRadius,
            Vector3.down,
            out RaycastHit hitInfo,
            castDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        groundNormal = Vector3.up;
        slopeSlideDirection = Vector3.zero;
        isSliding = false;

        bool validGround = controllerGrounded;

        if (hitGround)
        {
            groundNormal = hitInfo.normal;
            float slopeAngle = Vector3.Angle(hitInfo.normal, Vector3.up);
            bool tooSteep = slopeAngle > controller.slopeLimit;

            // On considère le personnage comme au sol si le SphereCast touche à une distance raisonnable.
            if (hitInfo.distance <= castDistance + 0.05f && !tooSteep)
            {
                validGround = true;
            }

            // Prépare la glissade sur les pentes trop raides.
            if (controllerGrounded && tooSteep)
            {
                isSliding = true;
                slopeSlideDirection = Vector3.ProjectOnPlane(Vector3.down, hitInfo.normal).normalized;
            }
        }

        groundedStable = validGround;

        // Détection des transitions sol ↔ air.
        if (groundedStable)
        {
            lastGroundedTimestamp = Time.time;
            isFalling = false;

            if (!wasGroundedLastFrame && isJumping)
            {
                // Atterrissage : on applique une impulsion vers le bas et on déclenche l'animation dédiée.
                bool moving = horizontalVelocity.sqrMagnitude > 0.01f;
                if (animator != null)
                {
                    if (moving)
                    {
                        animator.Play("Landing_OnMove");
                        activeLandingStateHash = landingOnMoveStateHash;
                        landingAnimationEndTime = Time.time + landingOnMoveClipLength;
                    }
                    else
                    {
                        animator.Play("Landing");
                        activeLandingStateHash = landingStateHash;
                        landingAnimationEndTime = Time.time + landingClipLength;
                    }
                }

                // Mémorisation complète de la vitesse pour maintenir l'inertie durant la séquence.
                landingVelocitySnapshot = horizontalVelocity;
                landingSpeedSnapshot = currentSpeed;
                if (landingVelocitySnapshot.sqrMagnitude > 0.0001f)
                {
                    lastMoveDirection = landingVelocitySnapshot.normalized;
                }

                verticalVelocity = -landingForce;
                isJumping = false;
                landingAnimationLocked = true;
                currentAnimState = MovementAnimation.IsLanding;
            }

            // Lorsque l'on est collé au sol, on évite les oscillations verticales.
            if (verticalVelocity < -groundStickForce)
            {
                verticalVelocity = -groundStickForce;
            }
        }
        else
        {
            if (wasGroundedLastFrame && !isJumping)
            {
                // Transition sol → air sans saut explicite : on considère qu'on tombe.
                isFalling = true;
            }
        }

        wasGroundedLastFrame = groundedStable;
    }

    /// <summary>
    /// Gestion du déplacement horizontal avec inertie, contrôle aérien et glissades.
    /// </summary>
    private void HandleHorizontalMove()
    {
        if (cameraTransform == null)
            return;

        // Direction voulue relative à la caméra (plan XZ).
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 desiredMove = camForward * moveInput.y + camRight * moveInput.x;

        if (desiredMove.sqrMagnitude > 1f)
            desiredMove.Normalize();
        else
            desiredMove = desiredMove.normalized;

        bool hasInput = moveInput.sqrMagnitude > 0.01f;
        bool runBuffered = runPressed || runReleaseTimer > 0f;

        // La course est possible uniquement si canRun est vrai.
        isRunning = hasInput && runBuffered && canRun && !isSliding;
        isWalking = hasInput && (!runBuffered || !canRun) && !isSliding;

        // Si l'animation d'atterrissage est verrouillée, on fige complètement la vitesse horizontale.
        if (landingAnimationLocked)
        {
            currentSpeed = landingSpeedSnapshot;
            horizontalVelocity = landingVelocitySnapshot;

            controller.Move(horizontalVelocity * Time.deltaTime);

            if (horizontalVelocity.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(horizontalVelocity);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, orientationLerpSpeed * Time.deltaTime);
            }

            return; // On stoppe ici pour conserver l'inertie et éviter tout ajustement de vitesse.
        }

        float targetSpeed = 0f;
        if (hasInput)
            targetSpeed = isRunning ? runSpeed : walkSpeed;

        float accelRate = targetSpeed > currentSpeed ? acceleration : deceleration;

        if (groundedStable && hasInput && !IsGroundAhead(desiredMove))
        {
            // Pas de sol devant → annule la vitesse horizontale pour éviter les chutes involontaires.
            targetSpeed = 0f;
            currentSpeed = 0f;
            horizontalVelocity = Vector3.zero;
            hasInput = false;
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);
        }

        Vector3 effectiveDirection = desiredMove;

        if (isSliding)
        {
            // Mélange la direction de glisse naturelle avec l'influence du joueur.
            Vector3 slideDirection = slopeSlideDirection;
            if (slideDirection == Vector3.zero)
                slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

            Vector3 controlDirection = desiredMove.sqrMagnitude > 0.001f ? Vector3.ProjectOnPlane(desiredMove, groundNormal).normalized : Vector3.zero;
            Vector3 combinedDirection = (slideDirection + controlDirection * airControlPercent).normalized;

            currentSpeed = Mathf.MoveTowards(currentSpeed, slopeSlideSpeed, slopeSlideAcceleration * Time.deltaTime);
            effectiveDirection = combinedDirection;
        }
        else if (!groundedStable)
        {
            if (lastMoveDirection == Vector3.zero)
                lastMoveDirection = transform.forward;

            if (desiredMove.sqrMagnitude > 0.001f)
                effectiveDirection = Vector3.Slerp(lastMoveDirection, desiredMove, airControlPercent);
            else
                effectiveDirection = lastMoveDirection;
        }
        else if (!hasInput && horizontalVelocity.sqrMagnitude > 0.001f)
        {
            // Maintient légèrement l'inertie au sol lorsqu'on relâche les inputs.
            effectiveDirection = horizontalVelocity.normalized;
        }

        if (effectiveDirection.sqrMagnitude > 0.001f)
        {
            if (groundedStable && !isSliding)
            {
                // Épouse la pente pour éviter que le personnage ne "flotte".
                effectiveDirection = Vector3.ProjectOnPlane(effectiveDirection, groundNormal).normalized;
            }

            horizontalVelocity = effectiveDirection * currentSpeed;
            lastMoveDirection = effectiveDirection;
        }
        else
        {
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        controller.Move(horizontalVelocity * Time.deltaTime);

        // Orientation (autorisé en l'air pour du contrôle visuel).
        Vector3 lookDirection = horizontalVelocity.sqrMagnitude > 0.0001f ? horizontalVelocity : effectiveDirection;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, orientationLerpSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Gestion du saut intelligent et de la gravité non linéaire.
    /// </summary>
    private void HandleVerticalMove()
    {
        bool recentlyGrounded = Time.time - lastGroundedTimestamp <= coyoteTime;
        bool jumpBuffered = Time.time - lastJumpPressedTimestamp <= jumpBufferTime;

        if (jumpBuffered && recentlyGrounded && !landingAnimationLocked)
        {
            float baseJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity = baseJumpVelocity * jumpBoost;
            isJumping = true;
            isFalling = false;
            lastJumpPressedTimestamp = float.NegativeInfinity;

            if (animator != null)
            {
                animator.Play("Jump_Start");
            }
        }

        if (groundedStable && verticalVelocity < 0f)
        {
            // Plaque légèrement le personnage au sol pour éviter les micro-sauts.
            verticalVelocity = -groundStickForce;
        }
        else
        {
            float gravityMultiplier = verticalVelocity > 0f ? ascentGravityMultiplier : fallGravityMultiplier;
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
        }

        if (!groundedStable && verticalVelocity < 0f)
        {
            isFalling = true;
        }

        controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    /// <summary>
    /// Évite les chutes involontaires : vérifie s'il y a du sol devant.
    /// </summary>
    private bool IsGroundAhead(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return true;

        Vector3 origin = controller.bounds.center + Vector3.up * 0.05f;
        float checkDistance = controller.radius + voidCheckDistance;
        Vector3 checkPos = origin + direction.normalized * checkDistance;

        int mask = groundLayer.value == 0 ? Physics.DefaultRaycastLayers : groundLayer;
        return Physics.Raycast(checkPos, Vector3.down, voidCheckDepth, mask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Sélectionne et lance les animations de locomotion (idle/walk/run) sans paramètres Animator.
    /// </summary>
    private void UpdateMovementAnimation()
    {
        if (animator == null || isJumping || landingAnimationLocked)
            return;

        MovementAnimation targetState =
            isRunning ? MovementAnimation.Run :
            isWalking ? MovementAnimation.Walk :
            MovementAnimation.Idle;

        if (targetState == currentAnimState)
            return;

        switch (targetState)
        {
            case MovementAnimation.Run:
                animator.CrossFade("Run Start", locomotionCrossFadeDuration);
                break;
            case MovementAnimation.Walk:
                animator.CrossFade("Walk_Start", locomotionCrossFadeDuration);
                break;
            case MovementAnimation.Idle:
                if (currentAnimState == MovementAnimation.Run) animator.CrossFade("Run Stop", locomotionCrossFadeDuration);
                else if (currentAnimState == MovementAnimation.Walk) animator.CrossFade("Walk_Stop", locomotionCrossFadeDuration);
                else animator.CrossFade("Idle_World", locomotionCrossFadeDuration);
                break;
        }

        currentAnimState = targetState;
    }

    /// <summary>
    /// Enchaîne automatiquement vers la boucle de saut quand l'anticipation est finie.
    /// </summary>
    private void UpdateJumpAnimation()
    {
        if (!isJumping || animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Jump_Start") && state.normalizedTime >= 1f)
        {
            animator.CrossFade("Jump_Loop", 0.1f);
        }
    }

    /// <summary>
    /// Libère le verrou une fois l'animation d'atterrissage terminée et relance la bonne locomotion.
    /// </summary>
    private void UpdateLandingLock()
    {
        if (!landingAnimationLocked || animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        bool isCurrentLanding = state.shortNameHash == activeLandingStateHash;
        bool animationCompleted = Time.time >= landingAnimationEndTime;

        // Si nous sommes toujours dans l'état d'atterrissage, on vérifie également l'avancement de la timeline.
        if (isCurrentLanding && state.normalizedTime >= 1f)
        {
            animationCompleted = true;
        }

        // Tant que l'animation dédiée n'est pas totalement achevée, aucune transition n'est autorisée.
        if (!animationCompleted)
            return;

        landingAnimationLocked = false; // Permet de rejouer les animations de locomotion et d'autoriser le saut.

        MovementAnimation targetState;
        if (isRunning)
        {
            targetState = MovementAnimation.Run;
            animator.Play("Run Start");
        }
        else if (isWalking)
        {
            targetState = MovementAnimation.Walk;
            animator.Play("Walk_Start");
        }
        else
        {
            targetState = MovementAnimation.Idle;
            animator.Play("Idle_World");
        }

        currentAnimState = targetState;
    }

    /// <summary>
    /// Récupère la durée des animations de landing afin de pouvoir verrouiller les transitions jusqu'à leur fin.
    /// </summary>
    private void CacheLandingClipDurations()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
                continue;

            if (clip.name == "Landing")
            {
                landingClipLength = clip.length;
            }
            else if (clip.name == "Landing_OnMove")
            {
                landingOnMoveClipLength = clip.length;
            }
        }

        // Sécurités : si les clips ne sont pas trouvés (variant de nom, configuration incomplète...), on conserve des valeurs par défaut.
        if (landingClipLength <= 0f)
            landingClipLength = 0.5f;

        if (landingOnMoveClipLength <= 0f)
            landingOnMoveClipLength = landingClipLength;
    }

    public void ToggleMuninLink()
    {
        linkToMunin = !linkToMunin;
    }

    public void LinkMuninToLucian()
    {
        linkToMunin = true;
    }

    /// <summary>
    /// Applique les effets visuels associés au lien entre Lucian et Munin.
    /// Gère à la fois l'affichage via le culling mask de la WorldCamera et
    /// l'activation des objets placés sur les couches dédiées.
    /// </summary>
    private void ApplyMuninLinkState()
    {
        // Sécurités : certaines scènes ou tests peuvent ne pas disposer d'une WorldCamera.
        if (worldCamera != null)
        {
            // On repart du culling mask d'origine pour éviter les accumulations d'états.
            if (linkToMunin) worldCamera.cullingMask = worldBaseMask | revealMask;
            else worldCamera.cullingMask = worldBaseMask & ~revealMask;
        }

        // Active ou désactive tous les GameObjects appartenant aux couches révélées.
        // Passage à FindObjectsByType afin de respecter les recommandations Unity, d'éviter le tri inutile
        // et de récupérer également les objets inactifs nécessaires à l'effet de révélation.
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject obj = t.gameObject;
            if (obj.layer == revealInteractableLayer || obj.layer == revealUILayer || obj.layer == revealObjectLayer)
                obj.SetActive(linkToMunin);
        }

        previousLinkToMunin = linkToMunin; // On enregistre l'état courant pour la prochaine vérification.
    }
}
