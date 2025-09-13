using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Suit la cible actuelle du combat avec un décalage personnalisé.
/// </summary>
// Désormais basé sur CinemachineCamera pour la compatibilité avec Cinemachine 3
[RequireComponent(typeof(CinemachineCamera))]
public class FollowBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position de la cible.")]
    public Vector3 offset = new(0, 2, -3);

    // Caméra Cinemachine utilisée pour suivre la cible
    private CinemachineCamera cmCamera;

    void Awake()
    {
        // Remplace l'ancien appel vers CinemachineVirtualCamera
        cmCamera = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target == null) return;

        // Assigne la cible à suivre
        cmCamera.Follow = target.transform;
        // Dans Cinemachine 3, les composants sont obtenus par étape plutôt que par générique
        var transposer = cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineTransposer;
        // Si un transposer est trouvé, on applique le décalage voulu via la propriété publique
        if (transposer != null)
            transposer.FollowOffset = offset;
    }
}
