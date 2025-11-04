using UnityEngine;

/// <summary>
/// Active ApplyRootMotion à l'entrée du state, le coupe en sortie.
/// Tu peux cocher "ReturnToIdleOnExit" pour forcer un retour Idle après le state.
/// </summary>
public class ApplyRootMotionToggle : StateMachineBehaviour
{
    [Tooltip("Revenir à Idle en sortie du state (crossfade court)")]
    public bool ReturnToIdleOnExit = false;

    [Tooltip("Nom du state Idle (si ReturnToIdleOnExit)")]
    public string IdleStateName = "Idle_BT";

    [Tooltip("Durée du crossfade retour Idle")]
    public float IdleXFade = 0.08f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.applyRootMotion = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.applyRootMotion = false;
        if (ReturnToIdleOnExit && !string.IsNullOrEmpty(IdleStateName))
        {
            animator.CrossFade(IdleStateName, IdleXFade, layerIndex);
        }
    }
}
