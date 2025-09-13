using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Assigne dynamiquement le lanceur actuel comme cible de suivi pour une caméra Cinemachine.
/// </summary>
// Utilise CinemachineCamera (Cinemachine 3) à la place de CinemachineVirtualCamera
[RequireComponent(typeof(CinemachineCamera))]
public class FollowBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué par rapport à la position du lanceur.")]
    public Vector3 offset = new(0, 2, -3);

    // Caméra responsable du suivi du lanceur
    private CinemachineCamera cmCamera;

    void Awake()
    {
        // Obtention de la nouvelle CinemachineCamera
        cmCamera = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (caster == null) return;

        // Suit le lanceur tout en appliquant un décalage personnalisé
        cmCamera.Follow = caster.transform;
        var transposer = cmCamera.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
            transposer.m_FollowOffset = offset;
    }
}
