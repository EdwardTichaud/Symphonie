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

        // Suit le lanceur
        cmCamera.Follow = caster.transform;
        // Récupère le composant du corps via l'étape correspondante dans Cinemachine 3
        var transposer = cmCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineTransposer;
        // Applique le décalage si le composant existe
        if (transposer != null)
            transposer.FollowOffset = offset;
    }
}
