using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZoneTrigger : MonoBehaviour
{
    [Tooltip("ZoneSO à activer quand le joueur entre.")]
    public ZoneSO zone;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (zone == null)
            {
                Debug.LogWarning("[ZoneTrigger] ZoneSO non assignée !");
                return;
            }

            ZoneManager.Instance.SetCurrentZone(zone);
            Debug.Log($"[ZoneTrigger] Entrée dans la zone : {zone.zoneName}");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if(ZoneManager.Instance.currentZone == null)
            {
                ZoneManager.Instance.SetCurrentZone(zone);
            }
        }
    }
}
