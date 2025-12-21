using UnityEngine;

/// Suit la cible actuelle du combat avec un décalage personnalisé.
public class FollowBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position de la cible.")]
    public Vector3 offset = new(0, 2, -3);

    void LateUpdate()
    {
        // Récupère la cible actuellement suivie dans le gestionnaire de combat.
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (!target) return;

        // Déplace directement la caméra en appliquant l'offset au transform.
        // On n'utilise plus CinemachineCamera afin de rester indépendant de ce composant.
        transform.position = target.transform.position + offset;
    }
}
