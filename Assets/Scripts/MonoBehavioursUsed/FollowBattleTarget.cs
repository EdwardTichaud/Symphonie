using UnityEngine;
using Unity.Cinemachine;

/// Suit la cible actuelle du combat avec un décalage personnalisé (Body = CinemachineFollow).
[RequireComponent(typeof(CinemachineCamera))]
public class FollowBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position de la cible.")]
    public Vector3 offset = new(0, 2, -3);

    private CinemachineCamera cmCamera;

    void Awake() => cmCamera = GetComponent<CinemachineCamera>();

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (!target) return;

        cmCamera.Follow = target.transform;

        // Cinemachine 3 : Body = CinemachineFollow
        if (cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) is CinemachineFollow follow)
            follow.FollowOffset = offset;
    }
}
