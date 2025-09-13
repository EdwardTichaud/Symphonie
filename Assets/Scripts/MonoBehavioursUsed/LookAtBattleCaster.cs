using UnityEngine;
using Unity.Cinemachine;

/// Oriente la caméra vers le lanceur avec un offset de visée (Aim = CinemachineComposer).
[RequireComponent(typeof(CinemachineCamera))]
public class LookAtBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage du lanceur.")]
    public Vector3 offset;

    private CinemachineCamera cmCamera;

    void Awake() => cmCamera = GetComponent<CinemachineCamera>();

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (!caster) return;

        cmCamera.LookAt = caster.transform;

        // Cinemachine 3 : Composer → offset = m_TrackedObjectOffset
        if (cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim) is CinemachineComposer composer)
            composer.m_TrackedObjectOffset = offset;
    }
}
