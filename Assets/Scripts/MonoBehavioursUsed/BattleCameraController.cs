using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Contrôleur spécialisé pour la caméra de combat.
/// Gère les petits effets de respiration et expose une orbite simple utilisée par certains évènements.
/// </summary>
[DisallowMultipleComponent]
public class BattleCameraController : MonoBehaviour
{
    /// <summary>
    /// Instance globale, utile pour les timelines et scripts de gameplay.
    /// </summary>
    public static BattleCameraController Instance { get; private set; }

    [Header("Références")]
    [Tooltip("Caméra principale utilisée pendant les combats.")]
    [SerializeField] private Camera battleCamera;

    [Tooltip("Rig optionnel permettant de déplacer la caméra sans toucher au CinemachineBrain.")]
    [SerializeField] private Transform cameraRig;

    [Header("Respiration")] 
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float breathingAmplitude = 0.05f;
    [SerializeField] private float breathingFrequency = 1f;

    [Header("Orbite")] 
    [SerializeField] private float orbitDistance = 5f;
    [SerializeField] private float orbitSpeed = 30f;
    [SerializeField] private bool orbitX;
    [SerializeField] private bool orbitY = true;
    [SerializeField] private bool orbitZ;

    private bool supportsBreathing;
    private float breathingOffset;
    private Vector3 baseLocalPosition;
    private bool baseCaptured;

    private Transform orbitTarget;

    private void Reset()
    {
        battleCamera = GetComponent<Camera>();
        cameraRig = transform.parent;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BattleCameraController] Instance en double détectée, destruction du composant excédentaire.");
            Destroy(this);
            return;
        }

        Instance = this;

        battleCamera ??= GetComponent<Camera>();
        cameraRig ??= transform.parent;

        DetermineBreathingSupport();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        battleCamera ??= GetComponent<Camera>();
        if (battleCamera != null && cameraRig == null)
        {
            cameraRig = transform.parent;
        }
    }

    private void LateUpdate()
    {
        if (!enabled || !Application.isPlaying)
            return;

        if (orbitTarget != null)
        {
            UpdateOrbit();
        }

        ApplyBreathing();
    }

    /// <summary>
    /// Détermine si la respiration peut être appliquée sans lutter avec Cinemachine.
    /// </summary>
    private void DetermineBreathingSupport()
    {
        bool hasUnityCamera = battleCamera != null;
        bool hasCinemachineComponent = TryGetComponent(out CinemachineCamera _) || TryGetComponent(out CinemachineBrain _);

        supportsBreathing = hasUnityCamera && !hasCinemachineComponent;

        if (!supportsBreathing && enableBreathing)
        {
            Debug.Log("[BattleCameraController] Respiration désactivée : la caméra est pilotée par Cinemachine.", this);
        }
    }

    /// <summary>
    /// Met à jour la position locale de la caméra pour simuler la respiration.
    /// </summary>
    private void ApplyBreathing()
    {
        if (!supportsBreathing || !enableBreathing)
        {
            if (baseCaptured && !Mathf.Approximately(breathingOffset, 0f))
            {
                transform.localPosition = baseLocalPosition;
                breathingOffset = 0f;
            }
            return;
        }

        if (!baseCaptured)
        {
            baseLocalPosition = transform.localPosition;
            baseCaptured = true;
        }

        float newOffset = Mathf.Sin(Time.time * breathingFrequency) * breathingAmplitude;
        transform.localPosition = baseLocalPosition + Vector3.up * newOffset;
        breathingOffset = newOffset;
    }

    /// <summary>
    /// Lance une orbite simple autour d'une cible (utilisé par certaines cinématiques de combat).
    /// </summary>
    public void StartOrbit(Transform target, float distance, float speed, bool x, bool y, bool z)
    {
        if (target == null)
        {
            Debug.LogWarning("[BattleCameraController] Impossible de démarrer l'orbite sans cible.");
            return;
        }

        if (TryGetComponent(out CinemachineBrain _) || TryGetComponent(out CinemachineCamera _))
        {
            Debug.LogWarning("[BattleCameraController] Orbite ignorée car Cinemachine contrôle déjà cette caméra.", this);
            return;
        }

        orbitTarget = target;
        orbitDistance = distance;
        orbitSpeed = speed;
        orbitX = x;
        orbitY = y;
        orbitZ = z;

        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 direction = (rig.position - orbitTarget.position).normalized;
        if (direction == Vector3.zero)
        {
            direction = Vector3.forward;
        }

        rig.position = orbitTarget.position + direction * orbitDistance;
        rig.LookAt(orbitTarget);
    }

    /// <summary>
    /// Arrête l'orbite en cours.
    /// </summary>
    public void StopOrbit()
    {
        orbitTarget = null;
    }

    private void UpdateOrbit()
    {
        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 axis = new Vector3(orbitX ? 1f : 0f, orbitY ? 1f : 0f, orbitZ ? 1f : 0f);
        if (axis == Vector3.zero)
            return;

        axis.Normalize();
        float angle = orbitSpeed * Time.deltaTime;

        rig.RotateAround(orbitTarget.position, axis, angle);
        Vector3 offset = rig.position - orbitTarget.position;
        rig.position = orbitTarget.position + offset.normalized * orbitDistance;
        rig.LookAt(orbitTarget);
    }
}
