using UnityEngine;
using Unity.Cinemachine;

/// Suit le lanceur actuel (caster) avec un décalage (Body = CinemachineFollow).
[RequireComponent(typeof(CinemachineCamera))]
public class FollowBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position du lanceur.")]
    public Vector3 offset = new(0, 2, -3);

    private CinemachineCamera cmCamera;

    void Awake() => cmCamera = GetComponent<CinemachineCamera>();

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (!caster) return;

        cmCamera.Follow = caster.transform;

        if (cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) is CinemachineFollow follow)
            follow.FollowOffset = offset;
    }
}
