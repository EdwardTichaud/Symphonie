using UnityEngine;

/// <summary>
/// Synchronise la position et la rotation du GameObject parent avec le Root Motion
/// calculé par un Animator enfant. À utiliser lorsque l'Animator n'est pas sur le
/// même GameObject que le Transform racine afin d'éviter que le parent reste en (0,0,0).
/// </summary>
public class RootMotionParentSync : MonoBehaviour
{
    [Tooltip("Animator possédant les animations en Root Motion.")]
    public Animator animator; // Référence vers l'Animator enfant.

    void Awake()
    {
        // Si aucune référence n'est assignée, on tente de récupérer l'Animator présent dans les enfants.
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // On force l'utilisation du Root Motion pour que les déplacements proviennent des animations.
        if (animator != null)
            animator.applyRootMotion = true;
    }

    void OnAnimatorMove()
    {
        if (animator == null)
            return; // Aucun Animator : rien à synchroniser.

        // Application du déplacement et de la rotation calculés par l'Animator sur le parent.
        transform.position += animator.deltaPosition;
        transform.rotation *= animator.deltaRotation;
    }
}

