using UnityEngine;

#if CINEMACHINE
using Cinemachine;
#endif

/// <summary>
/// Gère les plans de caméra durant les combats. Lorsque Cinemachine est
/// disponible, le script s'appuie sur ses composants avancés ; sinon il
/// se replie sur la caméra standard de Unity pour assurer un minimum de
/// fonctionnalité.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    public static BattleCameraManager Instance { get; private set; }
#if CINEMACHINE
    [Header("Référence de caméra")]
    [Tooltip("Virtual Camera principale utilisée pour les plans de combat.")]
    public CinemachineVirtualCamera mainVirtualCamera;

    private CinemachineBrain brain;
#else
    [Header("Référence de caméra")]
    [Tooltip("Caméra standard utilisée lorsque Cinemachine est absent.")]
    public Camera mainVirtualCamera;

    // Aucun CinemachineBrain n'est disponible ; variable conservée pour cohérence.
    private Camera brain;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
#if CINEMACHINE
        // Récupère la référence au CinemachineBrain pour gérer les fondus entre plans.
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
#endif
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

#if CINEMACHINE
        // Utilisation complète de Cinemachine : suivi, orientation et FOV.
        mainVirtualCamera.Follow = target;
        mainVirtualCamera.LookAt = target;
        mainVirtualCamera.m_Lens.FieldOfView = shot.fieldOfView;

        // Positionne la caméra en appliquant l'offset dans l'espace local de la cible
        mainVirtualCamera.transform.position = target.position + target.TransformDirection(shot.offset);

        // Configure la durée de transition entre les plans
        if (brain != null)
            brain.m_DefaultBlend.m_Time = shot.blendDuration;
#else
        // Version de secours : déplace et oriente simplement la caméra standard.
        mainVirtualCamera.transform.position = target.position + target.TransformDirection(shot.offset);
        mainVirtualCamera.transform.LookAt(target);
        mainVirtualCamera.fieldOfView = shot.fieldOfView;
#endif
    }
}
