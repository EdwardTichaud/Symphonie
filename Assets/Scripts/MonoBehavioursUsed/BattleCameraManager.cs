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

    /// <summary>Motif actuellement appliqué par le gestionnaire.</summary>
    private CameraMotifSO currentCameraMotif;

    /// <summary>Motif précédent pour permettre un blend progressif.</summary>
    private CameraMotifSO previousMotif;

    /// <summary>Indique si le motif courant est verrouillé pour la durée d'un move.</summary>
    private bool motifLocked;

    /// <summary>Timer de blend entre motifs.</summary>
    private float motifBlendTimer;

    /// <summary>Durée retenue pour la transition de motif.</summary>
    private float motifBlendDuration = 0.25f;

    /// <summary>Indique si une transition de motif est en cours.</summary>
    private bool motifBlendActive;

    /// <summary>Timestamp de départ pour l'animation du motif actif.</summary>
    private float activeMotifStartTime;

    /// <summary>Temps d'animation figé pour le motif précédent lors d'un blend.</summary>
    private float previousMotifElapsed;

    /// <summary>Timestamp de départ pour l'animation du motif par défaut.</summary>
    private float defaultMotifStartTime;

    /// <summary>Derniere valeur appliquee pour le flou de bordure.</summary>
    private float lastEdgeBlurAmount = -1f;

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
        public Transform ReferenceTransform;
        public CameraMotifSO.ReferencePoint ReferencePoint;
        public float ReferenceDelay;
        public List<ReferenceSample> ReferenceSamples;
        public bool HasBaseFov;
        public float BaseFov;
        public bool WasCompensatingFov;
    }

    private struct MotifTuning
    {
        public CameraMotifSO.ReferencePoint ReferencePoint;
        public Vector3 ReferenceOffsetPosition;
        public Vector3 ReferenceOffsetRotation;
        public float ReferenceOffsetSmoothTime;
        public float ReferenceOffsetDelay;
        public bool OrbitEnabled;
        public CameraMotifSO.ReferencePoint OrbitReferencePoint;
        public float OrbitSpeed;
        public bool LookAtEnabled;
        public CameraMotifSO.ReferencePoint LookAtReferencePoint;
        public float EdgeBlurAmount;
        public bool CompensateReferenceSize;
        public float MaxCompensationFovIncrease;
    }

    private struct ReferenceSample
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private static readonly Vector3[] BoundsCornerSigns =
    {
        new Vector3(-1f, -1f, -1f),
        new Vector3(-1f, -1f, 1f),
        new Vector3(-1f, 1f, -1f),
        new Vector3(-1f, 1f, 1f),
        new Vector3(1f, -1f, -1f),
        new Vector3(1f, -1f, 1f),
        new Vector3(1f, 1f, -1f),
        new Vector3(1f, 1f, 1f)
    };
    private const float DefaultCompensationFovIncrease = 12f;
    private const float CompensationFovSpeed = 120f;
    private const float CompensationFovEpsilon = 0.1f;

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

        float now = Time.unscaledTime;
        activeMotifStartTime = now;
        defaultMotifStartTime = now;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        cameraByName.Clear();
        cameraStates.Clear();
        breathingPhaseOffsets.Clear();
        PostProcessManager.Instance?.SetEdgeBlur(0f);
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

        CameraState state = GetOrCreateCameraState(cameraName, camera);
        if (state == null)
            return false;

        MotifTuning tuning = ResolveMotifTuning();
        ApplyEdgeBlur(tuning.EdgeBlurAmount);
        CharacterUnit referenceUnit = ResolveReferenceUnit(tuning.ReferencePoint);
        if (referenceUnit == null)
        {
            ApplyReferenceSizeCompensation(
                camera,
                state,
                null,
                false,
                tuning.MaxCompensationFovIncrease);
            return false;
        }

        Transform referenceTransform = referenceUnit.transform;

        Vector3 referencePosition = referenceTransform.position;
        Quaternion referenceRotation = referenceTransform.rotation;
        ResolveDelayedReference(state, referenceTransform, tuning.ReferencePoint, tuning.ReferenceOffsetDelay, ref referencePosition, ref referenceRotation);

        Vector3 offsetWorld = referenceRotation * tuning.ReferenceOffsetPosition;
        Vector3 desiredPosition = referencePosition + offsetWorld;
        Quaternion desiredRotation = referenceRotation * Quaternion.Euler(tuning.ReferenceOffsetRotation);

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
                if (tuning.CompensateReferenceSize)
                    lookAtPosition = lookAtUnit.GetVisualBounds().center;
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
        ApplyReferenceSizeCompensation(
            camera,
            state,
            referenceUnit,
            tuning.CompensateReferenceSize,
            tuning.MaxCompensationFovIncrease);
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
        if (motifLocked && motif != activeMotif)
            return;

        if (motif == activeMotif && !motifBlendActive)
            return;

        previousMotifElapsed = ResolveMotifElapsed(activeMotif, activeMotifStartTime);
        previousMotif = activeMotif;
        activeMotif = motif;
        currentCameraMotif = motif;
        if (activeMotif != previousMotif)
            activeMotifStartTime = Time.unscaledTime;

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

    /// <summary>Active et verrouille un motif jusqu'à la fin explicite d'un move.</summary>
    public void LockCameraMotif(CameraMotifSO motif, float blendDuration = -1f)
    {
        if (motif != null)
            SetCameraMotif(motif, blendDuration);

        motifLocked = motif != null;
    }

    /// <summary>Déverrouille le motif pour autoriser un changement explicite.</summary>
    public void UnlockCameraMotif()
    {
        motifLocked = false;
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
            ReferenceOffsetDelay = 0f,
            OrbitEnabled = false,
            OrbitReferencePoint = CameraMotifSO.ReferencePoint.Caster,
            OrbitSpeed = 0f,
            LookAtEnabled = false,
            LookAtReferencePoint = CameraMotifSO.ReferencePoint.Caster,
            EdgeBlurAmount = 0f,
            CompensateReferenceSize = false,
            MaxCompensationFovIncrease = DefaultCompensationFovIncrease
        };

        if (defaultMotif != null)
            baseTuning = ApplyMotif(baseTuning, defaultMotif, ResolveMotifElapsed(defaultMotif, defaultMotifStartTime));

        MotifTuning from = ApplyMotif(baseTuning, previousMotif, previousMotifElapsed);
        MotifTuning to = ApplyMotif(baseTuning, activeMotif, ResolveMotifElapsed(activeMotif, activeMotifStartTime));
        float t = ResolveMotifBlend();
        return LerpMotifTuning(from, to, t);
    }

    private float ResolveMotifBlend()
    {
        if (!motifBlendActive || motifBlendDuration <= 0.0001f)
            return 1f;

        return Mathf.Clamp01(motifBlendTimer / motifBlendDuration);
    }

    private static MotifTuning ApplyMotif(MotifTuning baseTuning, CameraMotifSO motif, float motifElapsed)
    {
        if (motif == null)
            return baseTuning;

        baseTuning.ReferencePoint = motif.referencePoint;
        baseTuning.ReferenceOffsetPosition += motif.referenceOffsetPosition;
        if (motif.referenceOffsetSmoothTime >= 0f)
            baseTuning.ReferenceOffsetSmoothTime = motif.referenceOffsetSmoothTime;
        baseTuning.ReferenceOffsetDelay = Mathf.Max(0f, motif.referenceOffsetDelay);
        baseTuning.ReferenceOffsetRotation += motif.referenceOffsetRotation;
        baseTuning.OrbitEnabled = motif.orbitEnabled;
        baseTuning.OrbitReferencePoint = motif.orbitReferencePoint;
        baseTuning.OrbitSpeed = motif.orbitSpeed;
        baseTuning.LookAtEnabled = motif.lookAtEnabled;
        baseTuning.LookAtReferencePoint = motif.lookAtReferencePoint;
        baseTuning.CompensateReferenceSize = motif.compensateReferenceSize;
        baseTuning.MaxCompensationFovIncrease = Mathf.Max(0f, motif.maxCompensationFovIncrease);
        baseTuning.EdgeBlurAmount = Mathf.Clamp01(
            baseTuning.EdgeBlurAmount + Mathf.Max(0f, motif.edgeBlurAmount));

        if (TryResolveAnimationTime(motif, motifElapsed, out float animationTime))
        {
            baseTuning.ReferenceOffsetPosition += EvaluateVector3Curves(
                motif.referenceOffsetPositionX,
                motif.referenceOffsetPositionY,
                motif.referenceOffsetPositionZ,
                animationTime);
            baseTuning.ReferenceOffsetRotation += EvaluateVector3Curves(
                motif.referenceOffsetRotationX,
                motif.referenceOffsetRotationY,
                motif.referenceOffsetRotationZ,
                animationTime);
            baseTuning.ReferenceOffsetSmoothTime += EvaluateCurve(motif.referenceOffsetSmoothTimeCurve, animationTime);
            baseTuning.ReferenceOffsetDelay = Mathf.Max(
                0f,
                baseTuning.ReferenceOffsetDelay + EvaluateCurve(motif.referenceOffsetDelayCurve, animationTime));
            baseTuning.OrbitSpeed += EvaluateCurve(motif.orbitSpeedCurve, animationTime);
            baseTuning.EdgeBlurAmount = Mathf.Clamp01(
                baseTuning.EdgeBlurAmount + EvaluateCurve(motif.edgeBlurAmountCurve, animationTime));
        }
        return baseTuning;
    }

    private static float ResolveMotifElapsed(CameraMotifSO motif, float startTime)
    {
        if (motif == null)
            return 0f;

        return Mathf.Max(0f, Time.unscaledTime - startTime);
    }

    private static bool TryResolveAnimationTime(CameraMotifSO motif, float motifElapsed, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (motif == null || !motif.animate || motif.animationDuration <= 0f)
            return false;

        float duration = motif.animationDuration;
        float elapsed = motif.loopAnimation
            ? Mathf.Repeat(motifElapsed, duration)
            : Mathf.Min(motifElapsed, duration);
        normalizedTime = duration > 0f ? elapsed / duration : 0f;
        return true;
    }

    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        return curve != null && curve.length > 0 ? curve.Evaluate(t) : 0f;
    }

    private static Vector3 EvaluateVector3Curves(AnimationCurve xCurve, AnimationCurve yCurve, AnimationCurve zCurve, float t)
    {
        return new Vector3(
            EvaluateCurve(xCurve, t),
            EvaluateCurve(yCurve, t),
            EvaluateCurve(zCurve, t));
    }

    private void ApplyEdgeBlur(float amount)
    {
        amount = Mathf.Clamp01(amount);
        PostProcessManager postProcess = PostProcessManager.Instance;
        if (postProcess == null)
            return;

        if (Mathf.Abs(amount - lastEdgeBlurAmount) < 0.001f)
            return;

        lastEdgeBlurAmount = amount;
        postProcess.SetEdgeBlur(amount);
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
            ReferenceOffsetDelay = Mathf.Lerp(from.ReferenceOffsetDelay, to.ReferenceOffsetDelay, t),
            OrbitEnabled = t < 0.5f ? from.OrbitEnabled : to.OrbitEnabled,
            OrbitReferencePoint = t < 0.5f ? from.OrbitReferencePoint : to.OrbitReferencePoint,
            OrbitSpeed = Mathf.Lerp(from.OrbitSpeed, to.OrbitSpeed, t),
            LookAtEnabled = t < 0.5f ? from.LookAtEnabled : to.LookAtEnabled,
            LookAtReferencePoint = t < 0.5f ? from.LookAtReferencePoint : to.LookAtReferencePoint,
            EdgeBlurAmount = Mathf.Lerp(from.EdgeBlurAmount, to.EdgeBlurAmount, t),
            CompensateReferenceSize = t < 0.5f ? from.CompensateReferenceSize : to.CompensateReferenceSize,
            MaxCompensationFovIncrease = Mathf.Lerp(from.MaxCompensationFovIncrease, to.MaxCompensationFovIncrease, t)
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

    private void ApplyReferenceSizeCompensation(
        CinemachineCamera camera,
        CameraState state,
        CharacterUnit referenceUnit,
        bool compensate,
        float maxIncreaseLimit)
    {
        if (camera == null || state == null)
            return;

        if (!state.HasBaseFov)
        {
            state.HasBaseFov = true;
            state.BaseFov = camera.Lens.FieldOfView;
        }

        if (!compensate || referenceUnit == null)
        {
            if (state.WasCompensatingFov)
                RestoreBaseFov(camera, state);
            state.WasCompensatingFov = false;
            return;
        }

        var lens = camera.Lens;
        if (lens.Orthographic)
        {
            if (state.WasCompensatingFov)
                RestoreBaseFov(camera, state);
            state.WasCompensatingFov = false;
            return;
        }

        Bounds bounds = referenceUnit.GetVisualBounds();
        if (!TryComputeRequiredFov(camera.transform, bounds, lens.Aspect, out float requiredFov))
        {
            if (state.WasCompensatingFov)
                RestoreBaseFov(camera, state);
            state.WasCompensatingFov = false;
            return;
        }

        float maxIncrease = Mathf.Max(0f, maxIncreaseLimit);
        float maxFov = state.BaseFov + maxIncrease;
        float targetFov = Mathf.Min(Mathf.Max(state.BaseFov, requiredFov), maxFov);

        if (targetFov <= state.BaseFov + CompensationFovEpsilon)
        {
            if (state.WasCompensatingFov)
                RestoreBaseFov(camera, state);
            state.WasCompensatingFov = false;
            return;
        }

        float newFov = Mathf.MoveTowards(lens.FieldOfView, targetFov, CompensationFovSpeed * Time.deltaTime);
        if (Mathf.Abs(lens.FieldOfView - newFov) > 0.001f)
        {
            lens.FieldOfView = newFov;
            camera.Lens = lens;
        }
        state.WasCompensatingFov = true;
    }

    private void RestoreBaseFov(CinemachineCamera camera, CameraState state)
    {
        if (camera == null || state == null || !state.HasBaseFov)
            return;

        var lens = camera.Lens;
        float newFov = Mathf.MoveTowards(lens.FieldOfView, state.BaseFov, CompensationFovSpeed * Time.deltaTime);
        if (Mathf.Abs(lens.FieldOfView - newFov) < 0.001f)
            return;

        lens.FieldOfView = newFov;
        camera.Lens = lens;
    }

    private static bool TryComputeRequiredFov(
        Transform cameraTransform,
        Bounds bounds,
        float aspect,
        out float requiredFov)
    {
        requiredFov = 0f;
        if (cameraTransform == null)
            return false;

        aspect = Mathf.Max(0.0001f, aspect);

        float maxAngleX = 0f;
        float maxAngleY = 0f;
        int validPoints = 0;

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int i = 0; i < BoundsCornerSigns.Length; i++)
        {
            Vector3 corner = center + Vector3.Scale(extents, BoundsCornerSigns[i]);
            Vector3 local = cameraTransform.InverseTransformPoint(corner);
            if (local.z <= 0.001f)
                continue;

            validPoints++;
            float angleX = Mathf.Atan2(Mathf.Abs(local.x), local.z);
            float angleY = Mathf.Atan2(Mathf.Abs(local.y), local.z);
            if (angleX > maxAngleX)
                maxAngleX = angleX;
            if (angleY > maxAngleY)
                maxAngleY = angleY;
        }

        if (validPoints == 0)
            return false;

        float halfFromX = Mathf.Atan(Mathf.Tan(maxAngleX) / aspect);
        float requiredHalf = Mathf.Max(maxAngleY, halfFromX);
        requiredFov = Mathf.Clamp(requiredHalf * 2f * Mathf.Rad2Deg, 1f, 179f);
        return true;
    }

    private CameraState GetOrCreateCameraState(string cameraName, CinemachineCamera camera)
    {
        if (string.IsNullOrEmpty(cameraName))
            return null;

        if (!cameraStates.TryGetValue(cameraName, out var state) || state == null)
        {
            state = new CameraState
            {
                SmoothedPosition = Vector3.zero,
                SmoothedRotation = Quaternion.identity,
                ReferenceSamples = new List<ReferenceSample>(32)
            };
            cameraStates[cameraName] = state;
        }

        if (camera != null && !state.HasBaseFov)
        {
            state.HasBaseFov = true;
            state.BaseFov = camera.Lens.FieldOfView;
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

    private static void ResolveDelayedReference(
        CameraState state,
        Transform referenceTransform,
        CameraMotifSO.ReferencePoint referencePoint,
        float delay,
        ref Vector3 position,
        ref Quaternion rotation)
    {
        delay = Mathf.Max(0f, delay);
        if (delay <= 0f)
        {
            if (state.ReferenceSamples != null && state.ReferenceSamples.Count > 0)
                state.ReferenceSamples.Clear();
            state.ReferenceTransform = referenceTransform;
            state.ReferencePoint = referencePoint;
            state.ReferenceDelay = delay;
            return;
        }

        bool resetBuffer = state.ReferenceTransform != referenceTransform
            || state.ReferencePoint != referencePoint;

        if (state.ReferenceSamples == null)
            state.ReferenceSamples = new List<ReferenceSample>(32);

        if (resetBuffer)
        {
            state.ReferenceSamples.Clear();
            state.ReferenceTransform = referenceTransform;
            state.ReferencePoint = referencePoint;
        }
        state.ReferenceDelay = delay;

        float now = Time.time;
        state.ReferenceSamples.Add(new ReferenceSample
        {
            Time = now,
            Position = position,
            Rotation = rotation
        });

        float targetTime = now - delay;
        while (state.ReferenceSamples.Count >= 2 && state.ReferenceSamples[1].Time <= targetTime)
            state.ReferenceSamples.RemoveAt(0);

        if (state.ReferenceSamples.Count == 0)
            return;

        if (state.ReferenceSamples.Count == 1)
        {
            position = state.ReferenceSamples[0].Position;
            rotation = state.ReferenceSamples[0].Rotation;
            return;
        }

        ReferenceSample sampleA = state.ReferenceSamples[0];
        ReferenceSample sampleB = state.ReferenceSamples[1];

        if (targetTime <= sampleA.Time)
        {
            position = sampleA.Position;
            rotation = sampleA.Rotation;
            return;
        }

        if (targetTime >= sampleB.Time)
        {
            position = sampleB.Position;
            rotation = sampleB.Rotation;
            return;
        }

        float t = Mathf.InverseLerp(sampleA.Time, sampleB.Time, targetTime);
        position = Vector3.Lerp(sampleA.Position, sampleB.Position, t);
        rotation = Quaternion.Slerp(sampleA.Rotation, sampleB.Rotation, t);
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

    /// <summary>Expose le motif actuellement appliqué par le gestionnaire.</summary>
    public CameraMotifSO CurrentCameraMotif => currentCameraMotif;
}
