using UnityEngine;

/// <summary>
/// Composant permettant de téléporter un ou plusieurs objets vers un point donné.
/// La destination peut être définie à l'avance via l'inspecteur ou transmise
/// dynamiquement (par exemple depuis une Timeline) grâce à la surcharge de Teleport.
/// </summary>
public class TeleportToPoint : MonoBehaviour
{
    [Header("Point de destination par défaut")]
    [Tooltip("Si aucun Transform n'est passé en paramètre à Teleport(), ce point est utilisé.")]
    public Transform targetPoint;

    [Header("Objets à téléporter (laisser vide pour téléporter uniquement ce GameObject)")]
    public Transform[] objectsToTeleport;

    /// <summary>
    /// Téléporte les objets vers le point 'targetPoint' défini dans l'inspecteur.
    /// Cette version sans paramètre est utile pour les appels depuis le menu contextuel.
    /// </summary>
    [ContextMenu("Téléporter maintenant")]
    public void Teleport()
    {
        // On réutilise la logique commune en fournissant le point défini dans l'inspecteur
        TeleportInternal(targetPoint);
    }

    /// <summary>
    /// Téléporte les objets vers le <see cref="Transform"/> fourni en paramètre.
    /// Cette méthode permet notamment aux Signaux de Timeline d'indiquer directement
    /// la destination de la téléportation.
    /// </summary>
    /// <param name="destination">Point de destination où téléporter les objets.</param>
    public void Teleport(Transform destination)
    {
        TeleportInternal(destination);
    }

    /// <summary>
    /// Contient la logique partagée de téléportation entre les différentes surcharges.
    /// </summary>
    /// <param name="point">Point final de la téléportation.</param>
    private void TeleportInternal(Transform point)
    {
        if (point == null)
        {
            Debug.LogWarning("Aucun point de destination assigné !");
            return;
        }

        if (objectsToTeleport != null && objectsToTeleport.Length > 0)
        {
            foreach (Transform obj in objectsToTeleport)
            {
                if (obj != null)
                {
                    // Déplacement de chaque objet explicitement renseigné
                    obj.position = point.position;
                }
            }
        }
        else
        {
            // Si aucun objet n'est défini, on déplace juste celui qui possède ce script
            transform.position = point.position;
        }
    }
}
