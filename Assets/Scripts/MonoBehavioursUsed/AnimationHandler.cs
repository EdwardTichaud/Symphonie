using UnityEngine;

[RequireComponent(typeof(ThirdPersonPlayerController))]
public class AnimationHandler : MonoBehaviour
{
    private Animator animator;                         // Référence à l'Animator de Lucian.
    private ThirdPersonPlayerController controller;    // Nouveau contrôleur de mouvement.

    // Variables internes permettant de détecter les changements d'état
    // entre deux frames pour déclencher les animations de démarrage/arrêt.
    private bool wasWalking;                           // Indique si Lucian marchait au frame précédent.
    private bool wasRunning;                           // Indique si Lucian courait au frame précédent.

    [Header("Préfabs")]
    public GameObject dashTrailprefab;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponentInChildren<ThirdPersonPlayerController>();

        // Initialisation des états précédents pour éviter les déclenchements
        // d'animations au chargement de la scène.
        wasWalking = false;
        wasRunning = false;
    }

    void Update()
    {
        // Synchronise l'Animator avec l'état actuel du contrôleur de joueur.
        animator.SetBool("isGrounded", controller.isGrounded); // Au sol ou non.
        animator.SetBool("isWalking", controller.isWalking);   // Marche en cours ?
        animator.SetBool("isRunning", controller.isRunning);   // Course en cours ?
        animator.SetBool("isJumping", controller.isJumping);   // Saut en cours ?

        // ----- Détection des transitions de marche -----
        if (controller.isWalking && !wasWalking)
        {
            // Le joueur commence à marcher : on lance l'animation de départ.
            animator.SetTrigger("Walk_Start");
        }
        else if (!controller.isWalking && wasWalking)
        {
            // Le joueur s'arrête de marcher : on joue l'animation d'arrêt.
            animator.SetTrigger("Walk_Stop");
        }

        // ----- Détection des transitions de course -----
        if (controller.isRunning && !wasRunning)
        {
            // Début de la course.
            animator.SetTrigger("Run_Start");
        }
        else if (!controller.isRunning && wasRunning)
        {
            // Fin de la course.
            animator.SetTrigger("Run_Stop");
        }

        // Mise à jour des états précédents pour la frame suivante.
        wasWalking = controller.isWalking;
        wasRunning = controller.isRunning;
    }
}
