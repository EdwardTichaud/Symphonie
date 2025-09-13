using UnityEngine;
using Cinemachine;

/// <summary>
/// Oriente la caméra vers la cible actuelle avec un décalage optionnel.
/// </summary>
// Adoption de CinemachineCamera pour être compatible avec Cinemachine 3
[RequireComponent(typeof(CinemachineCamera))]
public class LookAtBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage de la cible.")]
    public Vector3 offset;

    // Caméra utilisée pour orienter le regard vers la cible
    private CinemachineCamera cmCamera;

    void Awake()
    {
        // Récupère la CinemachineCamera associée à ce GameObject
        cmCamera = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target == null) return;

        // Oriente la caméra vers la cible actuelle
        cmCamera.LookAt = target.transform;
        var composer = cmCamera.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
            composer.m_TrackedObjectOffset = offset;
    }
}
