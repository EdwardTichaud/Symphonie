using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Prépare et anime les différents points de vue cinématiques utilisés pendant les combats.
/// L'objectif est de reproduire une grammaire proche de Clair Obscur: Expedition 33
/// avec des plans identifiés (épaule, réaction, travelling projectile, etc.).
/// </summary>
[DisallowMultipleComponent]
public class BattleCameraRig : MonoBehaviour
{
    // ------------------------------------------------------------------------------
    // Configuration générale
    // ------------------------------------------------------------------------------
    [Header("Général")]
    [Tooltip("Vitesse globale d'interpolation utilisée pour lisser les déplacements de caméra.")]
    [SerializeField] private float baseLerpSpeed = 6f;

    [Tooltip("Facteur appliqué aux interpolations d'angle pour éviter les rotations brusques.")]
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Tooltip("Distance minimale utilisée pour éviter les divisions par zéro lors des calculs directionnels.")]
    [SerializeField] private float epsilon = 0.0001f;

    [Header("Points de focus par défaut")]
    [SerializeField, Tooltip("Hauteur de référence utilisée lorsque le lanceur ne fournit pas d'ancrage caméra dédié.")]
    private float defaultCasterFocusHeight = 1.6f;

    [SerializeField, Tooltip("Hauteur de référence pour la cible lorsque aucun point précis n'est communiqué.")]
    private float defaultTargetFocusHeight = 1.5f;

    [Header("Bruit cinématique (attaques)")]
    [SerializeField, Tooltip("Amplitude du bruit positionnel appliqué aux plans d'attaque rapprochés (en mètres).")]
    private float attackNoiseAmplitude = 0.12f;

    [SerializeField, Tooltip("Facteur appliqué sur l'axe vertical afin de conserver un mouvement plus subtil.")]
    private float attackNoiseVerticalMultiplier = 0.45f;

    [SerializeField, Tooltip("Fréquence du bruit cinématique appliqué lors des attaques (en oscillations par seconde).")]
    private float attackNoiseFrequency = 2.2f;

    [SerializeField, Tooltip("Amplitude maximale du bruit de rotation (en degrés) pour renforcer l'impact visuel.")]
    private float attackNoiseRotationAmplitude = 0.9f;

    [Header("Main Menu Idle")]
    [SerializeField, Tooltip("Distance orbitale du plan d'attente autour du caster.")]
    private float idleOrbitRadius = 3.5f;

    [SerializeField, Tooltip("Vitesse de l'orbite micro autour du point visé.")]
    private float idleOrbitSpeed = 0.35f;

    [SerializeField, Tooltip("Hauteur du point focal pendant le plan d'attente.")]
    private float idleFocusHeight = 1.6f;

    [SerializeField, Tooltip("Amplitude (en degrés) de la respiration de FOV du plan d'attente.")]
    private float idleFovAmplitude = 2.5f;

    [SerializeField, Tooltip("Vitesse de la respiration de FOV du plan d'attente.")]
    private float idleFovSpeed = 0.75f;

    [SerializeField, Tooltip("Distance radiale de base avant application de l'orbite.")]
    private float idleBaseDistance = 2.25f;

    [Header("Over Shoulder")]
    [SerializeField] private float shoulderHeight = 1.55f;
    [SerializeField] private float shoulderDistance = 2.6f;
    [SerializeField] private float shoulderSideOffset = 0.65f;
    [SerializeField] private float shoulderLookHeight = 1.45f;

    [Header("Close Push Caster")]
    [SerializeField] private float pushHeight = 1.7f;
    [SerializeField] private float pushDistance = 1.1f;
    [SerializeField] private float pushSideSwing = 0.35f;
    [SerializeField] private float pushFov = 42f;

    [Header("Target Reaction")]
    [SerializeField] private float reactionHeight = 1.4f;
    [SerializeField] private float reactionDistance = 2.2f;
    [SerializeField] private float reactionFov = 48f;

    [Header("Wide Establish")] 
    [SerializeField] private float wideHeight = 4f;
    [SerializeField] private float wideDepth = 9f;
    [SerializeField] private float wideSide = 1.25f;
    [SerializeField] private float wideFov = 55f;
    [SerializeField] private float wideCasterWeight = 0.6f;
    [SerializeField] private float wideTargetWeight = 0.4f;

    [Header("Projectile Flyby")]
    [SerializeField] private float projectileHeight = 1.8f;
    [SerializeField] private float projectileOffset = 4f;
    [SerializeField] private float projectileSpeed = 1.35f;
    [SerializeField] private float projectileSideAmplitude = 0.65f;
    [SerializeField] private float projectileSideFrequency = 1.2f;
    [SerializeField] private float projectileFocusHeight = 1.5f;

    [Header("Victory")] 
    [SerializeField] private float victoryHeight = 2.4f;
    [SerializeField] private float victoryDistance = 3.5f;
    [SerializeField] private float victoryFov = 38f;
    [SerializeField] private float victoryTilt = 12f;

    // ------------------------------------------------------------------------------
    // Etat interne
    // ------------------------------------------------------------------------------
    private readonly Dictionary<BattleCameraRole, CinemachineCamera> cameraByRole = new();
    private readonly Dictionary<BattleCameraRole, string> legacyNames = new();

    // Les dictionnaires ci-dessous mémorisent les vitesses intermédiaires utilisées par SmoothDamp.
    // Sans cette persistance explicite, chaque appel repartirait de zéro et générerait des à-coups visibles
    // dès que les cibles changent brutalement de position.
    private readonly Dictionary<CinemachineCamera, Vector3> positionVelocities = new();
    private readonly Dictionary<CinemachineCamera, Vector3> rotationVelocities = new();

    private CinemachineTargetGroup targetGroup;
    private Transform midAnchor;
    private Transform caster;
    private Transform target;
    private Transform casterFocusAnchor;
    private Transform targetFocusAnchor;
    private Vector3? manualMidpoint;

    // Références mémorisées pour retirer proprement les membres du CinemachineTargetGroup.
    private Transform activeCasterGroupMember;
    private Transform activeTargetGroupMember;
    private Transform activeMidGroupMember;

    private BattleCameraRole currentActiveRole = BattleCameraRole.None;
    private float projectileTimer;
    private Vector3 attackNoiseSeed;

    private static readonly Dictionary<string, BattleCameraRole> NameToRole = new()
    {
        {"CMV_MainMenuIdle", BattleCameraRole.MainMenuIdle},
        {"CMV_OverShoulder_CasterToTarget", BattleCameraRole.OverShoulderCasterToTarget},
        {"CMV_ClosePush_Caster", BattleCameraRole.ClosePushCaster},
        {"CMV_TargetReaction", BattleCameraRole.TargetReaction},
        {"CMV_WideEstablish", BattleCameraRole.WideEstablish},
        {"CMV_Projectile_Flyby", BattleCameraRole.ProjectileFlyby},
        {"CMV_Victory", BattleCameraRole.Victory}
    };

    /// <summary>
    /// Permet d'accéder au groupe de cibles utilisé par le plan large.
    /// Exposé afin que le BattleCameraManager puisse l'alimenter si nécessaire.
    /// </summary>
    public CinemachineTargetGroup TargetGroup => targetGroup;

    private void Awake()
    {
        CacheCameras();
        PrepareTargetGroup();
        PrepareMidAnchor();
        PrepareImpulseListener();
        attackNoiseSeed = new Vector3(
            UnityEngine.Random.Range(0f, 1000f),
            UnityEngine.Random.Range(0f, 1000f),
            UnityEngine.Random.Range(0f, 1000f));
    }

    private void CacheCameras()
    {
        cameraByRole.Clear();
        legacyNames.Clear();

        foreach (var cam in GetComponentsInChildren<CinemachineCamera>(true))
        {
            if (!NameToRole.TryGetValue(cam.gameObject.name, out var role))
                continue;

            cameraByRole[role] = cam;
            legacyNames[role] = cam.gameObject.name;
            DisableLegacyControllers(cam);

            // Chaque re-cache invalide les vitesses stockées précédemment : sans cette remise à zéro,
            // une caméra réassignée conserverait une inertie obsolète et réintroduirait du jitter.
            ResetCameraSmoothing(cam);
        }
    }

    private void PrepareTargetGroup()
    {
        targetGroup = GetComponentInChildren<CinemachineTargetGroup>();
        if (!targetGroup)
        {
            var go = new GameObject("CombatTargetGroup");
            go.transform.SetParent(transform, false);
            targetGroup = go.AddComponent<CinemachineTargetGroup>();
        }

        // Initialisation : on purge les membres pour éviter les doublons lors des recharges de scène.
        ClearTargetGroupMembers();
    }

    private void PrepareMidAnchor()
    {
        var existing = transform.Find("DynamicMidPoint");
        if (existing != null)
            midAnchor = existing;
        else
        {
            var go = new GameObject("DynamicMidPoint");
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSave;
            midAnchor = go.transform;
        }
    }

    private void PrepareImpulseListener()
    {
        var brain = Camera.main ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
        if (brain == null) return;

        var ext = brain.GetComponent<Unity.Cinemachine.CinemachineExternalImpulseListener>();
        if (ext == null)
            ext = brain.gameObject.AddComponent<Unity.Cinemachine.CinemachineExternalImpulseListener>();

        ext.Gain = 1f;

        var reaction = ext.ReactionSettings;    // remplace m_ReactionSettings
        reaction.AmplitudeGain = 0.85f;
        reaction.FrequencyGain = 1f;
        reaction.Duration = 0.12f + 0.45f;
        ext.ReactionSettings = reaction;
    }

    private void DisableLegacyControllers(CinemachineCamera cam)
    {
        // Les anciens contrôleurs dédiés au prototype initial ont été supprimés du projet.
        // On conserve néanmoins la méthode afin de documenter l'intention : toute caméra instanciée par le rig
        // ne doit plus embarquer de MonoBehaviours hérités des prototypes initiaux. Si un composant obsolète est
        // réintroduit par erreur dans l'éditeur, le simple fait de garder ce point d'extension permettra de
        // réimplémenter facilement la désactivation ciblée sans toucher au reste du pipeline caméra.
        if (!cam)
        {
            return; // Sécurité supplémentaire : les appels protecteurs évitent les NullReference en cas d'appel inattendu.
        }
    }

    /// <summary>
    /// Configure les cibles suivies par le rig.
    /// </summary>
    public void ConfigureTargets(
        CharacterUnit casterUnit,
        CharacterUnit targetUnit,
        Vector3? midpointOverride = null,
        Transform casterAnchor = null,
        Transform targetAnchor = null)
    {
        caster = casterUnit ? casterUnit.transform : null;
        target = targetUnit ? targetUnit.transform : null;
        casterFocusAnchor = casterAnchor;
        targetFocusAnchor = targetAnchor;
        manualMidpoint = midpointOverride;

        UpdateTargetGroupMembers();
    }

    /// <summary>
    /// Efface les références de cibles lorsque l'action est terminée.
    /// </summary>
    public void ClearTargets()
    {
        caster = null;
        target = null;
        manualMidpoint = null;
        casterFocusAnchor = null;
        targetFocusAnchor = null;
        UpdateTargetGroupMembers();
    }

    private void UpdateTargetGroupMembers()
    {
        if (!targetGroup)
            return;

        ClearTargetGroupMembers();

        Transform casterMember = casterFocusAnchor ? casterFocusAnchor : caster;
        if (casterMember)
        {
            targetGroup.AddMember(casterMember, wideCasterWeight, 0.5f);
            activeCasterGroupMember = casterMember;
        }

        Transform targetMember = targetFocusAnchor ? targetFocusAnchor : target;
        if (targetMember)
        {
            targetGroup.AddMember(targetMember, wideTargetWeight, 0.5f);
            activeTargetGroupMember = targetMember;
        }

        if (manualMidpoint.HasValue)
        {
            midAnchor.position = manualMidpoint.Value;
            targetGroup.AddMember(midAnchor, 0.2f, 0.1f);
            activeMidGroupMember = midAnchor;
        }
    }

    /// <summary>
    /// Retourne la caméra associée à un rôle donné.
    /// </summary>
    public bool TryGetCamera(BattleCameraRole role, out CinemachineCamera camera)
        => cameraByRole.TryGetValue(role, out camera);

    /// <summary>
    /// Retourne le nom d'origine de la caméra (utile pour le BlendSwitcher).
    /// </summary>
    public bool TryGetCameraName(BattleCameraRole role, out string cameraName)
        => legacyNames.TryGetValue(role, out cameraName);

    /// <summary>
    /// Notifie le rig du rôle actuellement prioritaire afin d'adapter certains effets.
    /// </summary>
    public void NotifyActiveRole(BattleCameraRole role)
    {
        currentActiveRole = role;
        if (role == BattleCameraRole.ProjectileFlyby)
            projectileTimer = 0f; // Ré-initialise le travelling à chaque activation.
    }

    private void ClearTargetGroupMembers()
    {
        if (!targetGroup)
            return;

        // Retire systématiquement les anciennes références avant de réinjecter les cibles.
        if (activeCasterGroupMember)
        {
            targetGroup.RemoveMember(activeCasterGroupMember);
            activeCasterGroupMember = null;
        }

        if (activeTargetGroupMember)
        {
            targetGroup.RemoveMember(activeTargetGroupMember);
            activeTargetGroupMember = null;
        }

        if (activeMidGroupMember)
        {
            targetGroup.RemoveMember(activeMidGroupMember);
            activeMidGroupMember = null;
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateIdleCamera(dt);
        UpdateOverShoulderCamera(dt);
        UpdateClosePushCamera(dt);
        UpdateTargetReactionCamera(dt);
        UpdateWideCamera(dt);
        UpdateProjectileCamera(dt);
        UpdateVictoryCamera(dt);
    }

    private Vector3 GetCasterFocusBase()
    {
        if (casterFocusAnchor)
            return casterFocusAnchor.position;

        if (caster)
            return caster.position + Vector3.up * defaultCasterFocusHeight;

        return transform.position + Vector3.up * defaultCasterFocusHeight;
    }

    private Vector3 GetTargetFocusBase()
    {
        if (targetFocusAnchor)
            return targetFocusAnchor.position;

        if (target)
            return target.position + Vector3.up * defaultTargetFocusHeight;

        return GetCasterFocusBase();
    }

    private Vector3 GetCasterFocusPosition()
        => GetCasterFocusBase();

    private Vector3 GetCasterFocusPosition(float desiredHeight)
        => GetCasterFocusBase() + Vector3.up * (desiredHeight - defaultCasterFocusHeight);

    private Vector3 GetTargetFocusPosition()
        => GetTargetFocusBase();

    private Vector3 GetTargetFocusPosition(float desiredHeight)
        => GetTargetFocusBase() + Vector3.up * (desiredHeight - defaultTargetFocusHeight);

    private Vector3 GetWeightedCenter()
    {
        bool hasCaster = caster || casterFocusAnchor;
        bool hasTarget = target || targetFocusAnchor;

        if (!hasCaster && !hasTarget)
            return transform.position;

        Vector3 casterPos = GetCasterFocusPosition();
        Vector3 targetPos = GetTargetFocusPosition();

        if (hasCaster && hasTarget)
        {
            float totalWeight = Mathf.Max(0.001f, wideCasterWeight + wideTargetWeight);
            return (casterPos * wideCasterWeight + targetPos * wideTargetWeight) / totalWeight;
        }

        return hasCaster ? casterPos : targetPos;
    }

    private void UpdateIdleCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.MainMenuIdle, out var cam))
            return;

        Vector3 anchorPos = GetCasterFocusPosition(idleFocusHeight);
        float orbitAngle = Time.time * idleOrbitSpeed;

        Vector3 orbitOffset = new(
            Mathf.Cos(orbitAngle) * idleOrbitRadius,
            0f,
            Mathf.Sin(orbitAngle) * idleOrbitRadius);

        Vector3 direction = (anchorPos - cam.transform.position).normalized;
        if (direction.sqrMagnitude < epsilon)
            direction = Vector3.forward;

        Vector3 baseOffset = -direction * idleBaseDistance;
        Vector3 desiredPos = anchorPos + orbitOffset + baseOffset;

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.5f);
        SmoothLookAt(cam, anchorPos, dt, rotationLerpSpeed * 0.5f);

        float targetFov = 50f + Mathf.Sin(Time.time * idleFovSpeed) * idleFovAmplitude;
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, targetFov, dt * 0.5f);
    }

    private void UpdateOverShoulderCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.OverShoulderCasterToTarget, out var cam) || !caster)
            return;

        Vector3 casterFocus = GetCasterFocusPosition(shoulderHeight);
        Vector3 targetFocus = GetTargetFocusPosition(shoulderLookHeight);

        Vector3 focusDir = targetFocus - casterFocus;
        if (focusDir.sqrMagnitude < epsilon)
            focusDir = caster.forward;
        Vector3 normalizedDir = focusDir.normalized;

        Vector3 side = Vector3.Cross(Vector3.up, normalizedDir).normalized;
        Vector3 desiredPos = casterFocus - normalizedDir * shoulderDistance + side * shoulderSideOffset;
        Vector3 lookPos = targetFocus;

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed);
        SmoothLookAt(cam, lookPos, dt, rotationLerpSpeed);
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, 52f, dt * 0.8f);
    }

    private void UpdateClosePushCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.ClosePushCaster, out var cam) || !caster)
            return;

        Vector3 casterFocus = GetCasterFocusPosition(pushHeight);
        Vector3 targetFocus = GetTargetFocusPosition();
        Vector3 toTarget = targetFocus - casterFocus;
        if (toTarget.sqrMagnitude < epsilon)
            toTarget = caster.forward;
        Vector3 direction = toTarget.normalized;

        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 desiredPos = casterFocus - direction * pushDistance + side * pushSideSwing;
        desiredPos = ApplyAttackPositionNoise(desiredPos, BattleCameraRole.ClosePushCaster);

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 1.1f);

        Vector3 lookPos = GetCasterFocusPosition(pushHeight + 0.2f) + direction * 0.15f;
        SmoothLookAt(cam, lookPos, dt, rotationLerpSpeed * 1.2f, GetAttackRotationNoise(BattleCameraRole.ClosePushCaster));
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, pushFov, dt * 1.5f);
    }

    private void UpdateTargetReactionCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.TargetReaction, out var cam) || !target)
            return;

        Vector3 targetFocus = GetTargetFocusPosition(reactionHeight);
        Vector3 fromCaster = targetFocus - GetCasterFocusPosition();
        if (fromCaster.sqrMagnitude < epsilon)
            fromCaster = target.forward.sqrMagnitude > epsilon ? -target.forward : Vector3.back;
        Vector3 direction = fromCaster.normalized;

        Vector3 desiredPos = targetFocus + direction * reactionDistance;
        desiredPos = ApplyAttackPositionNoise(desiredPos, BattleCameraRole.TargetReaction);

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.9f);
        Vector3 lookPos = GetTargetFocusPosition(reactionHeight + 0.1f);
        SmoothLookAt(cam, lookPos, dt, rotationLerpSpeed, GetAttackRotationNoise(BattleCameraRole.TargetReaction));
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, reactionFov, dt * 0.9f);
    }

    private void UpdateWideCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.WideEstablish, out var cam))
            return;

        Vector3 center = GetWeightedCenter();
        Vector3 separation = GetTargetFocusPosition() - GetCasterFocusPosition();
        if (separation.sqrMagnitude < epsilon)
            separation = caster ? caster.forward : Vector3.forward;

        Vector3 forward = separation.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 desiredPos = center - forward * wideDepth + side * wideSide + Vector3.up * wideHeight;
        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.6f);
        SmoothLookAt(cam, center + Vector3.up * 1.2f, dt, rotationLerpSpeed * 0.6f);
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, wideFov, dt * 0.5f);
    }

    private void UpdateProjectileCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.ProjectileFlyby, out var cam) || !caster || !target)
            return;

        if (currentActiveRole == BattleCameraRole.ProjectileFlyby)
            projectileTimer += dt * projectileSpeed;
        else
            projectileTimer = Mathf.Max(0f, projectileTimer - dt * projectileSpeed);

        float t = Mathf.Clamp01(projectileTimer);
        Vector3 casterPos = GetCasterFocusPosition(projectileHeight);
        Vector3 targetPos = GetTargetFocusPosition(projectileHeight);
        Vector3 direction = (targetPos - casterPos);
        if (direction.sqrMagnitude < epsilon)
            direction = caster.forward;
        Vector3 forward = direction.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 start = casterPos - forward * projectileOffset;
        Vector3 end = targetPos + forward * projectileOffset;
        Vector3 lerp = Vector3.Lerp(start, end, t);
        float sine = Mathf.Sin(projectileTimer * projectileSideFrequency) * projectileSideAmplitude;
        Vector3 desiredPos = lerp + side * sine;

        // Le plan travelling doit conserver un contrôle direct sur la trajectoire pour respecter la vitesse
        // du projectile. On impose donc la position immédiatement tout en purgeant les vitesses mémorisées
        // afin que les prochains appels SmoothDamp repartent d'un état propre.
        cam.transform.position = desiredPos;
        ResetPositionSmoothing(cam);
        Vector3 midFocus = Vector3.Lerp(
            GetCasterFocusPosition(projectileFocusHeight),
            GetTargetFocusPosition(projectileFocusHeight),
            t);
        SmoothLookAt(cam, midFocus, dt, rotationLerpSpeed * 1.4f);
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, 60f, dt);
    }

    private void UpdateVictoryCamera(float dt)
    {
        if (!TryGetCamera(BattleCameraRole.Victory, out var cam) || !caster)
            return;

        Vector3 focus = GetCasterFocusPosition(victoryHeight);
        Vector3 backward = caster.forward.sqrMagnitude > epsilon ? -caster.forward.normalized : Vector3.back;
        Vector3 desiredPos = focus + backward * victoryDistance;

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.4f);
        Quaternion targetRot = Quaternion.LookRotation((focus - desiredPos).normalized, Vector3.up) * Quaternion.Euler(victoryTilt, 0f, 0f);
        SmoothRotation(cam, targetRot, dt, rotationLerpSpeed * 0.5f);
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, victoryFov, dt * 0.6f);
    }

    private bool IsAttackNoiseActive(BattleCameraRole role)
    {
        if (currentActiveRole != role)
            return false;

        return attackNoiseAmplitude > 0f || attackNoiseRotationAmplitude > 0f;
    }

    private Vector3 ApplyAttackPositionNoise(Vector3 basePosition, BattleCameraRole role)
    {
        if (!IsAttackNoiseActive(role) || attackNoiseAmplitude <= 0f)
            return basePosition;

        float t = Time.time * attackNoiseFrequency;
        float x = (Mathf.PerlinNoise(attackNoiseSeed.x, t) - 0.5f) * attackNoiseAmplitude;
        float y = (Mathf.PerlinNoise(attackNoiseSeed.y, t) - 0.5f) * attackNoiseAmplitude * attackNoiseVerticalMultiplier;
        float z = (Mathf.PerlinNoise(attackNoiseSeed.z, t) - 0.5f) * attackNoiseAmplitude;
        return basePosition + new Vector3(x, y, z);
    }

    private Vector3 GetAttackRotationNoise(BattleCameraRole role)
    {
        if (!IsAttackNoiseActive(role) || attackNoiseRotationAmplitude <= 0f)
            return Vector3.zero;

        float t = Time.time * attackNoiseFrequency;
        float pitch = (Mathf.PerlinNoise(attackNoiseSeed.x + 31.7f, t) - 0.5f) * attackNoiseRotationAmplitude;
        float yaw = (Mathf.PerlinNoise(attackNoiseSeed.y + 78.2f, t) - 0.5f) * attackNoiseRotationAmplitude;
        return new Vector3(pitch, yaw, 0f);
    }

    private void SmoothPosition(CinemachineCamera cam, Vector3 desiredPos, float dt, float speed)
    {
        if (speed <= 0f)
        {
            // Vitesse nulle : on applique directement la cible et on réinitialise les vitesses mémorisées.
            cam.transform.position = desiredPos;
            ResetPositionSmoothing(cam);
            return;
        }

        // Conversion "vitesse" -> "smoothTime" : plus la vitesse est élevée, plus le temps de lissage est court.
        float smoothTime = GetSmoothTime(speed);
        Vector3 current = cam.transform.position;
        Vector3 velocity = positionVelocities.TryGetValue(cam, out var storedVelocity) ? storedVelocity : Vector3.zero;

        // SmoothDamp fournit un amortissement critique très proche du comportement attendu pour des caméras cinématiques.
        Vector3 next = Vector3.SmoothDamp(current, desiredPos, ref velocity, smoothTime, Mathf.Infinity, dt);
        positionVelocities[cam] = velocity;
        cam.transform.position = next;
    }

    private void SmoothLookAt(CinemachineCamera cam, Vector3 targetPos, float dt, float speed, Vector3 noiseEuler = default)
    {
        Vector3 direction = targetPos - cam.transform.position;
        if (direction.sqrMagnitude < epsilon)
            return;

        Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (noiseEuler != Vector3.zero)
            targetRot *= Quaternion.Euler(noiseEuler);

        SmoothRotation(cam, targetRot, dt, speed);
    }

    private void SmoothRotation(
        CinemachineCamera cam,
        Quaternion targetRot,
        float dt,
        float speed)
    {
        if (speed <= 0f)
        {
            cam.transform.rotation = targetRot;
            ResetRotationSmoothing(cam);
            return;
        }

        float smoothTime = GetSmoothTime(speed);
        Vector3 currentEuler = cam.transform.rotation.eulerAngles;
        Vector3 targetEuler = targetRot.eulerAngles;
        Vector3 velocity = rotationVelocities.TryGetValue(cam, out var storedVelocity) ? storedVelocity : Vector3.zero;

        float velX = velocity.x;
        float velY = velocity.y;
        float velZ = velocity.z;

        // On amortit indépendamment chaque axe d'Euler afin de conserver une transition régulière, même lorsque
        // les angles traversent la discontinuité 0°/360°.
        float nextX = Mathf.SmoothDampAngle(currentEuler.x, targetEuler.x, ref velX, smoothTime, Mathf.Infinity, dt);
        float nextY = Mathf.SmoothDampAngle(currentEuler.y, targetEuler.y, ref velY, smoothTime, Mathf.Infinity, dt);
        float nextZ = Mathf.SmoothDampAngle(currentEuler.z, targetEuler.z, ref velZ, smoothTime, Mathf.Infinity, dt);

        rotationVelocities[cam] = new Vector3(velX, velY, velZ);
        cam.transform.rotation = Quaternion.Euler(nextX, nextY, nextZ);
    }

    private float GetSmoothTime(float speed)
    {
        // Clamp agressif pour éviter les divisions par zéro tout en conservant une réponse fine sur les vitesses élevées.
        return Mathf.Max(0.0001f, 1f / Mathf.Max(0.0001f, speed));
    }

    private void ResetCameraSmoothing(CinemachineCamera cam)
    {
        ResetPositionSmoothing(cam);
        ResetRotationSmoothing(cam);
    }

    private void ResetPositionSmoothing(CinemachineCamera cam)
    {
        // Remove renvoie false si la clé est absente : l'appel reste donc sans effet secondaire et évite
        // un branching supplémentaire lorsque la caméra n'avait pas encore été amortie.
        positionVelocities.Remove(cam);
    }

    private void ResetRotationSmoothing(CinemachineCamera cam)
    {
        // Même logique que pour la position : un simple Remove garantit la suppression des inerties obsolètes
        // sans créer d'allocation supplémentaire.
        rotationVelocities.Remove(cam);
    }
}
