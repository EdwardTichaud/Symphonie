using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

public class TeleportAfterTimeline : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private List<Transform> objectsToTeleport;
    [SerializeField] private Transform targetPoint;

    private void OnEnable()
    {
        director.stopped += OnTimelineStopped;
    }

    private void OnDisable()
    {
        director.stopped -= OnTimelineStopped;
    }

    private void OnTimelineStopped(PlayableDirector pd)
    {
        if (pd != director) return;
        Teleport();
    }

    private void Teleport()
    {
        if (targetPoint == null)
        {
            Debug.LogWarning("Aucun point de destination assigné !");
            return;
        }

        if (objectsToTeleport != null && objectsToTeleport.Count > 0)
        {
            foreach (Transform obj in objectsToTeleport)
            {
                if (obj != null)
                    obj.position = targetPoint.position;
            }
        }
        else
        {
            // Si aucun objet n'est défini, on déplace juste celui qui a ce script
            transform.position = targetPoint.position;
        }
    }
}
