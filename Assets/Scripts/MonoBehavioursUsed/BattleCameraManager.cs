using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Gère l'activation et le placement des caméras Cinemachine pendant les combats.
/// Munin positionne la caméra autour d'un point de référence via un <see cref="CameraMotifSO"/>.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Accès global au gestionnaire de caméras de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    /// <summary>
    /// Fournit un accès en lecture à l'unité actuellement propriétaire du tour.
    /// Ce getter est principalement utilisé par le <see cref="NewBattleManager"/>.
    /// </summary>
    public CharacterUnit CurrentTurnOwner => currentTurnOwner;

    /// <summary>BlendSwitcher responsable des transitions (0,5 s smooth imposé).</summary>
    private CinemachineBlendSwitcher blendSwitcher;

    /// <summary>Accès direct aux CinemachineCamera par leur nom.</summary>
    private readonly Dictionary<string, CinemachineCamera> cameraByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>État de lissage par caméra.</summary>
    private readonly Dictionary<string, CameraState> cameraStates = new(StringComparer.OrdinalIgnoreCase);

    [Header("Effet de respiration")]
    [Tooltip("Active ou non le léger flottement des caméras de combat pour éviter une image totalement fixe.")]
    [SerializeField] private bool enableBreathingMotion = true;

    [Tooltip("Nombre d'oscillations complètes par seconde appliqué au mouvement de respiration.")]
    [SerializeField] private float breathingFrequency = 0.33f;

    [Tooltip("Amplitude verticale (en mètres) ajoutée par l'effet de respiration.")]
    [SerializeField] private float breathingVerticalAmplitude = 0.05f;

    [Tooltip("Amplitude latérale (en mètres) ajoutée par l'effet de respiration.")]
    [SerializeField] private float breathingHorizontalAmplitude = 0.025f;

    [Tooltip("Amplitude de tangage (en degrés) appliquée à la caméra pendant l'oscillation.")]
    [SerializeField] private float breathingPitchAmplitude = 0.8f;

    [Tooltip("Amplitude de lacet (en degrés) appliquée à la caméra pendant l'oscillation.")]
    [SerializeField] private float breathingYawAmplitude = 0.4f;

    [Header("Motif par défaut")]
    [Tooltip("Motif appliqué en continu si aucun motif ponctuel n'est actif.")]
    [SerializeField] private CameraMotifSO defaultMotif;

    /// <summary>Unité actuellement en train de jouer son tour.</summary>
    private CharacterUnit currentTurnOwner;

    /// <summary>Unité considérée comme lanceur pour les caméras d'action.</summary>
    private CharacterUnit currentCaster;

    /// <summary>Unité ciblée par l'action en cours ou par la sélection.</summary>
    private CharacterUnit currentTarget;

    /// <summary>Motif actuellement actif.</summary>
    private CameraMotifSO activeMotif;

    /// <summary>Motif précédent pour permettre un blend progressif.</summary>
    private CameraMotifSO previousMotif;

    /// <summary>Timer de blend entre motifs.</summary>
    private float motifBlendTimer;

    /// <summary>Durée retenue pour la transition de motif.</summary>
    private float motifBlendDuration = 0.25f;

    /// <summary>Indique si une transition de motif est en cours.</summary>
    private bool motifBlendActive;

    /// <summary>Décalages de phase uniques par caméra pour l'effet de respiration.</summary>
    private readonly Dictionary<string, float> breathingPhaseOffsets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>État interne utilisé pour lisser chaque caméra.</summary>
    private sealed class CameraState
    {
        public bool Initialized;
        public Vector3 PositionVelocity;
        public Vector3 SmoothedPosition;
        public Quaternion SmoothedRotation;
        public bool OrbitActive;
        public float OrbitPhaseOffset;
        public float OrbitSpeed;
        public CameraMotifSO.ReferencePoint OrbitReferencePoint;
    }

    private struct MotifTuning
    {
        public CameraMotifSO.ReferencePoint ReferencePoint;
        public Vector3 ReferenceOffsetPosition;
        public Vector3 ReferenceOffsetRotation;
        public float ReferenceOffsetSmoothTime;
        public bool OrbitEnabled;
        public CameraMotifSO.ReferencePoint OrbitReferencePoint;
        public float OrbitSpeed;
        public bool LookAtEnabled;
        public CameraMotifSO.ReferencePoint LookAtReferencePoint;
    }

    /// <summary>Style de blend homogène imposé par le <see cref="CinemachineBlendSwitcher"/>.</summary>
    public CinemachineBlendDefinition.Styles SmoothBlendStyle =>
        blendSwitcher ? blendSwitcher.SmoothBlendStyle : CinemachineBlendSwitcher.ResolveSmoothBlendStyle();

    /// <summary>Durée (0,5 s) imposée à toutes les transitions caméra.</summary>
    public float SmoothBlendDuration =>
        blendSwitcher ? blendSwitcher.SmoothBlendDuration : CinemachineBlendSwitcher.GlobalSmoothBlendDurationSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        blendSwitcher = FindFirstObjectByType<CinemachineBlendSwitcher>();
        if (!blendSwitcher)
            Debug.LogWarning("[BattleCameraManager] Aucun CinemachineBlendSwitcher trouvé dans la scène.");

        RefreshCameraCache();

        // Active une Cinemachine par défaut pour éviter tout retour vers la caméra Unity brute.
        if (blendSwitcher)
            SwitchToCamera("CMV_MainMenu", SmoothBlendDuration);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        cameraByName.Clear();
        cameraStates.Clear();
        breathingPhaseOffsets.Clear();
    }

    private void LateUpdate()
    {
        UpdateMotifBlend();
        RefreshAllCameraPlacements();
    }

    /// <summary>Réactualise le cache des CinemachineCamera disponibles.</summary>
    private void RefreshCameraCache()
    {
        cameraByName.Clear();
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cam == null)
                continue;

            string name = cam.gameObject.name;
            if (!cameraByName.ContainsKey(name))
                cameraByName.Add(name, cam);
        }

        PruneCameraStates();
    }

    /// <summary>Supprime les états associés à des caméras disparues.</summary>
    private void PruneCameraStates()
    {
        if (cameraStates.Count == 0)
            return;

        List<string> staleKeys = null;
        foreach (var kvp in cameraStates)
        {
            if (!cameraByName.ContainsKey(kvp.Key))
            {
                staleKeys ??= new List<string>();
                staleKeys.Add(kvp.Key);
            }
        }

        if (staleKeys == null)
            return;

        foreach (string key in staleKeys)
            cameraStates.Remove(key);
    }

    /// <summary>Replace toutes les caméras "CMV_" sur leurs points d'ancrage respectifs.</summary>
    private void RefreshAllCameraPlacements()
    {
        if (!blendSwitcher || !blendSwitcher.HasActiveCamera)
            return;

        string cameraName = blendSwitcher.CurrentCameraName;
        if (string.IsNullOrEmpty(cameraName))
            return;

        if (!TryGetCameraByName(cameraName, out var camera) || camera == null)
            return;

        if (ApplyMotifPlacement(camera, cameraName))
            ApplyBreathingMotion(camera, cameraName);
    }

    /// <summary>
    /// Applique le motif actif pour positionner et orienter la caméra autour du point de référence.
    /// </summary>
    private bool ApplyMotifPlacement(CinemachineCamera camera, string cameraName)
    {
        if (camera == null)
            return false;

        MotifTuning tuning = ResolveMotifTuning();
        CharacterUnit referenceUnit = ResolveReferenceUnit(tuning.ReferencePoint);
        if (referenceUnit == null)
            return false;

        Transform referenceTransform = referenceUnit.transform;

        Vector3 offsetWorld = referenceTransform.TransformVector(tuning.ReferenceOffsetPosition);
        Vector3 desiredPosition = referenceTransform.position + offsetWorld;
        Quaternion desiredRotation = referenceTransform.rotation * Quaternion.Euler(tuning.ReferenceOffsetRotation);

        CameraState state = GetOrCreateCameraState(cameraName);
        if (state == null)
            return false;

        bool orbitApplied = false;
        if (tuning.OrbitEnabled && Mathf.Abs(tuning.OrbitSpeed) > 0.0001f)
        {
            CharacterUnit orbitUnit = ResolveReferenceUnit(tuning.OrbitReferencePoint);
            if (orbitUnit != null)
            {
                bool orbitConfigChanged = !state.OrbitActive
                    || state.OrbitReferencePoint != tuning.OrbitReferencePoint
                    || Mathf.Abs(state.OrbitSpeed - tuning.OrbitSpeed) > 0.0001f;

                if (orbitConfigChanged)
                {
                    state.OrbitPhaseOffset = -Time.time * tuning.OrbitSpeed;
                    state.OrbitSpeed = tuning.OrbitSpeed;
                    state.OrbitReferencePoint = tuning.OrbitReferencePoint;
                    state.OrbitActive = true;
                }

                float orbitAngle = Time.time * tuning.OrbitSpeed + state.OrbitPhaseOffset;
                Vector3 pivot = orbitUnit.transform.position;
                Vector3 axis = orbitUnit.transform.up;
                Vector3 toCamera = desiredPosition - pivot;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    Quaternion orbitRotation = Quaternion.AngleAxis(orbitAngle, axis);
                    desiredPosition = pivot + orbitRotation * toCamera;
                }

                orbitApplied = true;
            }
        }
        if (!orbitApplied)
        {
            state.OrbitActive = false;
        }

        if (tuning.LookAtEnabled)
        {
            CharacterUnit lookAtUnit = ResolveReferenceUnit(tuning.LookAtReferencePoint);
            if (lookAtUnit != null)
            {
                Vector3 lookAtPosition = lookAtUnit.transform.position;
                Vector3 forward = lookAtPosition - desiredPosition;
                if (forward.sqrMagnitude > 0.0001f)
                    desiredRotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(tuning.ReferenceOffsetRotation);
            }
        }

        float smoothTime = Mathf.Max(0f, tuning.ReferenceOffsetSmoothTime);
        if (!state.Initialized || smoothTime <= 0.0001f)
        {
            state.SmoothedPosition = desiredPosition;
            state.SmoothedRotation = desiredRotation;
            state.PositionVelocity = Vector3.zero;
            state.Initialized = true;
        }
        else
        {
            state.SmoothedPosition = Vector3.SmoothDamp(
                state.SmoothedPosition,
                desiredPosition,
                ref state.PositionVelocity,
                smoothTime);

            float rotationBlend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothTime));
            state.SmoothedRotation = Quaternion.Slerp(state.SmoothedRotation, desiredRotation, rotationBlend);
        }

        camera.transform.SetPositionAndRotation(state.SmoothedPosition, state.SmoothedRotation);
        return true;
    }

    /// <summary>Met à jour l'interpolation entre deux motifs actifs.</summary>
    private void UpdateMotifBlend()
    {
        if (!motifBlendActive)
            return;

        float duration = Mathf.Max(0.0001f, motifBlendDuration);
        motifBlendTimer += Time.unscaledDeltaTime;
        if (motifBlendTimer >= duration)
        {
            motifBlendTimer = duration;
            motifBlendActive = false;
            previousMotif = activeMotif;
        }
    }

    /// <summary>Active un motif pour modifier le comportement de la caméra.</summary>
    public void SetCameraMotif(CameraMotifSO motif, float blendDuration = -1f)
    {
        if (motif == activeMotif && !motifBlendActive)
            return;

        previousMotif = activeMotif;
        activeMotif = motif;

        float resolvedDuration = blendDuration >= 0f
            ? blendDuration
            : (motif != null ? motif.blendDuration : 0.25f);

        motifBlendDuration = Mathf.Max(0.0001f, resolvedDuration);
        motifBlendTimer = 0f;
        motifBlendActive = true;
    }

    /// <summary>Désactive le motif courant.</summary>
    public void ClearCameraMotif(float blendDuration = -1f)
    {
        SetCameraMotif(null, blendDuration);
    }

    /// <summary>Calcule les réglages finaux en tenant compte du motif actif.</summary>
    private MotifTuning ResolveMotifTuning()
    {
        MotifTuning baseTuning = new MotifTuning
        {
            ReferencePoint = CameraMotifSO.ReferencePoint.Caster,
            ReferenceOffsetPosition = Vector3.zero,
            ReferenceOffsetRotation = Vector3.zero,
            ReferenceOffsetSmoothTime = 0.25f,
            OrbitEnabled = false,
            OrbitReferencePoint = CameraMotifSO.ReferencePoint.Caster,
            OrbitSpeed = 0f,
            LookAtEnabled = false,
            LookAtReferencePoint = CameraMotifSO.ReferencePoint.Caster
        };

        if (defaultMotif != null)
            baseTuning = ApplyMotif(baseTuning, defaultMotif);

        MotifTuning from = ApplyMotif(baseTuning, previousMotif);
        MotifTuning to = ApplyMotif(baseTuning, activeMotif);
        float t = ResolveMotifBlend();
        return LerpMotifTuning(from, to, t);
    }

    private float ResolveMotifBlend()
    {
        if (!motifBlendActive || motifBlendDuration <= 0.0001f)
            return 1f;

        return Mathf.Clamp01(motifBlendTimer / motifBlendDuration);
    }

    private static MotifTuning ApplyMotif(MotifTuning baseTuning, CameraMotifSO motif)
    {
        if (motif == null)
            return baseTuning;

        baseTuning.ReferencePoint = motif.referencePoint;
        baseTuning.ReferenceOffsetPosition += motif.referenceOffsetPosition;
        if (motif.referenceOffsetSmoothTime >= 0f)
            baseTuning.ReferenceOffsetSmoothTime = motif.referenceOffsetSmoothTime;
        baseTuning.ReferenceOffsetRotation += motif.referenceOffsetRotation;
        baseTuning.OrbitEnabled = motif.orbitEnabled;
        baseTuning.OrbitReferencePoint = motif.orbitReferencePoint;
        baseTuning.OrbitSpeed = motif.orbitSpeed;
        baseTuning.LookAtEnabled = motif.lookAtEnabled;
        baseTuning.LookAtReferencePoint = motif.lookAtReferencePoint;
        return baseTuning;
    }

    private static MotifTuning LerpMotifTuning(MotifTuning from, MotifTuning to, float t)
    {
        t = Mathf.Clamp01(t);
        return new MotifTuning
        {
            ReferencePoint = t < 0.5f ? from.ReferencePoint : to.ReferencePoint,
            ReferenceOffsetPosition = Vector3.Lerp(from.ReferenceOffsetPosition, to.ReferenceOffsetPosition, t),
            ReferenceOffsetRotation = Vector3.Lerp(from.ReferenceOffsetRotation, to.ReferenceOffsetRotation, t),
            ReferenceOffsetSmoothTime = Mathf.Lerp(from.ReferenceOffsetSmoothTime, to.ReferenceOffsetSmoothTime, t),
            OrbitEnabled = t < 0.5f ? from.OrbitEnabled : to.OrbitEnabled,
            OrbitReferencePoint = t < 0.5f ? from.OrbitReferencePoint : to.OrbitReferencePoint,
            OrbitSpeed = Mathf.Lerp(from.OrbitSpeed, to.OrbitSpeed, t),
            LookAtEnabled = t < 0.5f ? from.LookAtEnabled : to.LookAtEnabled,
            LookAtReferencePoint = t < 0.5f ? from.LookAtReferencePoint : to.LookAtReferencePoint
        };
    }

    /// <summary>Indique si le nom fourni correspond à une phase stable de respiration.</summary>
    private float ResolveBreathingPhase(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName))
            return 0f;

        if (breathingPhaseOffsets.TryGetValue(cameraName, out float cachedPhase))
            return cachedPhase;

        int hash = Animator.StringToHash(cameraName);
        float phase = (hash & 0xFFFF) / 65535f * Mathf.PI * 2f;
        breathingPhaseOffsets[cameraName] = phase;
        return phase;
    }

    /// <summary>
    /// Applique un léger mouvement sinusoïdal à la caméra sélectionnée afin de simuler une respiration.
    /// </summary>
    private void ApplyBreathingMotion(CinemachineCamera camera, string cameraName)
    {
        if (!enableBreathingMotion || camera == null)
            return;

        float frequency = Mathf.Max(breathingFrequency, 0.0001f);
        float baseAngle = (Time.time * frequency * Mathf.PI * 2f) + ResolveBreathingPhase(cameraName);

        float sin = Mathf.Sin(baseAngle);
        float cos = Mathf.Cos(baseAngle);

        if (Mathf.Abs(breathingVerticalAmplitude) > 0.0001f || Mathf.Abs(breathingHorizontalAmplitude) > 0.0001f)
        {
            Vector3 offset = Vector3.zero;

            if (Mathf.Abs(breathingVerticalAmplitude) > 0.0001f)
                offset += camera.transform.up * (sin * breathingVerticalAmplitude);

            if (Mathf.Abs(breathingHorizontalAmplitude) > 0.0001f)
                offset += camera.transform.right * (cos * breathingHorizontalAmplitude);

            camera.transform.position += offset;
        }

        if (Mathf.Abs(breathingPitchAmplitude) > 0.0001f || Mathf.Abs(breathingYawAmplitude) > 0.0001f)
        {
            Quaternion rotationOffset = Quaternion.identity;

            if (Mathf.Abs(breathingPitchAmplitude) > 0.0001f)
                rotationOffset = Quaternion.AngleAxis(sin * breathingPitchAmplitude, camera.transform.right) * rotationOffset;

            if (Mathf.Abs(breathingYawAmplitude) > 0.0001f)
                rotationOffset = Quaternion.AngleAxis(cos * breathingYawAmplitude, camera.transform.up) * rotationOffset;

            camera.transform.rotation = rotationOffset * camera.transform.rotation;
        }
    }

    private CameraState GetOrCreateCameraState(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName))
            return null;

        if (!cameraStates.TryGetValue(cameraName, out var state) || state == null)
        {
            state = new CameraState
            {
                SmoothedPosition = Vector3.zero,
                SmoothedRotation = Quaternion.identity
            };
            cameraStates[cameraName] = state;
        }

        return state;
    }

    private void ResetCameraState(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName))
            return;

        if (cameraStates.TryGetValue(cameraName, out var state) && state != null)
            state.Initialized = false;
    }

    private CharacterUnit ResolveReferenceUnit(CameraMotifSO.ReferencePoint referencePoint)
    {
        if (referencePoint == CameraMotifSO.ReferencePoint.Target)
        {
            if (currentTarget != null)
                return currentTarget;
            if (currentCaster != null)
                return currentCaster;
        }
        else
        {
            if (currentCaster != null)
                return currentCaster;
            if (currentTarget != null)
                return currentTarget;
        }

        if (currentTurnOwner != null)
            return currentTurnOwner;

        return null;
    }

    /// <summary>Enregistre l'unité actuellement active (tour en cours).</summary>
    public void SetTurnOwner(CharacterUnit unit, bool alsoSetAsCaster = true)
    {
        currentTurnOwner = unit;
        if (alsoSetAsCaster && unit != null)
            currentCaster = unit;

        BattleCameraDamageFilter.Instance?.SetActiveUnit(unit);
        RefreshAllCameraPlacements();
    }

    /// <summary>Définit l'unité ciblée lors de la sélection ou de l'exécution d'une action.</summary>
    public void SetCurrentTarget(CharacterUnit target)
    {
        currentTarget = target;
        RefreshAllCameraPlacements();
    }

    /// <summary>Configure le contexte complet d'un move ou d'un item.</summary>
    public void ConfigureActionTargets(CharacterUnit caster, CharacterUnit target)
    {
        currentCaster = caster ?? currentCaster;
        currentTarget = target;
        RefreshAllCameraPlacements();
    }

    /// <summary>Efface les informations associées au move en cours.</summary>
    public void ClearRigTargets()
    {
        currentCaster = null;
        currentTarget = null;
        RefreshAllCameraPlacements();
    }

    /// <summary>Active la caméra correspondant au nom fourni.</summary>
    public void SwitchToCamera(
        string cameraName,
        float blendTime = -1f,
        CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (!blendSwitcher)
            return;

        if (string.IsNullOrEmpty(cameraName))
        {
            DisplayCameraWithBlend(null, blendTime, overrideStyle);
            return;
        }

        ResetCameraState(cameraName);
        DisplayCameraWithBlend(cameraName, blendTime, overrideStyle);
    }

    /// <summary>Active la caméra indiquée en respectant la durée/ le style de blend global.</summary>
    private void DisplayCameraWithBlend(
        string cameraName,
        float blendTime,
        CinemachineBlendDefinition.Styles? overrideStyle)
    {
        if (!blendSwitcher)
            return;

        float duration = blendTime >= 0f ? blendTime : SmoothBlendDuration;
        var style = overrideStyle ?? SmoothBlendStyle;
        blendSwitcher.DisplayCamera(cameraName, duration, style);
    }

    /// <summary>Tente de récupérer une <see cref="CinemachineCamera"/> via son nom de GameObject.</summary>
    public bool TryGetCameraByName(string cameraName, out CinemachineCamera camera)
    {
        if (string.IsNullOrEmpty(cameraName))
        {
            camera = null;
            return false;
        }

        if (!cameraByName.TryGetValue(cameraName, out camera) || camera == null)
        {
            RefreshCameraCache();
            cameraByName.TryGetValue(cameraName, out camera);
        }

        return camera != null;
    }

    /// <summary>Renvoie le nom de la Cinemachine actuellement prioritaire (ou <c>null</c>).</summary>
    public string CurrentCinemachineCameraName => blendSwitcher ? blendSwitcher.CurrentCameraName : null;

    /// <summary>Indique si une Cinemachine possède la priorité dans le <see cref="CinemachineBrain"/>.</summary>
    public bool HasActiveCinemachineCamera => blendSwitcher && blendSwitcher.HasActiveCamera;
}
