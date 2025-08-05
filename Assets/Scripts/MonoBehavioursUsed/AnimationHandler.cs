using UnityEngine;

[RequireComponent(typeof(ThirdPersonPlayerController))]
public class AnimationHandler : MonoBehaviour
{
    private Animator animator;                         // Référence à l'Animator de Lucian.
    private ThirdPersonPlayerController controller;    // Nouveau contrôleur de mouvement.

    [Header("Préfabs")]
    public GameObject dashTrailprefab;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponentInChildren<ThirdPersonPlayerController>();
    }

    void Update()
    {
        // Synchronise l'Animator avec l'état du contrôleur de joueur.
        animator.SetBool("isGrounded", controller.isGrounded);
        animator.SetBool("isWalking", controller.isWalking);
        animator.SetBool("isRunning", controller.isRunning);
        animator.SetBool("isJumping", controller.isJumping);
    }
}
