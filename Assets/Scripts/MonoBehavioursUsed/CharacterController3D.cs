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

    [Header("Battle Camera Manual Control")]
    [Tooltip("Vitesse de déplacement appliquée au BattleCamera_Origin lorsque le joueur prend le contrôle manuel.")]
    [SerializeField] private float battleCameraMoveSpeed = 8f;
    [Tooltip("Nom du GameObject représentant l'origine du rig de caméra de combat.")]
    [SerializeField] private string battleCameraOriginName = "BattleCamera_Origin";

    private Transform battleCameraOrigin;          // Référence au BattleCamera_Origin dans la scène courante
    private bool manualBattleCameraControl;        // Indique si les entrées pilotent actuellement la caméra au lieu du joueur
    private InputAction switchLucianToCameraAction; // Action d'entrée dédiée pour basculer entre Lucian et la caméra
    private bool switchActionWarningLogged;        // Évite de spammer la console si l'action n'existe pas dans l'asset

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

        TryRegisterSwitchAction();
    }

    void OnEnable()
    {
        // Lors du rechargement d'une scène ou après une désactivation temporaire, on s'assure
        // que l'action d'entrée est bien reliée afin que le joueur puisse reprendre la main.
        TryRegisterSwitchAction();
    }

    void OnDisable()
    {
        if (switchLucianToCameraAction != null)
        {
            switchLucianToCameraAction.performed -= OnSwitchLucianToCamera;
            switchLucianToCameraAction = null;
        }

        // On réinitialise le mode de contrôle manuel afin d'éviter qu'il reste actif après un changement de scène.
        manualBattleCameraControl = false;
        switchActionWarningLogged = false;
    }

    /// <summary>
    /// Gère les entrées et le mouvement à chaque frame.
    /// </summary>
    void Update()
    {
        if (!controller.enabled) return;
        isGrounded = controller.isGrounded;

        // Tant que l'action SwitchLucianToCamera n'a pas été trouvée (par exemple au chargement d'une scène),
        // on retente discrètement le câblage. Cela évite d'avoir à redémarrer le jeu si le InputsManager est créé après nous.
        if (switchLucianToCameraAction == null)
        {
            TryRegisterSwitchAction();
        }

        if (manualBattleCameraControl)
        {
            // Lorsque la caméra est pilotée manuellement, on neutralise les états de déplacement
            // pour éviter toute animation résiduelle côté personnage.
            isWalking = false;
            isRunning = false;
            wasWalking = false;
            wasRunning = false;
            dashTriggered = false;
            jumpRequested = false;
            velocity = Vector3.zero; // Empêche la gravité de faire glisser Lucian pendant que la caméra est contrôlée.

            HandleManualBattleCameraMovement();
            return;
        }

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

        // Toujours appeler la logique de déplacement afin de gérer l'inertie
        if (movementMode == MovementMode.FixedCamera)
        {
            HandleFixedCameraMovement(moveInput);
        }
        else if (movementMode == MovementMode.TPSOverShoulder)
        {
            HandleTPSOverShoulderMovement(moveInput);
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

        MoveWithInertia(moveDir);
    }

    /// <summary>
    /// Mouvement en vue TPS épaulière avec auto alignement.
    /// </summary>
    private void HandleTPSOverShoulderMovement(Vector2 moveInput)
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

        // Applique le déplacement avec inertie, même lorsqu'il n'y a plus d'entrée
        MoveWithInertia(moveDir);
    }

    /// <summary>
    /// Calcule et applique la vitesse avec inertie selon la direction souhaitée.
    /// </summary>
    /// <param name="moveDir">Direction de déplacement normalisée.</param>
    private void MoveWithInertia(Vector3 moveDir)
    {
        // Détermine la vitesse cible en fonction de l'état courant
        float targetSpeed = isRunning ? runSpeed : moveSpeed;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            // Accélère progressivement jusqu'à la vitesse cible
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Décélère lorsque l'entrée de mouvement s'arrête
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        controller.Move(moveDir * currentSpeed * Time.deltaTime);
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

        if (groundLayer == 0 || Physics.Raycast(checkPos, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
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

    #region Contrôle Manuel de la BattleCamera

    /// <summary>
    /// Recherche et abonne dynamiquement l'action d'entrée <c>SwitchLucianToCamera</c>.
    /// </summary>
    private void TryRegisterSwitchAction()
    {
        if (switchLucianToCameraAction != null || InputsManager.Instance == null || InputsManager.Instance.playerInputs == null)
            return; // Rien à faire si déjà branché ou si l'InputsManager n'est pas prêt.

        var asset = InputsManager.Instance.playerInputs.asset;
        if (asset == null)
            return;

        var action = asset.FindAction("SwitchLucianToCamera", throwIfNotFound: false);

        if (action != null)
        {
            switchLucianToCameraAction = action;
            switchLucianToCameraAction.performed += OnSwitchLucianToCamera;
            switchActionWarningLogged = false; // On réinitialise le flag pour autoriser un nouveau warning si l'action disparaît plus tard.
        }
        else if (!switchActionWarningLogged)
        {
            Debug.LogWarning("[CharacterController3D] Action 'SwitchLucianToCamera' introuvable dans PlayerInputs. Le contrôle manuel de la BattleCamera restera désactivé tant qu'elle ne sera pas définie.");
            switchActionWarningLogged = true;
        }
    }

    /// <summary>
    /// Callback invoqué lors de l'appui sur l'action <c>SwitchLucianToCamera</c>.
    /// </summary>
    private void OnSwitchLucianToCamera(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return; // On attend la validation de l'entrée pour éviter les toggles parasites.

        manualBattleCameraControl = !manualBattleCameraControl;

        if (manualBattleCameraControl)
        {
            // Lors de l'activation on essaie immédiatement de récupérer la référence du rig de caméra.
            if (!EnsureBattleCameraOrigin())
            {
                Debug.LogWarning("[CharacterController3D] BattleCamera_Origin introuvable : retour automatique au contrôle de Lucian.");
                manualBattleCameraControl = false;
            }
            else
            {
                // On s'assure que Lucian reste immobile pour éviter toute glissade pendant l'utilisation libre de la caméra.
                controller.Move(Vector3.zero);
                velocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Déplace librement le <c>BattleCamera_Origin</c> à l'aide des entrées de déplacement usuelles.
    /// </summary>
    private void HandleManualBattleCameraMovement()
    {
        if (!EnsureBattleCameraOrigin() || InputsManager.Instance == null)
            return;

        // Projection des axes selon l'orientation de la caméra active afin que W/S avance et recule dans la direction visée.
        Transform reference = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        Vector2 moveInput = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        Vector3 displacement = (forward * moveInput.y + right * moveInput.x);

        // Les touches de saut/dash contrôlent l'altitude lors du déplacement manuel pour véritablement naviguer "dans tous les sens".
        float vertical = 0f;
        if (InputsManager.Instance.playerInputs.World.Jump.IsPressed())
            vertical += 1f;
        if (InputsManager.Instance.playerInputs.World.Dash.IsPressed())
            vertical -= 1f;
        displacement += Vector3.up * vertical;

        if (displacement.sqrMagnitude > 0.001f)
        {
            battleCameraOrigin.position += displacement.normalized * battleCameraMoveSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Tente de retrouver la référence du BattleCamera_Origin s'il est absent ou a été recréé.
    /// </summary>
    private bool EnsureBattleCameraOrigin()
    {
        if (battleCameraOrigin != null)
            return true;

        GameObject origin = GameObject.Find(battleCameraOriginName);
        if (origin != null)
        {
            battleCameraOrigin = origin.transform;
            return true;
        }

        return false;
    }

    #endregion
}
