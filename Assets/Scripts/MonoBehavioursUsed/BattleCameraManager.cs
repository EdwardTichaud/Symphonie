using UnityEngine;

//
// Ce script doit pouvoir compiler que Cinemachine soit installé ou non.
// La version 3 de Cinemachine a introduit un nouveau namespace et de nouveaux
// types (CinemachineCamera remplace CinemachineVirtualCamera, par exemple).
// Les directives de compilation conditionnelle ci-dessous détectent la
// présence et la version de Cinemachine afin d'utiliser l'API appropriée.
//

#if CINEMACHINE_3_0_0_OR_NEWER
using Unity.Cinemachine;          // Nouveau namespace pour Cinemachine 3+
#define HAS_CINEMACHINE            // Indique que Cinemachine est disponible
#elif CINEMACHINE
using Cinemachine;                // Ancien namespace pour Cinemachine 2.x
#define HAS_CINEMACHINE
#endif

/// <summary>
/// Gère les plans de caméra durant les combats. Lorsque Cinemachine est
/// disponible, le script s'appuie sur ses composants avancés ; sinon il
/// se replie sur la caméra standard de Unity pour assurer un minimum de
/// fonctionnalité.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    // Singleton classique utilisé par le reste du projet.
    public static BattleCameraManager Instance { get; private set; }

#if CINEMACHINE_3_0_0_OR_NEWER && HAS_CINEMACHINE
    // --- Configuration pour Cinemachine 3.x ---
    [Header("Référence de caméra")]
    [Tooltip("Caméra Cinemachine (v3+) principale utilisée pour les plans de combat.")]
    public CinemachineCamera mainVirtualCamera;

    // Le CinemachineBrain reste présent pour gérer les transitions entre plans.
    private CinemachineBrain brain;

#elif HAS_CINEMACHINE
    // --- Configuration pour Cinemachine 2.x ---
    [Header("Référence de caméra")]
    [Tooltip("Virtual Camera principale utilisée pour les plans de combat.")]
    public CinemachineVirtualCamera mainVirtualCamera;

    private CinemachineBrain brain;

#else
    // --- Version de secours sans Cinemachine ---
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

#if HAS_CINEMACHINE
        // Récupère la référence au CinemachineBrain pour gérer les fondus entre plans.
        // Si aucune caméra principale n'est présente ou si le composant n'est pas trouvé,
        // "brain" restera nul et les fondus seront ignorés.
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

#if HAS_CINEMACHINE
        // --- Utilisation de Cinemachine lorsque disponible ---
        mainVirtualCamera.Follow = target;            // La caméra suit la cible choisie.
        mainVirtualCamera.LookAt = target;            // Et la regarde.

#if CINEMACHINE_3_0_0_OR_NEWER
        // Dans Cinemachine 3, le champ de vision est exposé via la propriété Lens.
        mainVirtualCamera.Lens.FieldOfView = shot.fieldOfView;
#else
        // Ancienne API (Cinemachine 2.x).
        mainVirtualCamera.m_Lens.FieldOfView = shot.fieldOfView;
#endif

        // Positionne la caméra à la position calculée.
        mainVirtualCamera.transform.position = finalPosition;

        // Configure la durée de transition entre les plans.
        if (brain != null)
        {
#if CINEMACHINE_3_0_0_OR_NEWER
            brain.DefaultBlend.Time = shot.blendDuration;
#else
            brain.m_DefaultBlend.m_Time = shot.blendDuration;
#endif
        }
#else
        // --- Version de secours : déplace et oriente simplement la caméra standard. ---
        mainVirtualCamera.transform.position = finalPosition;
        mainVirtualCamera.transform.LookAt(target); // Conserve le regard sur la cible.
        mainVirtualCamera.fieldOfView = shot.fieldOfView;
#endif
    }
}
