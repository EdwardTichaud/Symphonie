using UnityEngine;

/// <summary>
/// Contrôleur simple à la troisième personne pour Lucian en mode exploration.
/// Gère le déplacement de base (marche/course) et l'orientation du personnage.
/// La caméra (Munin) peut pivoter autour de Lucian grâce au script <see cref="ThirdPersonCameraController"/>.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Transform de la WorldCamera utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    private CharacterController controller; // Référence au CharacterController Unity.
    private Animator animator;              // Référence à l'Animator pour déclencher les animations.
    private Vector3 velocity;               // Vecteur de vitesse utilisé pour la gravité et le saut.

    // États exposés pour l'AnimationHandler afin de synchroniser les animations.
    [HideInInspector] public bool isWalking;   // Indique si Lucian marche.
    [HideInInspector] public bool isRunning;   // Indique si Lucian court.
    [HideInInspector] public bool isJumping;   // Indique si Lucian effectue un saut.

    /// <summary>
    /// Indique si le personnage touche le sol.
    /// </summary>
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

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>(); // Récupération de l'Animator pour jouer les triggers.
        // Si aucune caméra n'est assignée, on cherche d'abord la WorldCamera, puis on retombe sur la MainCamera.
        if (cameraTransform == null)
        {
            Camera worldCam = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
            if (worldCam != null)
            {
                cameraTransform = worldCam.transform;
            }
            else if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("[ThirdPersonPlayerController] Aucune WorldCamera ou MainCamera trouvée pour orienter le déplacement.");
            }
        }
    }

    void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    /// <summary>
    /// Lit les entrées et déplace Lucian dans le monde.
    /// </summary>
    void HandleMovement()
    {
        if (cameraTransform == null)
            return;
        // Lecture des axes de déplacement (WASD / stick gauche).
        Vector2 input = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        bool runPressed = InputsManager.Instance.playerInputs.World.Run.IsPressed();

        // Mise à jour des états de déplacement pour les animations.
        isRunning = input.sqrMagnitude > 0.01f && runPressed;
        isWalking = input.sqrMagnitude > 0.01f && !runPressed;

        // Conversion de l'entrée en vecteur 3D relatif à l'orientation de la caméra (Munin).
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 move = camForward * input.y + camRight * input.x;

        // Vitesse selon marche/course.
        float speed = isRunning ? runSpeed : walkSpeed;
        controller.Move(move * speed * Time.deltaTime);

        // Oriente le personnage dans la direction du mouvement pour rester cohérent avec l'histoire (Lucian fait face à son chemin).
        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }

        // Gestion du saut simple.
        if (InputsManager.Instance.playerInputs.World.Jump.triggered && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true; // Le saut débute.

            // Déclenche l'animation d'anticipation du saut.
            if (animator != null)
            {
                animator.Play("Jump_Start");
            }
        }
    }

    /// <summary>
    /// Applique une gravité simple sur le CharacterController.
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            // Si on revient au sol après un saut, on déclenche l'animation de landing appropriée.
            if (isJumping && animator != null)
            {
                // Déterminer si Lucian bouge lors de l'impact.
                bool isMoving = isWalking || isRunning;

                if (isMoving)
                {
                    animator.Play("Landing_OnMove");
                }
                else
                {
                    animator.Play("Landing");
                }                    
            }

            // Petite valeur négative pour ancrer le personnage au sol.
            velocity.y = -2f;
            isJumping = false; // Retour au sol : le saut est terminé.
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}

