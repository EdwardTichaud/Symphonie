using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Entrée référencée par l'<see cref="AnimationSignalReceiver"/> pour
/// associer un identifiant à un <see cref="Animator"/> spécifique.
/// </summary>
[System.Serializable]
public class AnimatorEntry
{
    [Tooltip("ID unique pour identifier cet Animator (ex: Lucian, Door01, EnemyA)")]
    public string id;

    [Tooltip("Référence de l'Animator dans la scène")]
    public Animator animator;
}

/// <summary>
/// Récepteur d'animation déclenché par les Signaux de la Timeline.
/// L'attribut <see cref="ExecuteAlways"/> garantit son exécution même
/// hors Play Mode, permettant de tester visuellement les animations
/// depuis l'Éditeur.
/// </summary>
[ExecuteAlways]
public class AnimationSignalReceiver : MonoBehaviour
{
    [Header("List of Animators available in this scene")]
    public List<AnimatorEntry> animators = new List<AnimatorEntry>();

    public void TriggerAnimation(AnimationTriggerSO trigger)
    {
        // Méthode invoquée par la Timeline, y compris lors de la prévisualisation
        // en mode Éditeur. On vérifie systématiquement les entrées pour éviter
        // toute erreur pendant l'édition.
        if (trigger == null)
        {
            Debug.LogWarning("[AnimationSignalReceiver] Received null AnimationTriggerSO.");
            return;
        }

        AnimatorEntry entry = animators.Find(e => e.id == trigger.animatorID);

        if (entry == null || entry.animator == null)
        {
            Debug.LogWarning($"[AnimationSignalReceiver] No Animator found for ID: '{trigger.animatorID}'.");
            return;
        }

        // Empêcher de jouer une animation si l'unité possède une CharacterUnit morte
        var unit = entry.animator.GetComponentInParent<CharacterUnit>();
        if (unit != null && unit.IsDead)
            return;

        // Joue l'animation selon le mode choisi
        if (trigger.crossFadeDuration > 0f)
        {
            entry.animator.CrossFade(trigger.animationName, trigger.crossFadeDuration);
        }
        else
        {
            entry.animator.Play(trigger.animationName);
        }

#if UNITY_EDITOR
        Debug.Log($"[AnimationSignalReceiver] Played animation '{trigger.animationName}' on '{trigger.animatorID}'.");
#endif
    }
}