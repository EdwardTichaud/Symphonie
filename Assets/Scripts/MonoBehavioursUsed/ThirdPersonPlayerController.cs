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
    [Tooltip("Transform de la caméra utilisée pour orienter le déplacement.")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;

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
        // Si aucune caméra n'est assignée, on prend la caméra principale.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
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
        // Lecture des axes de déplacement (WASD / stick gauche).
        Vector2 input = InputsManager.Instance.playerInputs.World.Move.ReadValue<Vector2>();
        bool isRunning = InputsManager.Instance.playerInputs.World.Run.IsPressed();

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
        }
    }

    /// <summary>
    /// Applique une gravité simple sur le CharacterController.
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            // Petite valeur négative pour ancrer le personnage au sol.
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}

