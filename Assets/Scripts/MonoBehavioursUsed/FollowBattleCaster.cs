using UnityEngine;
using Cinemachine;

/// <summary>
/// Assigne dynamiquement le lanceur actuel comme cible de suivi pour une CinemachineVirtualCamera.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class FollowBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position du lanceur.")]
    public Vector3 offset = new(0, 2, -3);

    private CinemachineVirtualCamera vcam;

    void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (caster == null) return;

        vcam.Follow = caster.transform;
        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
            transposer.m_FollowOffset = offset;
    }
}
