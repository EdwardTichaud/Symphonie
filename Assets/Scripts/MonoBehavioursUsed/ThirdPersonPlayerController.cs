using UnityEngine;

/// <summary>
/// Contrôleur simple à la troisième personne pour Lucian en mode exploration.
/// Gère le déplacement de base (marche/course), l'orientation et le saut.
/// La caméra (Munin) peut pivoter autour de Lucian via <see cref="ThirdPersonCameraController"/>.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Transform de la WorldCamera utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    private CharacterController controller; // Référence au CharacterController Unity.
    private Animator animator;              // Référence à l'Animator pour déclencher les animations.
    private Vector3 velocity;               // Vitesse verticale (gravité / saut).
    private Vector3 horizontalVelocity;     // Vitesse horizontale, conservée en l'air pour éviter les changements brusques.

    // Verrouillage utilisé lorsque l'animation d'atterrissage est en cours pour éviter toute interruption.
    private bool landingAnimationLocked;

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

    /// <summary>Indique si le personnage touche le sol.</summary>
    public bool isGrounded => controller.isGrounded;

    [Header("Mouvement")]
    [Tooltip("Vitesse de marche en unités/seconde.")]
    public float walkSpeed = 5f;
    [Tooltip("Vitesse de course en unités/seconde.")]
    public float runSpeed = 8f;
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
    [Tooltip("Force dirigée vers le bas appliquée à l'atterrissage pour renforcer l'impact.")]
    public float landingForce = 5f;

    [Header("Inertie du déplacement")]
    [Tooltip("Taux d'accélération en unités/seconde².")]
    public float acceleration = 20f;
    [Tooltip("Taux de décélération en unités/seconde².")]
    public float deceleration = 25f;

    private float currentSpeed; // Vitesse courant (accélération/décélération progressive).

    [Header("Course")]
    [Tooltip("Durée durant laquelle la course reste active après avoir relâché l'input (secondes).")]
    public float runReleaseDelay = 1f;

    private float runReleaseTimer;

    [Header("Détection des vides")]
    [Tooltip("Distance avant le personnage utilisée pour vérifier la présence du sol.")]
    public float voidCheckDistance = 1f;
    [Tooltip("Profondeur minimale du raycast pour considérer qu'il y a du sol.")]
    public float voidCheckDepth = 2f;
    [Tooltip("Couches reconnues comme du sol lors de la détection des vides.")]
    public LayerMask groundLayer = ~0; // Par défaut : toutes les couches.

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
        }

        // Si aucune caméra n'est assignée, on cherche d'abord la WorldCamera, sinon la MainCamera.
        if (cameraTransform == null)
        {
            Camera worldCam = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
            if (worldCam != null) cameraTransform = worldCam.transform;
            else if (Camera.main != null) cameraTransform = Camera.main.transform;
            else Debug.LogWarning("[ThirdPersonPlayerController] Aucune WorldCamera ou MainCamera trouvée.");
        }
    }

    void Update()
    {
        HandleMovement();      // Déplacement horizontal + orientation.
        ApplyGravity();        // Déplacement vertical (gravité / saut).
        UpdateJumpAnimation(); // Gestion de la boucle de saut en l'air.
        UpdateLandingLock();   // Déverrouillage après l'animation d'atterrissage.
    }

    /// <summary>
    /// Sélectionne et lance les animations de locomotion (idle/walk/run) sans paramètres Animator.
    /// </summary>
    void UpdateMovementAnimation()
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
    /// Lecture des inputs, calcul de la vitesse horizontale, orientation du personnage.
    /// </summary>
    void HandleMovement()
    {
        if (cameraTransform == null)
            return;

        // Axes de déplacement (WASD / stick gauche).
        Vector2 input = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        bool runPressed = InputsManager.Instance.playerInputs.World.Run.IsPressed();

        // Buffer de relâchement du sprint (évite ping-pong marche/course).
        if (runPressed) runReleaseTimer = runReleaseDelay;
        else runReleaseTimer = Mathf.Max(runReleaseTimer - Time.deltaTime, 0f);

        // Direction voulue relative à la caméra (plan XZ).
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 desiredMove = camForward * input.y + camRight * input.x;

        bool hasInput = input.sqrMagnitude > 0.01f;
        bool runBuffered = runPressed || runReleaseTimer > 0f;

        isRunning = hasInput && runBuffered;
        isWalking = hasInput && !runBuffered;

        float targetSpeed = isRunning ? runSpeed : (isWalking ? walkSpeed : 0f);
        float accelRate = targetSpeed > currentSpeed ? acceleration : deceleration;

        if (controller.isGrounded)
        {
            if (!IsGroundAhead(desiredMove))
            {
                // Pas de sol devant → annule la vitesse horizontale.
                targetSpeed = 0f;
                currentSpeed = 0f;
                horizontalVelocity = Vector3.zero;
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);
                horizontalVelocity = desiredMove.normalized * currentSpeed; // Mémorisée pour l'air.
            }
        }

        // Déplacement horizontal par code (animations in-place).
        controller.Move(horizontalVelocity * Time.deltaTime);

        // Orientation (autorisé en l'air pour du contrôle visuel).
        if (desiredMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        // Saut
        if (InputsManager.Instance.playerInputs.World.Jump.triggered && controller.isGrounded && !landingAnimationLocked)
        {
            float baseJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            velocity.y = baseJumpVelocity * jumpBoost;
            isJumping = true;

            if (animator != null) animator.Play("Jump_Start");
        }

        UpdateMovementAnimation();
    }

    /// <summary>
    /// Évite les chutes involontaires : vérifie s'il y a du sol devant.
    /// </summary>
    bool IsGroundAhead(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return true;

        Vector3 origin = controller.bounds.center + direction.normalized * voidCheckDistance;
        return Physics.Raycast(origin, Vector3.down, voidCheckDepth, groundLayer);
    }

    /// <summary>
    /// Gravité non linéaire et déplacement vertical.
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            if (isJumping && animator != null)
            {
                // Choix du landing selon la vitesse horizontale conservée en l'air.
                bool isMoving = horizontalVelocity.sqrMagnitude > 0.01f;

                if (isMoving) animator.Play("Landing_OnMove");
                else animator.Play("Landing");

                velocity.y = -landingForce;     // Petit impact vers le bas.
                isJumping = false;
                landingAnimationLocked = true;  // Empêche qu'on écrase l'atterrissage.
                currentAnimState = MovementAnimation.IsLanding;
            }
            else
            {
                velocity.y = -2f; // Ancrage au sol.
            }
        }
        else
        {
            float gravityMultiplier = velocity.y > 0f ? ascentGravityMultiplier : fallGravityMultiplier;
            velocity.y += gravity * gravityMultiplier * Time.deltaTime;
        }

        // Mouvement vertical (séparé du horizontal).
        controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
    }

    /// <summary>
    /// Enchaîne automatiquement vers la boucle de saut quand l'anticipation est finie.
    /// </summary>
    void UpdateJumpAnimation()
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
    void UpdateLandingLock()
    {
        if (!landingAnimationLocked || animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if ((state.IsName("Landing") || state.IsName("Landing_OnMove")) && state.normalizedTime >= 1f)
        {
            landingAnimationLocked = false;

            MovementAnimation targetState;
            if (isRunning)
            {
                targetState = MovementAnimation.Run;
                animator.Play("Run Start"); // Ou "Run" direct si le start est trop long.
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
    }
}
