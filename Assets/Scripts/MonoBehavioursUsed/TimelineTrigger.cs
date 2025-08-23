using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Déclenche la lecture d'une Timeline lorsque le joueur entre dans ce trigger.
/// La Timeline passe par le <see cref="TimelineManager"/> afin de garantir
/// la désactivation du <see cref="CameraController"/> et des contrôles joueur
/// pendant la cinématique.
/// </summary>
public class TimelineTrigger : MonoBehaviour
{
    [Tooltip("Timeline jouée quand le joueur entre dans le trigger.")]
    public PlayableDirector timeline;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            if (other.CompareTag("Player") && timeline != null)
            {
                timeline.Play();
                // Utilise le TimelineManager s'il est présent pour centraliser
                // la gestion des timelines. Sinon, on lance directement.
                if (TimelineManager.Instance != null)
                    TimelineManager.Instance.PlayTimeline(timeline);
                else
                    timeline.Play();
            }
    }
}