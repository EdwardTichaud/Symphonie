using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AnimatorEntry
{
    [Tooltip("ID unique pour identifier cet Animator (ex: Lucian, Door01, EnemyA)")]
    public string id;

    [Tooltip("Référence de l'Animator dans la scène")]
    public Animator animator;
}

public class AnimationSignalReceiver : MonoBehaviour
{
    [Header("List of Animators available in this scene")]
    public List<AnimatorEntry> animators = new List<AnimatorEntry>();

    public void TriggerAnimation(AnimationTriggerSO trigger)
    {
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