using UnityEngine;

public class TeleportToPoint : MonoBehaviour
{
    [Header("Point de destination")]
    public Transform targetPoint;

    [Header("Objets à téléporter (laisser vide pour téléporter uniquement ce GameObject)")]
    public Transform[] objectsToTeleport;

    [ContextMenu("Téléporter maintenant")]
    public void Teleport()
    {
        if (targetPoint == null)
        {
            Debug.LogWarning("Aucun point de destination assigné !");
            return;
        }

        if (objectsToTeleport != null && objectsToTeleport.Length > 0)
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
