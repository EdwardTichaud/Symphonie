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
    public CinemachineCamera mainVirtualCamera;

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

        // Détermine la cible à suivre en fonction des paramètres du plan.
        Transform target = shot.targetToFollow == CameraShotSO.ShotTarget.Caster ? caster : focus;
        if (target == null)
            return;

        // Calcule la position finale de la caméra : on part de la cible, on recule selon son orientation
        // pour respecter la distance demandée puis on applique l'offset local.
        Vector3 finalPosition = target.position
                                - target.forward * shot.distance
                                + target.TransformDirection(shot.offset);

#if CINEMACHINE
        // Utilisation complète de Cinemachine : suivi, orientation et FOV.
        mainVirtualCamera.Follow = target;            // La caméra suit la cible choisie.
        mainVirtualCamera.LookAt = target;            // Et la regarde.
        mainVirtualCamera.m_Lens.FieldOfView = shot.fieldOfView;

        // Positionne la caméra à la position calculée.
        mainVirtualCamera.transform.position = finalPosition;

        // Configure la durée de transition entre les plans.
        if (brain != null)
            brain.m_DefaultBlend.m_Time = shot.blendDuration;
#else
        // Version de secours : déplace et oriente simplement la caméra standard.
        mainVirtualCamera.transform.position = finalPosition;
        mainVirtualCamera.transform.LookAt(target); // Conserve le regard sur la cible.
        mainVirtualCamera.fieldOfView = shot.fieldOfView;
#endif
    }
}
