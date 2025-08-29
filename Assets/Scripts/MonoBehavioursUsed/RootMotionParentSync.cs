using UnityEngine;

/// <summary>
/// Synchronise la position et la rotation du GameObject parent avec le Root Motion
/// calculé par l'Animator présent sur le même GameObject. Ce composant est pensé
/// pour être placé sur l'Animator lui‑même (ex. : <c>Lucian_Battle</c>) afin de
/// déplacer son parent dans la scène.
/// </summary>
public class RootMotionParentSync : MonoBehaviour
{
    [Tooltip("Animator possédant les animations en Root Motion.")]
    public Animator animator; // Référence vers l'Animator local.

    // Transform du parent à déplacer via le Root Motion.
    private Transform parentTransform;

    void Awake()
    {
        // Récupération de l'Animator sur le même GameObject si non assigné.
        if (animator == null)
            animator = GetComponent<Animator>();

        // Sauvegarde du Transform parent pour y appliquer les déplacements.
        parentTransform = transform.parent;
        if (parentTransform == null)
            Debug.LogWarning("RootMotionParentSync : aucun parent trouvé, le Root Motion ne sera pas appliqué.", this);

        // On force l'utilisation du Root Motion pour que les déplacements proviennent des animations.
        if (animator != null)
            animator.applyRootMotion = true;
    }

    void OnAnimatorMove()
    {
        // Sans Animator ou sans parent, aucune synchronisation possible.
        if (animator == null || parentTransform == null)
            return;

        // Application du déplacement calculé par l'Animator sur le Transform parent.
        parentTransform.position += animator.deltaPosition;

        // Application de la rotation calculée par l'Animator sur le Transform parent.
        parentTransform.rotation *= animator.deltaRotation;

        // Réinitialisation de la position locale pour éviter que l'Animator retourne à l'origine.
        transform.localPosition = Vector3.zero;

        // Réinitialisation de la rotation locale pour empêcher tout décalage visuel.
        transform.localRotation = Quaternion.identity;
    }
}

