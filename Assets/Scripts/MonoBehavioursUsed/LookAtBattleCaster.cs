using UnityEngine;
using Cinemachine;

/// <summary>
/// Oriente la caméra vers le lanceur actuel avec un décalage optionnel.
/// </summary>
// Utilisation de CinemachineCamera (nouveau dans Cinemachine 3) à la place de l'ancienne CinemachineVirtualCamera
[RequireComponent(typeof(CinemachineCamera))]
public class LookAtBattleCaster : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage du lanceur.")]
    public Vector3 offset;

    // Référence vers la caméra Cinemachine qui suit et oriente l'action
    private CinemachineCamera cmCamera;

    void Awake()
    {
        // Récupération de la nouvelle CinemachineCamera au lieu de CinemachineVirtualCamera
        cmCamera = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        var caster = NewBattleManager.Instance?.currentCharacterUnit;
        if (caster == null) return;

        // Affecte le Transform du lanceur comme cible à regarder
        cmCamera.LookAt = caster.transform;
        // Récupère le composant Composer pour appliquer le décalage configuré
        var composer = cmCamera.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
            composer.m_TrackedObjectOffset = offset;
    }
}
