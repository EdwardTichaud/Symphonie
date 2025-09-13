using UnityEngine;
using Cinemachine;

/// <summary>
/// Oriente la caméra vers le lanceur actuel avec un décalage optionnel.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class LookAtBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage du lanceur.")]
    public Vector3 offset;

    private CinemachineVirtualCamera vcam;

    void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (caster == null) return;

        vcam.LookAt = caster.transform;
        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
            composer.m_TrackedObjectOffset = offset;
    }
}
