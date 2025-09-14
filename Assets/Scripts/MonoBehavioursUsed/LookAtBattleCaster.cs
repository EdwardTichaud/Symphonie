using UnityEngine;

/// Oriente la caméra vers le lanceur avec un offset de visée.
public class LookAtBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage du lanceur.")]
    public Vector3 offset;

    void LateUpdate()
    {
        // Récupère le lanceur actuel pour orienter la caméra.
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (!caster) return;

        // Oriente le transform vers le lanceur en prenant en compte l'offset.
        Vector3 lookPosition = caster.transform.position + offset;
        transform.LookAt(lookPosition);
    }
}
