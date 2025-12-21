using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Declenche la lecture d'une Timeline lorsque le joueur entre dans ce trigger.
/// La Timeline passe par le <see cref="TimelineManager"/> afin de garantir
/// la desactivation du <see cref="CameraController"/> et des controles joueur
/// pendant la cinematique.
/// </summary>
public class TimelineTrigger : MonoBehaviour
{
    [Tooltip("Timeline jouee quand le joueur entre dans le trigger.")]
    public PlayableDirector timeline;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || timeline == null)
            return;

        // Utilise le TimelineManager s'il est present pour centraliser la gestion des timelines.
        if (TimelineManager.Instance != null)
            TimelineManager.Instance.PlayTimeline(timeline);
        else
            timeline.Play();
    }
}
