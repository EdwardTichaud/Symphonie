using UnityEngine;

public class AnimationHandler : MonoBehaviour
{
    private Animator animator;
    private CharacterController3D controller;

    [Header("Préfabs")]
    public GameObject dashTrailprefab;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponentInChildren<CharacterController3D>();
    }

    void Update()
    {
        animator.SetBool("isGrounded", controller.isGrounded);
        animator.SetBool("isWalking", controller.isWalking);
        animator.SetBool("isRunning", controller.isRunning);
        animator.SetBool("isJumping", controller.isJumping);
    }
}
