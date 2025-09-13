using UnityEngine;
using Unity.Cinemachine;

/// Oriente la caméra vers la cible actuelle avec un offset de visée (Aim = CinemachineComposer).
[RequireComponent(typeof(CinemachineCamera))]
public class LookAtBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage de la cible.")]
    public Vector3 offset;

    private CinemachineCamera cmCamera;

    void Awake() => cmCamera = GetComponent<CinemachineCamera>();

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (!target) return;

        cmCamera.LookAt = target.transform;

        if (cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim) is CinemachineComposer composer)
            composer.m_TrackedObjectOffset = offset;
    }
}
