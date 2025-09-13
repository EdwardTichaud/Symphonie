using UnityEngine;
using Cinemachine;

/// <summary>
/// Suit la cible actuelle du combat avec un décalage personnalisé.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class FollowBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position de la cible.")]
    public Vector3 offset = new(0, 2, -3);

    private CinemachineVirtualCamera vcam;

    void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target == null) return;

        vcam.Follow = target.transform;
        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
            transposer.m_FollowOffset = offset;
    }
}
