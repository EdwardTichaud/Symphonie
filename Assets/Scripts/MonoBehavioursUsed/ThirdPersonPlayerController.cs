using UnityEngine;

/// <summary>
/// Third-person controller (exploration) — version pilotée par paramètres Animator uniquement.
/// - AUCUN appel à Animator.Play / CrossFade.
/// - Tout passe par des bools / triggers / int / float.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Transform de la WorldCamera utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    private CharacterController controller;
    [SerializeField] private Animator animator;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private Vector3 lastMoveDirection = Vector3.forward;

    // Verrou d'atterrissage (pour laisser l’anim se jouer proprement).
    private bool landingAnimationLocked;
    private Vector3 landingVelocitySnapshot;
    private float landingSpeedSnapshot;
    private float landingAnimationEndTime;

    // Durées des animations d'atterrissage (récupérées dynamiquement).
    private float landingClipLength = 0.5f;
    private float landingOnMoveClipLength = 0.5f;

    [Header("Lien avec Munin")]
    [Tooltip("Détermine si Lucian est actuellement lié à Munin.")]
    public bool linkToMunin;
    private bool previousLinkToMunin;
    private Camera worldCamera;
    private int worldBaseMask;
    private int revealInteractableLayer;
    private int revealObjectLayer;
    private int revealUILayer;
    private int revealMask;

    // États exposés
    public bool isWalking;
    public bool isRunning;
    public bool isJumping;
    public bool isFalling;
    public bool isSliding;

    /// <summary>Indique si le personnage touche le sol de manière fiable.</summary>
    public bool isGrounded => groundedStable;

    [Header("Vitesses de base")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Saut & Gravité")]
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float jumpBoost = 1.1f;
    public float ascentGravityMultiplier = 1f;
    public float fallGravityMultiplier = 2f;
    public float terminalVelocity = -35f;
    public float landingForce = 5f;

    [Header("Inertie du déplacement")]
    public float acceleration = 20f;
    public float deceleration = 25f;
    public float orientationLerpSpeed = 12f;
    [Range(0f, 1f)] public float airControlPercent = 0.35f;

    private float currentSpeed;

    [Header("Animation — cohérence vitesse/état")]
    [Tooltip("Vitesse normalisée (0 à 1) en dessous de laquelle Lucian est considéré immobile pour l'Animator.")]
    [Range(0f, 0.3f)] public float idleNormalizedSpeedThreshold = 0.1f;
    [Tooltip("Vitesse normalisée (0 à 1) à partir de laquelle l'animation de course est prioritaire.")]
    [Range(0.3f, 1f)] public float runNormalizedSpeedThreshold = 0.7f;
    [Tooltip("Facteur de lissage (exponentiel) appliqué à la vitesse envoyée à l'Animator pour éviter les à-coups.")]
    public float animatorSpeedLerpSharpness = 12f;

    // Mémorise la vitesse normalisée envoyée au BlendTree Idle/Walk/Run.
    private float animatorNormalizedSpeed;

    [Header("Gestion du sprint")]
    public float runReleaseDelay = 1f;
    public bool canRun = true;
    private float runReleaseTimer;

    [Header("Détection du sol & des pentes")]
    public float groundCheckOffset = 0.3f;
    public float groundCheckDistance = 0.6f;
    [Range(0.5f, 1f)] public float groundProbeRadiusFactor = 0.9f;
    public float groundStickForce = 6f;
    public LayerMask groundLayer = ~0;
    public float slopeSlideSpeed = 6f;
    public float slopeSlideAcceleration = 30f;

    [Header("Sécurité contre le vide")]
    public float voidCheckDistance = 1f;
    public float voidCheckDepth = 2f;

    [Header("Aides au saut")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    private bool groundedStable;
    private bool wasGroundedLastFrame;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 slopeSlideDirection = Vector3.zero;
    private float lastGroundedTimestamp = float.NegativeInfinity;
    private float lastJumpPressedTimestamp = float.NegativeInfinity;

    // Inputs
    private Vector2 moveInput;
    private bool runPressed;

    // =========================
    // Paramètres Animator
    // =========================
    // Remets les mêmes noms dans ton Animator Controller.
    private static readonly int pLocomotionState = Animator.StringToHash("LocomotionState"); // int: 0 Idle, 1 Walk, 2 Run
    private static readonly int pSpeed = Animator.StringToHash("Speed");           // float: vitesse actuelle (0..runSpeed)
    private static readonly int pIsGrounded = Animator.StringToHash("IsGrounded");      // bool
    private static readonly int pIsSliding = Animator.StringToHash("IsSliding");       // bool
    private static readonly int pIsFalling = Animator.StringToHash("IsFalling");       // bool
    private static readonly int pIsJumping = Animator.StringToHash("IsJumping");       // bool (optionnel si tu préfères un trigger)
    private static readonly int pJumpTrigger = Animator.StringToHash("JumpTrigger");     // trigger: lancé au départ du saut
    private static readonly int pLandTrigger = Animator.StringToHash("LandTrigger");     // trigger: lancé à l’atterrissage
    private static readonly int pLandingMode = Animator.StringToHash("LandingMode");     // int: 0 = Landing, 1 = Landing_OnMove
    private static readonly int pVerticalVelocity = Animator.StringToHash("VerticalVelocity"); // float: utile pour blends/conditions
    private static readonly int pMoveX = Animator.StringToHash("MoveX");           // float: input latéral (optionnel)
    private static readonly int pMoveY = Animator.StringToHash("MoveY");           // float: input avant (optionnel)

    private const float locomotionCrossFadeDuration = 0.1f; // Conservé si tu veux l’utiliser côté Animator (pas ici).

    private enum MovementAnimation { Idle, Walk, Run, IsLanding }
    [SerializeField] private MovementAnimation currentAnimState = MovementAnimation.Idle;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator != null)
        {
            animator.applyRootMotion = false;

            // Valeurs initiales des paramètres
            animator.SetInteger(pLocomotionState, 0);
            animator.SetFloat(pSpeed, 0f);
            animator.SetBool(pIsGrounded, true);
            animator.SetBool(pIsSliding, false);
            animator.SetBool(pIsFalling, false);
            animator.SetBool(pIsJumping, false);
            animator.SetFloat(pVerticalVelocity, 0f);
            animator.SetFloat(pMoveX, 0f);
            animator.SetFloat(pMoveY, 0f);

            CacheLandingClipDurations();
        }
        else
        {
            Debug.LogWarning(
                "[ThirdPersonPlayerController] Aucun Animator trouvé dans la hiérarchie du joueur. " +
                "Les animations de déplacement resteront figées tant qu'un Animator valide ne sera pas associé.");
        }

        if (cameraTransform == null)
        {
            Camera worldCam = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
            if (worldCam != null) cameraTransform = worldCam.transform;
            else if (Camera.main != null) cameraTransform = Camera.main.transform;
            else Debug.LogWarning("[ThirdPersonPlayerController] Aucune WorldCamera ou MainCamera trouvée.");
        }

        if (cameraTransform != null)
        {
            worldCamera = cameraTransform.GetComponent<Camera>();
            if (worldCamera != null) worldBaseMask = worldCamera.cullingMask;
        }

        revealInteractableLayer = LayerMask.NameToLayer("World_ReveLink_Interactable");
        revealObjectLayer = LayerMask.NameToLayer("World_ReveLink_Object");
        revealUILayer = LayerMask.NameToLayer("World_ReveLink_UI");
        if (revealInteractableLayer != -1) revealMask |= 1 << revealInteractableLayer;
        if (revealObjectLayer != -1) revealMask |= 1 << revealObjectLayer;
        if (revealUILayer != -1) revealMask |= 1 << revealUILayer;

        previousLinkToMunin = !linkToMunin;
        ApplyMuninLinkState();

        wasGroundedLastFrame = controller.isGrounded;
        groundedStable = wasGroundedLastFrame;
    }

    void Update()
    {
        if (linkToMunin != previousLinkToMunin)
            ApplyMuninLinkState();

        CacheInputs();
        EvaluateGroundState();
        HandleHorizontalMove();
        HandleVerticalMove();
        PushAnimatorLocomotionParameters();
        UpdateLandingLock();
    }

    private void CacheInputs()
    {
        moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        runPressed = canRun && InputsManager.Instance.playerInputs.World.Run.IsPressed();

        if (runPressed) runReleaseTimer = runReleaseDelay;
        else runReleaseTimer = Mathf.Max(runReleaseTimer - Time.deltaTime, 0f);

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

            if (hitInfo.distance <= castDistance + 0.05f && !tooSteep)
                validGround = true;

            if (controllerGrounded && tooSteep)
            {
                isSliding = true;
                slopeSlideDirection = Vector3.ProjectOnPlane(Vector3.down, hitInfo.normal).normalized;
            }
        }

        groundedStable = validGround;

        if (groundedStable)
        {
            lastGroundedTimestamp = Time.time;
            isFalling = false;

            if (!wasGroundedLastFrame && isJumping)
            {
                // ATERRISSAGE
                bool moving = horizontalVelocity.sqrMagnitude > 0.01f;

                // Mémorise pour le lock (optionnel mais agréable)
                landingVelocitySnapshot = horizontalVelocity;
                landingSpeedSnapshot = currentSpeed;
                if (landingVelocitySnapshot.sqrMagnitude > 0.0001f)
                    lastMoveDirection = landingVelocitySnapshot.normalized;

                verticalVelocity = -landingForce;
                isJumping = false;
                landingAnimationLocked = true;

                // Déclenchement via paramètres
                if (animator != null)
                {
                    animator.SetInteger(pLandingMode, moving ? 1 : 0); // 0 = Landing, 1 = Landing_OnMove
                    animator.ResetTrigger(pJumpTrigger);
                    animator.SetBool(pIsJumping, false);
                    animator.SetTrigger(pLandTrigger);

                    // Durée d’attente liée au clip (si dispo)
                    landingAnimationEndTime = Time.time + (moving ? landingOnMoveClipLength : landingClipLength);
                }

                currentAnimState = MovementAnimation.IsLanding;
            }

            if (verticalVelocity < -groundStickForce)
                verticalVelocity = -groundStickForce;
        }
        else
        {
            if (wasGroundedLastFrame && !isJumping)
                isFalling = true;
        }

        wasGroundedLastFrame = groundedStable;
    }

    private void HandleHorizontalMove()
    {
        if (cameraTransform == null)
            return;

        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 desiredMove = camForward * moveInput.y + camRight * moveInput.x;

        if (desiredMove.sqrMagnitude > 1f) desiredMove.Normalize();
        else desiredMove = desiredMove.normalized;

        bool hasInput = moveInput.sqrMagnitude > 0.01f;
        bool runBuffered = runPressed || runReleaseTimer > 0f;

        isRunning = hasInput && runBuffered && canRun && !isSliding;
        isWalking = hasInput && (!runBuffered || !canRun) && !isSliding;

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
            return;
        }

        float targetSpeed = 0f;
        if (hasInput) targetSpeed = isRunning ? runSpeed : walkSpeed;

        float accelRate = targetSpeed > currentSpeed ? acceleration : deceleration;

        if (groundedStable && hasInput && !IsGroundAhead(desiredMove))
        {
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
            effectiveDirection = horizontalVelocity.normalized;
        }

        if (effectiveDirection.sqrMagnitude > 0.001f)
        {
            if (groundedStable && !isSliding)
                effectiveDirection = Vector3.ProjectOnPlane(effectiveDirection, groundNormal).normalized;

            horizontalVelocity = effectiveDirection * currentSpeed;
            lastMoveDirection = effectiveDirection;
        }
        else
        {
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        controller.Move(horizontalVelocity * Time.deltaTime);

        Vector3 lookDirection = horizontalVelocity.sqrMagnitude > 0.0001f ? horizontalVelocity : effectiveDirection;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, orientationLerpSpeed * Time.deltaTime);
        }
    }

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

            // Déclenchement via paramètres
            if (animator != null)
            {
                animator.SetBool(pIsJumping, true);
                animator.ResetTrigger(pLandTrigger);
                animator.SetTrigger(pJumpTrigger);
            }
        }

        if (groundedStable && verticalVelocity < 0f)
        {
            verticalVelocity = -groundStickForce;
        }
        else
        {
            float gravityMultiplier = verticalVelocity > 0f ? ascentGravityMultiplier : fallGravityMultiplier;
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
        }

        if (!groundedStable && verticalVelocity < 0f)
            isFalling = true;

        controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

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
    /// Pousse toutes les valeurs nécessaires vers l'Animator (locomotion & états).
    /// </summary>
    private void PushAnimatorLocomotionParameters()
    {
        if (animator == null)
            return;

        // Vitesse horizontale réelle calculée à partir du CharacterController (ignorant l'axe vertical).
        float rawHorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

        // Convertit cette vitesse en valeur normalisée (0 = immobile, 1 = runSpeed ou plus).
        float targetNormalizedSpeed = 0f;
        if (runSpeed > 0.0001f)
            targetNormalizedSpeed = Mathf.Clamp01(rawHorizontalSpeed / runSpeed);

        // Lisser la valeur pour éviter les oscillations rapides qui provoqueraient des transitions d'états parasites.
        if (animatorSpeedLerpSharpness > 0f)
        {
            float lerpFactor = 1f - Mathf.Exp(-animatorSpeedLerpSharpness * Time.deltaTime);
            animatorNormalizedSpeed = Mathf.Lerp(animatorNormalizedSpeed, targetNormalizedSpeed, lerpFactor);
        }
        else
        {
            animatorNormalizedSpeed = targetNormalizedSpeed;
        }

        if (landingAnimationLocked)
            return; // pendant le lock, on laisse l’anim d’atterrissage régner malgré la vitesse mesurée.

        // Assurer des seuils cohérents (run > idle) puis déterminer l'état de locomotion le plus adapté.
        float idleThreshold = Mathf.Clamp01(idleNormalizedSpeedThreshold);
        float runThreshold = Mathf.Clamp01(Mathf.Max(runNormalizedSpeedThreshold, idleThreshold + 0.05f));

        int loco;
        if (animatorNormalizedSpeed <= idleThreshold)
            loco = (int)MovementAnimation.Idle;
        else if (animatorNormalizedSpeed < runThreshold)
            loco = (int)MovementAnimation.Walk;
        else
            loco = (int)MovementAnimation.Run;

        animator.SetInteger(pLocomotionState, loco);
        animator.SetFloat(pSpeed, animatorNormalizedSpeed);
        animator.SetBool(pIsGrounded, groundedStable);
        animator.SetBool(pIsSliding, isSliding);
        animator.SetBool(pIsFalling, isFalling);
        animator.SetBool(pIsJumping, isJumping);
        animator.SetFloat(pVerticalVelocity, verticalVelocity);

        // Optionnel : utiles si ton blend-tree 2D les exploite
        animator.SetFloat(pMoveX, moveInput.x);
        animator.SetFloat(pMoveY, moveInput.y);

        currentAnimState = (MovementAnimation)loco;
    }

    /// <summary>
    /// Libère le verrou une fois la fenêtre d'atterrissage écoulée.
    /// (La fin exacte est synchronisée via la durée du clip lue au démarrage.)
    /// </summary>
    private void UpdateLandingLock()
    {
        if (!landingAnimationLocked)
            return;

        // Tant que la fenêtre n’est pas écoulée, on garde le lock.
        if (Time.time < landingAnimationEndTime)
            return;

        landingAnimationLocked = false;
    }

    private void CacheLandingClipDurations()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            if (clip.name == "Landing") landingClipLength = clip.length;
            else if (clip.name == "Landing_OnMove") landingOnMoveClipLength = clip.length;
        }

        if (landingClipLength <= 0f) landingClipLength = 0.5f;
        if (landingOnMoveClipLength <= 0f) landingOnMoveClipLength = landingClipLength;
    }

    public void ToggleMuninLink() => linkToMunin = !linkToMunin;
    public void LinkMuninToLucian() => linkToMunin = true;

    private void ApplyMuninLinkState()
    {
        if (worldCamera != null)
        {
            if (linkToMunin) worldCamera.cullingMask = worldBaseMask | revealMask;
            else worldCamera.cullingMask = worldBaseMask & ~revealMask;
        }

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject obj = t.gameObject;
            if (obj.layer == revealInteractableLayer || obj.layer == revealUILayer || obj.layer == revealObjectLayer)
                obj.SetActive(linkToMunin);
        }

        previousLinkToMunin = linkToMunin;
    }
}
