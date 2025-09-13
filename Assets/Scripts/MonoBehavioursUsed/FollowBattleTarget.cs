using UnityEngine;
using Cinemachine;

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

        // Assigne la cible à suivre et ajuste le décalage
        cmCamera.Follow = target.transform;
        var transposer = cmCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
            transposer.m_FollowOffset = offset;
    }
}
