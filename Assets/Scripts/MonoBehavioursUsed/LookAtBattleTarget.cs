using UnityEngine;
using Cinemachine;

/// <summary>
/// Oriente la caméra vers la cible actuelle avec un décalage optionnel.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class LookAtBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage de la cible.")]
    public Vector3 offset;

    private CinemachineVirtualCamera vcam;

    void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target == null) return;

        vcam.LookAt = target.transform;
        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
            composer.m_TrackedObjectOffset = offset;
    }
}
