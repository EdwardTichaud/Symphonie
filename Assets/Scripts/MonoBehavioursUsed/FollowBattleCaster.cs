using UnityEngine;

/// Suit le lanceur actuel (caster) avec un décalage.
public class FollowBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position du lanceur.")]
    public Vector3 offset = new(0, 2, -3);

    void LateUpdate()
    {
        // Récupère le lanceur actuellement actif dans le combat.
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (!caster) return;

        // Place la caméra directement par rapport au lanceur, en tenant compte de l'offset.
        transform.position = caster.transform.position + offset;
    }
}
