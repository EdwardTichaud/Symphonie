using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterController3D : MonoBehaviour
{
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

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!controller.enabled) return;
        isGrounded = controller.isGrounded;

        ReadInputs();
        HandleMovement();
        ApplyGravity();
    }

    private void ReadInputs()
    {
        Vector2 moveInput = InputsManager.Instance.playerInputs.Player.Move.ReadValue<Vector2>();
        bool movePressed = moveInput.magnitude > 0.1f;
        bool runPressed = InputsManager.Instance.playerInputs.Player.Run.IsPressed();
        bool dashTrigger = InputsManager.Instance.playerInputs.Player.Dash.triggered;

        isRunning = movePressed && runPressed;
        isWalking = movePressed && !isRunning;
        dashTriggered = dashTrigger && !isRunning && !isWalking && !isJumping && !isDodging && !isHurt;

        wasWalking = isWalking;
        wasRunning = isRunning;

        if (InputsManager.Instance.playerInputs.Player.Jump.triggered && controller.isGrounded)
            jumpRequested = true;
    }

    private void HandleMovement()
    {
        Vector2 moveInput = InputsManager.Instance.playerInputs.Player.Move.ReadValue<Vector2>();
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;

            Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

            // Vérifier si le sol existe devant le joueur
            if (!IsGroundAhead(moveDir))
            {
                // Pas de sol, on arrête le déplacement
                moveDir = Vector3.zero;
            }

            // Rotation du personnage
            if (moveDir.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, Time.deltaTime * rotationSpeed);
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }

            float speed = isRunning ? runSpeed : moveSpeed;
            controller.Move(moveDir * speed * Time.deltaTime);
        }

        if (jumpRequested)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            jumpRequested = false;
        }
    }

    private bool IsGroundAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float checkDistance = controller.radius + 0.1f;
        Vector3 checkPos = origin + direction * checkDistance;

        // Raycast vers le bas
        if (Physics.Raycast(checkPos, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            return true;
        }
        return false;
    }

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
}
