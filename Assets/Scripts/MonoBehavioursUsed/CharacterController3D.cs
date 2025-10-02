using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterController3D : MonoBehaviour
{
    public enum MovementMode
    {
        FixedCamera,
        TPSOverShoulder
    }

    // ⚠️ Ce contrôleur s'appuie sur Camera.main pour orienter le déplacement.
    // Si aucune caméra n'est taguée "MainCamera" (par exemple avant d'activer la BattleCamera),
    // les vecteurs de mouvement ne peuvent pas être calculés et le joueur reste immobile.
    public MovementMode movementMode = MovementMode.FixedCamera;

    [HideInInspector] public CharacterController controller;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float rotationSpeed = 10f;

    [Header("Jump & Fall Settings")]
    [Tooltip("Temps pendant lequel un saut reste possible après avoir quitté le sol (Coyote Time).")]
    public float coyoteTime = 0.2f;
    [Tooltip("Temps pendant lequel un appui sur Saut est mémorisé avant l'atterrissage (Jump Buffer).")]
    public float jumpBufferTime = 0.2f;
    [Tooltip("Multiplicateur appliqué à la gravité lorsque le personnage monte.")]
    public float jumpGravityMultiplier = 1f;
    [Tooltip("Multiplicateur appliqué à la gravité lorsque le personnage chute.")]
    public float fallGravityMultiplier = 2f;
    [Tooltip("Limite de vitesse de chute pour éviter des valeurs extrêmes.")]
    public float terminalVelocity = -30f;

    [Header("Slope & Air Control Settings")]
    [Tooltip("Vitesse maximale atteinte lors d'un glissement sur une pente trop raide.")]
    public float slopeSlideSpeed = 6f;
    [Tooltip("Vitesse à laquelle le glissement rejoint la vitesse cible.")]
    public float slopeSlideAcceleration = 25f;
    [Range(0f, 1f), Tooltip("Pourcentage de contrôle conservé dans les airs. 0 = aucun contrôle, 1 = contrôle total.")]
    public float airControlPercent = 0.35f;

    [Header("Inertia Settings")]
    [Tooltip("Accélération appliquée lors du démarrage d'un déplacement")]
    public float acceleration = 15f;
    [Tooltip("Décélération appliquée lorsque l'entrée de déplacement s'arrête")]
    public float deceleration = 20f;
    // Vitesse actuelle utilisée pour interpoler progressivement la vitesse cible
    private float currentSpeed = 0f;

    [Header("Fall Prevention")]
    public float groundCheckDistance = 1f; // distance to check for ground
    public LayerMask groundLayer;

    private Vector3 velocity;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 slopeSlideDirection = Vector3.zero;
    private Vector3 lastMoveDirection = Vector3.forward;
    private float lastGroundedTimestamp = float.NegativeInfinity;
    private float lastJumpPressedTimestamp = float.NegativeInfinity;

    [Header("Debug Movement State")]
    public bool isGrounded;
    public bool isWalking;
    public bool wasWalking;
    public bool isRunning;
    public bool wasRunning;
    public bool isJumping;
    public bool isFalling;
    public bool isSliding;
    public bool isHurt;
    public bool isDodging;
    public bool isDead;
    public bool dashTriggered;
    public bool isApproaching;
    public bool isRetreating;
    public bool isTaunting;
    public bool isThreatening;

    [Header("TPS Mode Settings")]
    [Range(0f, 0.5f)] public float turnSmoothTime = 0.1f;
    [Range(0f, 0.5f)] public float stickDeadZoneX = 0.2f;
    [Range(0f, 45f)] public float angleThreshold = 10f;

    [Header("Auto Align Settings")]
    public bool enableAutoAlign = true;
    public float autoAlignDelay = 0.5f;  // Temps d'inactivité avant de recadrer
    public float autoAlignSpeed = 5f;    // Vitesse du recadrage
    private float idleTimer = 0f;

    #region Cycle de Vie
    /// <summary>
    /// Récupère le CharacterController associé.
    /// </summary>
    void Start()
    {
        controller = GetComponent<CharacterController>();

        // S'assure qu'un masque de sol est défini pour ne pas bloquer le déplacement
        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
            Debug.LogWarning("[CharacterController3D] 'groundLayer' non spécifié, utilisation du layer 'Default'.");
        }
    }

    /// <summary>
    /// Gère les entrées et le mouvement à chaque frame.
    /// </summary>
    void Update()
    {
        if (!controller.enabled) return;

        EvaluateGroundState();
        ReadInputs();
        HandleMovement();
        ApplyGravity();
    }

    /// <summary>
    /// Lit les entrées du joueur et met à jour les états locaux.
    /// </summary>
    private void ReadInputs()
    {
        Vector2 moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();

        // Stocke l'état précédent avant de calculer le nouvel état (utile pour détecter les transitions).
        wasWalking = isWalking;
        wasRunning = isRunning;

        bool movePressed = moveInput.magnitude > 0.1f;
        bool runPressed = InputsManager.Instance.playerInputs.World.Run.IsPressed();
        bool dashTrigger = InputsManager.Instance.playerInputs.World.Dash.triggered;

        isRunning = movePressed && runPressed;
        isWalking = movePressed && !isRunning;

        dashTriggered = dashTrigger && !isRunning && !isWalking && !isJumping && !isDodging && !isHurt && !isFalling && !isSliding;

        // Mémorise le dernier appui sur Saut pour gérer intelligemment les buffers et le coyote time.
        if (InputsManager.Instance.playerInputs.World.Jump.triggered)
        {
            lastJumpPressedTimestamp = Time.time;
        }
    }

    /// <summary>
    /// Applique la bonne logique de déplacement selon le mode choisi.
    /// </summary>
    private void HandleMovement()
    {
        Vector2 moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();

        // Toujours appeler la logique de déplacement afin de gérer l'inertie
        Vector3 desiredMove;

        if (movementMode == MovementMode.FixedCamera)
        {
            desiredMove = HandleFixedCameraMovement(moveInput);
        }
        else // MovementMode.TPSOverShoulder
        {
            desiredMove = HandleTPSOverShoulderMovement(moveInput);
        }

        bool hasInput = desiredMove.sqrMagnitude > 0.001f;

        if (isSliding)
        {
            ApplySlopeSlide(desiredMove);
        }
        else
        {
            MoveWithInertia(desiredMove, hasInput);
        }

        TryConsumeBufferedJump();
    }

    /// <summary>
    /// Mouvement adapté aux caméras fixes.
    /// </summary>
    private Vector3 HandleFixedCameraMovement(Vector2 moveInput)
    {
        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // Vérifie le sol uniquement si un déplacement est demandé
        if (moveDir.sqrMagnitude > 0.001f && !IsGroundAhead(moveDir))
        {
            moveDir = Vector3.zero;
        }

        // Ajuste l'orientation uniquement lorsqu'un mouvement est réellement demandé
        if (moveDir.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, Time.deltaTime * rotationSpeed);
            transform.rotation = Quaternion.Euler(0, angle, 0);
        }

        return moveDir;
    }

    /// <summary>
    /// Mouvement en vue TPS épaulière avec auto alignement.
    /// </summary>
    private Vector3 HandleTPSOverShoulderMovement(Vector2 moveInput)
    {
        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;

        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
        if (moveDir.magnitude > 1f)
            moveDir.Normalize();

        bool hasInput = moveInput.magnitude > 0.1f;

        if (hasInput)
        {
            idleTimer = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
        }
        else
        {
            idleTimer += Time.deltaTime;

            if (enableAutoAlign && idleTimer >= autoAlignDelay)
            {
                Vector3 camFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                float targetAngle = Mathf.Atan2(camFwd.x, camFwd.z) * Mathf.Rad2Deg;
                float alignVelocity = 0f;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref alignVelocity, 1f / autoAlignSpeed);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
        }

        return moveDir;
    }

    /// <summary>
    /// Calcule et applique la vitesse avec inertie selon la direction souhaitée.
    /// </summary>
    /// <param name="moveDir">Direction de déplacement normalisée.</param>
    private void MoveWithInertia(Vector3 moveDir, bool hasInput)
    {
        // Détermine la vitesse cible en fonction de l'état courant
        float targetSpeed = isRunning ? runSpeed : moveSpeed;

        Vector3 effectiveDirection = moveDir;

        if (lastMoveDirection == Vector3.zero)
        {
            // Évite les artefacts lors du premier saut en fournissant une direction de référence.
            lastMoveDirection = transform.forward;
        }

        if (!isGrounded && moveDir.sqrMagnitude > 0.001f)
        {
            // Interpole la direction pour conserver un peu d'influence en l'air selon la configuration.
            effectiveDirection = Vector3.Slerp(lastMoveDirection, moveDir, airControlPercent);
            effectiveDirection.Normalize();
        }
        else if (!hasInput && currentSpeed > 0.01f)
        {
            // Recycle la dernière direction connue pour prolonger légèrement l'inertie au sol.
            Vector3 planarDirection = Vector3.ProjectOnPlane(lastMoveDirection, Vector3.up);
            if (planarDirection.sqrMagnitude > 0.001f)
            {
                effectiveDirection = planarDirection.normalized;
            }
        }

        if (hasInput)
        {
            // Accélère progressivement jusqu'à la vitesse cible
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Décélère lorsque l'entrée de mouvement s'arrête
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        controller.Move(effectiveDirection * currentSpeed * Time.deltaTime);

        if (effectiveDirection.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = effectiveDirection;
        }
    }

    private float turnSmoothVelocity;

    /// <summary>
    /// Vérifie s'il y a du sol devant pour éviter les chutes.
    /// </summary>
    private bool IsGroundAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float checkDistance = controller.radius + 0.1f;
        Vector3 checkPos = origin + direction * checkDistance;

        int layerMask = groundLayer == 0 ? Physics.DefaultRaycastLayers : groundLayer;

        if (Physics.Raycast(checkPos, Vector3.down, out RaycastHit hit, groundCheckDistance, layerMask))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gère la gravité et l'état de saut du personnage.
    /// </summary>
    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            isJumping = false;
            isFalling = false;
        }

        float gravityMultiplier = velocity.y > 0 ? jumpGravityMultiplier : fallGravityMultiplier;
        velocity.y += gravity * gravityMultiplier * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, terminalVelocity);
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Évalue l'état du sol, détecte les pentes et met à jour les indicateurs de glissade/chute.
    /// </summary>
    private void EvaluateGroundState()
    {
        isGrounded = controller.isGrounded;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
        float sphereRadius = controller.radius * 0.95f;
        // Le SphereCast évite les faux négatifs lors de légères dénivellations.
        int layerMask = groundLayer == 0 ? Physics.DefaultRaycastLayers : groundLayer;
        bool hitGround = Physics.SphereCast(rayOrigin, sphereRadius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, layerMask);

        if (hitGround)
        {
            groundNormal = hitInfo.normal;
            float slopeAngle = Vector3.Angle(hitInfo.normal, Vector3.up);
            bool tooSteep = slopeAngle > controller.slopeLimit;

            slopeSlideDirection = Vector3.ProjectOnPlane(Vector3.down, hitInfo.normal).normalized;
            isSliding = isGrounded && tooSteep;

            if (isGrounded)
            {
                lastGroundedTimestamp = Time.time;

                if (velocity.y < 0f)
                {
                    velocity.y = -2f; // Valeur légère pour coller le personnage au sol.
                }

                isJumping = false;
                isFalling = false;
            }
        }
        else
        {
            groundNormal = Vector3.up;
            slopeSlideDirection = Vector3.zero;
            isSliding = false;
        }

        if (!isGrounded && velocity.y < -0.1f)
        {
            isFalling = true;
        }
    }

    /// <summary>
    /// Consomme un saut mémorisé si les conditions le permettent (coyote time & jump buffer).
    /// </summary>
    private void TryConsumeBufferedJump()
    {
        bool recentlyGrounded = Time.time - lastGroundedTimestamp <= coyoteTime;
        bool jumpBuffered = Time.time - lastJumpPressedTimestamp <= jumpBufferTime;

        if (recentlyGrounded && jumpBuffered)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            isFalling = false;
            lastJumpPressedTimestamp = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// Applique une glissade contrôlée lorsque la pente dépasse la slopeLimit du CharacterController.
    /// </summary>
    /// <param name="desiredMove">Direction souhaitée par le joueur pour conserver un minimum d'influence.</param>
    private void ApplySlopeSlide(Vector3 desiredMove)
    {
        if (slopeSlideDirection == Vector3.zero)
        {
            slopeSlideDirection = Vector3.down;
        }

        Vector3 controlDirection = desiredMove.sqrMagnitude > 0.001f ? Vector3.ProjectOnPlane(desiredMove, groundNormal).normalized : Vector3.zero;
        Vector3 slideDirection = slopeSlideDirection;

        // Mélange la direction de glisse naturelle avec l'influence du joueur.
        Vector3 combinedDirection = (slideDirection + controlDirection * airControlPercent).normalized;

        currentSpeed = Mathf.MoveTowards(currentSpeed, slopeSlideSpeed, slopeSlideAcceleration * Time.deltaTime);
        controller.Move(combinedDirection * currentSpeed * Time.deltaTime);

        lastMoveDirection = combinedDirection;
    }

    #endregion
}
