using UnityEngine;

/// <summary>
/// Gère l'orientation de la tête de Lucian vers une cible.
/// L'orientation est appliquée via OnAnimatorIK afin de primer sur les animations.
/// Le suivi n'est actif que lorsqu'il est explicitement activé via les méthodes publiques.
/// </summary>
[RequireComponent(typeof(Animator))]
public class LucianHeadFollower : MonoBehaviour
{
    [Header("Réglages")]
    [Tooltip("Poids appliqué au regard de Lucian (0 = pas de suivi, 1 = suivi complet)")]
    [Range(0f,1f)]
    public float lookAtWeight = 1f; // Poids du suivi de tête

    private Animator animator;          // Référence à l'Animator contrôlant Lucian
    private Transform target;           // Cible à suivre
    private bool isTracking = false;    // Indique si le suivi est actif

    void Awake()
    {
        // Récupère l'Animator sur le même GameObject
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Active le suivi de tête vers la cible passée en paramètre.
    /// </summary>
    /// <param name="newTarget">Transform de l'objet que Lucian doit regarder.</param>
    public void EnableHeadTracking(Transform newTarget)
    {
        target = newTarget;
        isTracking = target != null;
    }

    /// <summary>
    /// Désactive le suivi de tête et rend l'animation maître de l'orientation.
    /// </summary>
    public void DisableHeadTracking()
    {
        isTracking = false;
        target = null;
    }

    /// <summary>
    /// Méthode appelée après l'évaluation des animations.
    /// Permet de surcharger l'orientation de la tête pour suivre la cible.
    /// </summary>
    /// <param name="layerIndex">Index de la couche d'animation (non utilisé).</param>
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return; // Sécurité au cas où l'Animator n'est pas trouvé

        if (isTracking && target != null)
        {
            // Définit le poids du regard et la position de la cible
            animator.SetLookAtWeight(lookAtWeight); // Poids global
            animator.SetLookAtPosition(target.position); // Position à suivre
        }
        else
        {
            // Lorsque le suivi est désactivé, remettre le poids à zéro
            animator.SetLookAtWeight(0f);
        }
    }
}
