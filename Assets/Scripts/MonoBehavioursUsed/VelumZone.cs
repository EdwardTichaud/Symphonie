using UnityEngine;
using UnityEngine.Playables;

public class VelumZone : MonoBehaviour
{
    [Header("References")]
    private PlayableDirector timeline;

    private void Start()
    {
        timeline = transform.GetChild(0).GetComponent<PlayableDirector>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            EnterVelumZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ExitVelumZone();
        }
    }

    private void EnterVelumZone()
    {
        timeline.Play();
        // Lance la timeline via le TimelineManager pour suspendre caméra et contrôles
        // tant que la zone de Vélum est active.
        if (TimelineManager.Instance != null)
            TimelineManager.Instance.PlayTimeline(timeline);
        else
            timeline.Play();
    }

    private void ExitVelumZone()
    {
        // On quitte la zone
    }
}
