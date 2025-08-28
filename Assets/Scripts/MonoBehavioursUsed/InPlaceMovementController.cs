using UnityEngine;

/// <summary>
/// Exemple de contrôleur de déplacement utilisant la méthode "In-Place + code" pour Lucian lors de son exploration du monde onirique d'Azazel.
/// Les animations restent sur place et c'est ce script qui déplace le personnage
/// via un <see cref="CharacterController"/>. L'Animator n'est utilisé que pour
/// refléter visuellement la vitesse en ajustant un paramètre "Speed".
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class InPlaceMovementController : MonoBehaviour
{
    [Header("Réglages de déplacement")]
    [Tooltip("Vitesse de déplacement en unités par seconde.")]
    public float speed = 4f;

    private CharacterController controller; // Référence au CharacterController pour déplacer l'objet.
    private Animator animator;              // Référence à l'Animator pour les animations de locomotion.

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        // S'assure que la root motion n'est pas appliquée puisque nous déplaçons
        // le GameObject par code.
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    void Update()
    {
        // Récupère l'entrée utilisateur (axes horizontaux et verticaux). Cette
        // approche fonctionne aussi bien au clavier qu'au gamepad par défaut.
        Vector3 inputDir = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        // Déplacement effectif : on normalise le vecteur pour éviter d'accélérer
        // en diagonale puis on le multiplie par la vitesse et le deltaTime.
        Vector3 movement = inputDir.normalized * speed * Time.deltaTime;
        controller.Move(movement);

        // Synchronise l'Animator avec la magnitude de l'entrée pour choisir la
        // bonne animation (Idle/Walk/Run suivant la configuration du controller).
        if (animator != null)
        {
            animator.SetFloat("Speed", inputDir.magnitude);
        }
    }
}
