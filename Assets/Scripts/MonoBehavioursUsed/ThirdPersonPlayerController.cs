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
    private Vector3 horizontalVelocity;     // Vitesse horizontale conservée en l'air pour éviter les changements brusques.

    // Verrouillage utilisé lorsque l'animation d'atterrissage est en cours pour éviter toute interruption.
    private bool landingAnimationLocked;

    /// <summary>
    /// Énumération interne utilisée pour suivre l'état courant du mouvement afin
    /// de déclencher l'animation adéquate sans recourir à des paramètres de l'Animator.
    /// </summary>
    private enum MovementAnimation
    {
        Idle,  // Lucian est immobile.
        Walk,  // Lucian marche.
        Run    // Lucian court.
    }

    // État d'animation actuel. Il permet d'éviter de relancer la même animation en boucle.
    private MovementAnimation currentAnimState = MovementAnimation.Idle;

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
    [Tooltip("Multiplicateur appliqué à la vitesse initiale du saut pour simuler une impulsion.")]
    public float jumpBoost = 1.1f;
    [Tooltip("Multiplicateur de gravité durant la montée pour une courbe de saut moins linéaire.")]
    public float ascentGravityMultiplier = 1f;
    [Tooltip("Multiplicateur de gravité durant la descente pour accentuer l'inertie.")]
    public float fallGravityMultiplier = 2f;
    [Tooltip("Force dirigée vers le bas appliquée à l'atterrissage pour renforcer l'impact.")]
    public float landingForce = 5f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>(); // Récupération de l'Animator pour jouer les animations.
        // On s'assure que l'animation d'idle est jouée dès le début pour éviter un personnage figé.
        if (animator != null)
        {
            animator.Play("Idle_World");
        }
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

    /// <summary>
    /// Gère le lancement des animations de marche/course/idle en fonction des actions du joueur.
    /// Cette méthode s'appuie uniquement sur <see cref="Animator.Play(string)"/> afin d'éviter
    /// l'utilisation de paramètres ou de triggers dans l'Animator, comme demandé.
    /// </summary>
    void UpdateMovementAnimation()
    {
        // Si aucun Animator n'est présent, si le personnage est en plein saut ou si
        // l'animation d'atterrissage est encore en cours, on ne touche pas aux
        // animations de locomotion pour éviter tout conflit.
        if (animator == null || isJumping || landingAnimationLocked)
            return;

        MovementAnimation targetState;

        // Détermination de l'état cible en fonction des entrées utilisateur.
        if (isRunning)
        {
            targetState = MovementAnimation.Run;
        }
        else if (isWalking)
        {
            targetState = MovementAnimation.Walk;
        }
        else
        {
            targetState = MovementAnimation.Idle;
        }

        // Si l'état ne change pas, il est inutile de relancer l'animation.
        if (targetState == currentAnimState)
            return;

        // Selon l'état détecté, on joue l'animation appropriée.
        switch (targetState)
        {
            case MovementAnimation.Run:
                // Début d'une course : animation d'accélération.
                animator.Play("Run Start");
                break;

            case MovementAnimation.Walk:
                // Début d'une marche : animation de mise en mouvement.
                animator.Play("Walk_Start");
                break;

            case MovementAnimation.Idle:
                // Retour à l'immobilité. On choisit l'animation de "stop" adéquate
                // en fonction de l'état précédent pour conserver la cohérence visuelle.
                if (currentAnimState == MovementAnimation.Run)
                {
                    animator.Play("Run Stop");
                }
                else if (currentAnimState == MovementAnimation.Walk)
                {
                    animator.Play("Walk_Stop");
                }
                else
                {
                    // Si Lucian était déjà à l'arrêt, on force l'idle afin de garantir
                    // que l'animation par défaut est bien jouée.
                    animator.Play("Idle_World");
                }
                break;
        }

        // Mise à jour de l'état courant pour les prochains appels.
        currentAnimState = targetState;
    }

    void Update()
    {
        HandleMovement();           // Gestion du déplacement horizontal et des rotations.
        ApplyGravity();             // Application de la gravité (déplacement vertical).
        UpdateLandingLock();        // Vérifie si l'animation d'atterrissage est terminée.
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

        // Conversion de l'entrée en vecteur 3D relatif à l'orientation de la caméra (Munin).
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 desiredMove = camForward * input.y + camRight * input.x; // Direction désirée par le joueur.

        // Si le personnage touche le sol, on peut mettre à jour la vitesse horizontale.
        if (controller.isGrounded)
        {
            // Mise à jour des états de déplacement pour les animations.
            isRunning = input.sqrMagnitude > 0.01f && runPressed;
            isWalking = input.sqrMagnitude > 0.01f && !runPressed;

            // Vitesse selon marche/course.
            float speed = isRunning ? runSpeed : walkSpeed;
            horizontalVelocity = desiredMove * speed; // Stockage de la vitesse pour conserver la direction en l'air.
        }

        // Déplacement horizontal : si Lucian est en l'air, la direction reste
        // celle enregistrée au moment du saut, évitant toute correction mid-air.
        controller.Move(horizontalVelocity * Time.deltaTime);

        // Le joueur peut toujours pivoter en l'air : on oriente donc Lucian selon
        // la direction actuellement demandée par le joueur, même si le mouvement
        // effectif reste figé.
        if (desiredMove.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }

        // Gestion du saut avec une petite impulsion supplémentaire pour éviter un départ trop linéaire.
        if (InputsManager.Instance.playerInputs.World.Jump.triggered && controller.isGrounded && !landingAnimationLocked)
        {
            // Calcul de la vitesse initiale nécessaire pour atteindre la hauteur voulue.
            float baseJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // On applique un léger boost pour simuler l'élan du héros au moment où il quitte le sol.
            velocity.y = baseJumpVelocity * jumpBoost;
            isJumping = true; // Le saut débute.

            // Déclenche l'animation d'anticipation du saut pour rester cohérent avec l'histoire.
            if (animator != null)
            {
                animator.Play("Jump_Start");
            }
        }

        // À la fin de la gestion du déplacement, on met à jour l'animation de locomotion
        // pour qu'elle corresponde à l'état courant (marche, course ou idle).
        UpdateMovementAnimation();
    }

    /// <summary>
    /// Applique une gravité non linéaire sur le CharacterController afin de simuler l'inertie.
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            // Si on revient au sol après un saut, on déclenche l'animation de landing appropriée
            // et on applique une légère impulsion négative pour ressentir l'impact.
            if (isJumping && animator != null)
            {
                // Déterminer si Lucian bouge réellement lors de l'impact en se basant
                // sur la vitesse horizontale conservée durant le saut.
                bool isMoving = horizontalVelocity.sqrMagnitude > 0.01f;

                if (isMoving)
                {
                    animator.Play("Landing_OnMove");
                }
                else
                {
                    animator.Play("Landing");
                }

                // Impulsion supplémentaire vers le bas pour simuler l'écrasement.
                velocity.y = -landingForce;
                isJumping = false;           // Retour au sol : le saut est terminé.
                landingAnimationLocked = true; // Empêche les autres animations de remplacer le landing.
            }
            else
            {
                // Petite valeur négative pour ancrer le personnage au sol.
                velocity.y = -2f;
            }
        }
        else
        {
            // Choix du multiplicateur de gravité selon que l'on monte ou que l'on descend.
            float gravityMultiplier = velocity.y > 0f ? ascentGravityMultiplier : fallGravityMultiplier;
            velocity.y += gravity * gravityMultiplier * Time.deltaTime;
        }

        // Application de la vitesse calculée.
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Vérifie l'état de l'animation de landing afin de lever le verrou une fois terminée.
    /// </summary>
    void UpdateLandingLock()
    {
        if (!landingAnimationLocked || animator == null)
            return;

        // Récupère les informations de l'état courant de l'Animator.
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        // Dès que l'animation d'atterrissage (avec ou sans déplacement) est terminée
        // on relâche le verrou pour permettre les animations suivantes.
        if ((state.IsName("Landing") || state.IsName("Landing_OnMove")) && state.normalizedTime >= 1f)
        {
            landingAnimationLocked = false;
        }
    }
}

