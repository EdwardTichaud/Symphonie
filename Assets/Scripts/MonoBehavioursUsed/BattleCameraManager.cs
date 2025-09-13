using UnityEngine;
using Cinemachine;

/// <summary>
/// Gère les plans de caméra durant les combats en utilisant Cinemachine.
/// Ce gestionnaire centralise l'activation des CameraShotSO afin de
/// respecter le point de vue de Munin tout en offrant des transitions
/// souples entre les actions.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    public static BattleCameraManager Instance { get; private set; }

    [Header("Référence de caméra")]
    [Tooltip("Virtual Camera principale utilisée pour les plans de combat.")]
    public CinemachineVirtualCamera mainVirtualCamera;

    private CinemachineBrain brain;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
    }

    /// <summary>
    /// Applique un plan défini par un CameraShotSO.
    /// </summary>
    /// <param name="shot">Plan de caméra à jouer.</param>
    /// <param name="caster">Transform du lanceur, utilisé comme repli si aucune cible.</param>
    /// <param name="focus">Transform principal suivi pendant l'action.</param>
    public void PlayShot(CameraShotSO shot, Transform caster, Transform focus)
    {
        if (shot == null || mainVirtualCamera == null)
            return;

        Transform target = focus != null ? focus : caster;
        if (target == null)
            return;

        mainVirtualCamera.Follow = target;
        mainVirtualCamera.LookAt = target;
        mainVirtualCamera.m_Lens.FieldOfView = shot.fieldOfView;

        // Positionne la caméra en appliquant l'offset dans l'espace local de la cible
        mainVirtualCamera.transform.position = target.position + target.TransformDirection(shot.offset);

        // Configure la durée de transition entre les plans
        if (brain != null)
            brain.m_DefaultBlend.m_Time = shot.blendDuration;
    }
}
