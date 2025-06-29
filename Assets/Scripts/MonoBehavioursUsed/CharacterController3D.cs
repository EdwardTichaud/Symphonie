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

    public MovementMode movementMode = MovementMode.FixedCamera;

    [HideInInspector] public CharacterController controller;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float rotationSpeed = 10f;

    [Header("Fall Prevention")]
    public float groundCheckDistance = 1f; // distance to check for ground
    public LayerMask groundLayer;

    private Vector3 velocity;
    private bool jumpRequested = false;

    [Header("Debug Movement State")]
    public bool isGrounded;
    public bool isWalking;
    public bool wasWalking;
    public bool isRunning;
    public bool wasRunning;
    public bool isJumping;
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
    }

    /// <summary>
    /// Gère les entrées et le mouvement à chaque frame.
    /// </summary>
    void Update()
    {
        if (!controller.enabled) return;
        isGrounded = controller.isGrounded;

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
        bool movePressed = moveInput.magnitude > 0.1f;
        bool runPressed = InputsManager.Instance.playerInputs.World.Run.IsPressed();
        bool dashTrigger = InputsManager.Instance.playerInputs.World.Dash.triggered;

        isRunning = movePressed && runPressed;
        isWalking = movePressed && !isRunning;
        dashTriggered = dashTrigger && !isRunning && !isWalking && !isJumping && !isDodging && !isHurt;

        wasWalking = isWalking;
        wasRunning = isRunning;

        if (InputsManager.Instance.playerInputs.World.Jump.triggered && controller.isGrounded)
            jumpRequested = true;
    }

    /// <summary>
    /// Applique la bonne logique de déplacement selon le mode choisi.
    /// </summary>
    private void HandleMovement()
    {
        Vector2 moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        if (moveInput.magnitude > 0.1f)
        {
            if (movementMode == MovementMode.FixedCamera)
            {
                HandleFixedCameraMovement(moveInput);
            }
            else if (movementMode == MovementMode.TPSOverShoulder)
            {
                HandleTPSOverShoulderMovement(moveInput);
            }
        }

        if (jumpRequested)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            jumpRequested = false;
        }
    }

    /// <summary>
    /// Mouvement adapté aux caméras fixes.
    /// </summary>
    private void HandleFixedCameraMovement(Vector2 moveInput)
    {
        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        if (!IsGroundAhead(moveDir))
        {
            moveDir = Vector3.zero;
        }

        if (moveDir.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, Time.deltaTime * rotationSpeed);
            transform.rotation = Quaternion.Euler(0, angle, 0);
        }

        float speed = isRunning ? runSpeed : moveSpeed;
        controller.Move(moveDir * speed * Time.deltaTime);
    }

    /// <summary>
    /// Mouvement en vue TPS épaulière avec auto alignement.
    /// </summary>
    private void HandleTPSOverShoulderMovement(Vector2 moveInput)
    {
        Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        bool hasInput = moveInput.magnitude > 0.1f;

        if (hasInput)
        {
            idleTimer = 0f;

            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float currentY = transform.eulerAngles.y;
            float angleDiff = Mathf.DeltaAngle(currentY, targetAngle);

            if (Mathf.Abs(moveInput.x) > stickDeadZoneX && Mathf.Abs(angleDiff) > angleThreshold)
            {
                float angle = Mathf.SmoothDampAngle(currentY, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }

            // Corriger moveDir pour matcher l'orientation
            moveDir = transform.forward;
        }
        else
        {
            idleTimer += Time.deltaTime;

            if (enableAutoAlign && idleTimer >= autoAlignDelay)
            {
                Vector3 camFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                float targetAngle = Mathf.Atan2(camFwd.x, camFwd.z) * Mathf.Rad2Deg;
                float currentY = transform.eulerAngles.y;

                float alignVelocity = 0f;
                float angle = Mathf.SmoothDampAngle(currentY, targetAngle, ref alignVelocity, 1f / autoAlignSpeed);

                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
        }

        float speed = isRunning ? runSpeed : moveSpeed;
        controller.Move(moveDir * speed * Time.deltaTime);
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

        if (Physics.Raycast(checkPos, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
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
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    #endregion
}
