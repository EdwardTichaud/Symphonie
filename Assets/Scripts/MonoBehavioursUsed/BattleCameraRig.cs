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

    [Header("Over Shoulder (Stay)")]
    [SerializeField, Tooltip("Champ de vision utilisé lorsque l'on se cale sur l'ancre fixe Camera_Shoulder_Stay.")]
    private float shoulderStayFov = 50f;

    [Header("Over Shoulder (Stay)")]
    [SerializeField, Tooltip("Champ de vision utilisé lorsque l'on se cale sur l'ancre fixe Camera_Shoulder_Stay.")]
    private float shoulderStayFov = 50f;

    [Header("Close Push Caster")]
    [SerializeField] private float pushFov = 42f;

    [Header("Target Reaction")]
    [SerializeField] private float reactionFov = 48f;

    [Header("Wide Establish")]
    [SerializeField] private float wideFov = 55f;
    [SerializeField] private float wideCasterWeight = 0.6f;
    [SerializeField] private float wideTargetWeight = 0.4f;

    [Header("Projectile Flyby")]
    [SerializeField] private float projectileSpeed = 1.35f;
    [SerializeField] private float projectileSideAmplitude = 0.65f;
    [SerializeField] private float projectileSideFrequency = 1.2f;

    [Header("Victory")]
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
    private CharacterUnit casterUnit;
    // Cache direct de la cible afin d'accéder rapidement à ses points caméra sans GetComponent redondant.
    private CharacterUnit targetUnit;
    private Vector3? manualMidpoint;

    // Références mémorisées pour retirer proprement les membres du CinemachineTargetGroup.
    private Transform activeCasterGroupMember;
    private Transform activeTargetGroupMember;
    private Transform activeMidGroupMember;

    private BattleCameraRole currentActiveRole = BattleCameraRole.None;
    private float projectileTimer;
    private Vector3 attackNoiseSeed;

    // Mémoire dédiée au plan "Over Shoulder Look Target" qui ne doit se calculer qu'une fois par action.
    private Vector3 shoulderStayPosition;
    private bool shoulderStayInitialized;

    private const string ShoulderStayAnchorName = "Camera_Shoulder_Stay";
    private static readonly string[] ShoulderMovingAnchorCandidates =
    {
        "Camera_Shoulder_Moving",
        "Camera_Shoulder_Right_Near",
        "Camera_Shoulder_Left_Near"
    };
    private static readonly string[] IdleAnchorCandidates =
    {
        "Camera_MainMenu",
        "Camera_Default",
        "Camera_Idle"
    };
    private static readonly string[] ClosePushAnchorCandidates =
    {
        "Camera_ClosePush",
        "Camera_Move",
        "Camera_Move_1",
        "Camera_Move_2",
        "Camera_Move_3"
    };
    private static readonly string[] TargetReactionAnchorCandidates =
    {
        "Camera_TargetReaction",
        "Camera_TargetedPoint"
    };
    private static readonly string[] WideAnchorCandidates =
    {
        "Camera_WideEstablish",
        "Camera_OrbitalPoint"
    };
    private static readonly string[] ProjectileAnchorCandidates =
    {
        "Camera_Projectile",
        "Camera_Move_3",
        "Camera_Move"
    };
    private static readonly string[] VictoryAnchorCandidates =
    {
        "Camera_Victory"
    };

    // Valeurs historiques conservées en constante pour offrir un repli gracieux lorsque les points d'ancrage
    // sont absents (scènes non mises à jour, prototypes, etc.). L'objectif est de garantir un comportement
    // fonctionnel sans encourager la configuration par sliders désormais obsolète.
    private const float LegacyShoulderHeight = 1.55f;
    private const float LegacyShoulderDistance = 2.6f;
    private const float LegacyShoulderSideOffset = 0.65f;
    private const float LegacyShoulderLookHeight = 1.45f;
    private const float LegacyPushHeight = 1.7f;
    private const float LegacyPushDistance = 1.1f;
    private const float LegacyPushSideSwing = 0.35f;
    private const float LegacyReactionHeight = 1.4f;
    private const float LegacyReactionDistance = 2.2f;
    private const float LegacyWideHeight = 4f;
    private const float LegacyWideDepth = 9f;
    private const float LegacyWideSide = 1.25f;
    private const float LegacyProjectileHeight = 1.8f;
    private const float LegacyProjectileOffset = 4f;
    private const float LegacyProjectileFocusHeight = 1.5f;
    private const float LegacyVictoryHeight = 2.4f;
    private const float LegacyVictoryDistance = 3.5f;

    private static readonly Dictionary<string, BattleCameraRole> NameToRole = new()
    {
        {"CMV_MainMenuIdle", BattleCameraRole.MainMenuIdle},
        {"CMV_OverShoulder_CasterToTarget", BattleCameraRole.OverShoulderCasterToTarget},
        {"CMV_OverShoulder_CasterLookTarget", BattleCameraRole.OverShoulderCasterLookTarget},
        {"CMV_ClosePush_Caster", BattleCameraRole.ClosePushCaster},
        {"CMV_TargetReaction", BattleCameraRole.TargetReaction},
        {"CMV_WideEstablish", BattleCameraRole.WideEstablish},
        {"CMV_Projectile_Flyby", BattleCameraRole.ProjectileFlyby},
        {"CMV_Victory", BattleCameraRole.Victory}
    };

    // Mémorise les avertissements déjà envoyés pour ne pas saturer la console lorsque certains points manquent.
    private readonly HashSet<string> missingAnchorWarnings = new();

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
        CharacterUnit casterUnitParam,
        CharacterUnit targetUnitParam,
        Vector3? midpointOverride = null,
        Transform casterAnchor = null,
        Transform targetAnchor = null)
    {
        casterUnit = casterUnitParam;
        targetUnit = targetUnitParam;
        caster = casterUnitParam ? casterUnitParam.transform : null;
        target = targetUnitParam ? targetUnitParam.transform : null;
        casterFocusAnchor = casterAnchor;
        targetFocusAnchor = targetAnchor;
        manualMidpoint = midpointOverride;

        ResetShoulderStayAnchor();
        missingAnchorWarnings.Clear();

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
        casterUnit = null;
        targetUnit = null;
        ResetShoulderStayAnchor();
        missingAnchorWarnings.Clear();
        UpdateTargetGroupMembers();
    }

    /// <summary>
    /// Réinitialise la mémoire du plan épaule figé afin de recalculer la position lors du prochain déclenchement.
    /// </summary>
    private void ResetShoulderStayAnchor()
    {
        shoulderStayInitialized = false;
        shoulderStayPosition = Vector3.zero;
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

        if (role != BattleCameraRole.OverShoulderCasterLookTarget)
            shoulderStayInitialized = false;
    }

    /// <summary>
    /// Replace instantanément la caméra associée au rôle donné sur sa pose idéale.
    /// <para>
    /// Cette étape est déclenchée juste avant un changement de priorité afin que la Cinemachine
    /// choisie démarre son blend depuis une position cohérente (aucun "pop" visuel la frame suivante).
    /// </para>
    /// </summary>
    /// <param name="role">Rôle dont la caméra doit être repositionnée.</param>
    public void SnapToRolePose(BattleCameraRole role)
    {
        if (role == BattleCameraRole.None)
            return; // Pas de caméra spécifique à réaligner.

        switch (role)
        {
            case BattleCameraRole.MainMenuIdle:
                UpdateIdleCamera(0f, true);
                break;
            case BattleCameraRole.OverShoulderCasterToTarget:
                UpdateOverShoulderCamera(0f, true);
                break;
            case BattleCameraRole.OverShoulderCasterLookTarget:
                UpdateOverShoulderStayCamera(0f, true);
                break;
            case BattleCameraRole.ClosePushCaster:
                UpdateClosePushCamera(0f, true);
                break;
            case BattleCameraRole.TargetReaction:
                UpdateTargetReactionCamera(0f, true);
                break;
            case BattleCameraRole.WideEstablish:
                UpdateWideCamera(0f, true);
                break;
            case BattleCameraRole.ProjectileFlyby:
                UpdateProjectileCamera(0f, true);
                break;
            case BattleCameraRole.Victory:
                UpdateVictoryCamera(0f, true);
                break;
        }
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
        UpdateOverShoulderStayCamera(dt);
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

    private void UpdateIdleCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.MainMenuIdle, out var cam))
            return;

        Vector3 focus = GetCasterFocusPosition(idleFocusHeight);
        Transform idleAnchor = ResolveCasterAnchor(IdleAnchorCandidates, "MainMenuIdle", logWarning: caster != null);

        Vector3 desiredPos;
        if (idleAnchor)
        {
            // Mode privilégié : la Cinemachine adopte strictement la position du point d'ancrage configuré sur le modèle.
            desiredPos = idleAnchor.position;
        }
        else
        {
            // Repli : conservation de l'ancien comportement orbital pour ne pas dégrader les scènes non mises à jour.
            float orbitAngle = Time.time * idleOrbitSpeed;
            Vector3 orbitOffset = new(
                Mathf.Cos(orbitAngle) * idleOrbitRadius,
                0f,
                Mathf.Sin(orbitAngle) * idleOrbitRadius);

            Vector3 direction = focus - cam.transform.position;
            if (forceSnap || direction.sqrMagnitude < epsilon)
                direction = Vector3.forward;
            else
                direction = direction.normalized;

            Vector3 baseOffset = -direction * idleBaseDistance;
            desiredPos = focus + orbitOffset + baseOffset;
        }

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.5f, forceSnap);
        SmoothLookAt(cam, focus, dt, rotationLerpSpeed * 0.5f, forceSnap: forceSnap);

        float targetFov = 50f + Mathf.Sin(Time.time * idleFovSpeed) * idleFovAmplitude;
        cam.Lens.FieldOfView = forceSnap
            ? targetFov
            : Mathf.Lerp(cam.Lens.FieldOfView, targetFov, dt * 0.5f);
    }

    private void UpdateOverShoulderCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.OverShoulderCasterToTarget, out var cam) || !caster)
            return;

        Vector3 casterFocus = GetCasterFocusPosition(LegacyShoulderHeight);
        Vector3 targetFocus = GetTargetFocusPosition(LegacyShoulderLookHeight);
        Transform movingAnchor = ResolveCasterAnchor(ShoulderMovingAnchorCandidates, "OverShoulder (Moving)");

        Vector3 desiredPos;
        if (movingAnchor)
        {
            // Le plan suit directement le point Camera_Shoulder_Moving pour coller aux réglages artistiques.
            desiredPos = movingAnchor.position;
        }
        else
        {
            // Repli hérité : on reconstruit la pose épaule à partir des directions calculées.
            Vector3 focusDir = targetFocus - casterFocus;
            if (focusDir.sqrMagnitude < epsilon)
                focusDir = caster.forward;
            Vector3 normalizedDir = focusDir.normalized;

            Vector3 side = Vector3.Cross(Vector3.up, normalizedDir).normalized;
            desiredPos = casterFocus - normalizedDir * LegacyShoulderDistance + side * LegacyShoulderSideOffset;
        }

        Vector3 lookPos = targetFocus;

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed, forceSnap);
        SmoothLookAt(cam, lookPos, dt, rotationLerpSpeed, forceSnap: forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? 52f
            : Mathf.Lerp(cam.Lens.FieldOfView, 52f, dt * 0.8f);
    }

    private void UpdateOverShoulderStayCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.OverShoulderCasterLookTarget, out var cam) || !caster)
            return;

        if (forceSnap || !shoulderStayInitialized)
        {
            // Capture une fois la position de l'ancre pour rester figé tout au long de la préparation.
            shoulderStayPosition = ResolveShoulderStayPosition();
            shoulderStayInitialized = true;
        }

        Vector3 desiredPos = shoulderStayPosition;
        Vector3 lookPos = GetTargetFocusPosition(LegacyShoulderLookHeight);

        // Vitesse nulle : la caméra reste collée à la position mémorisée sans suivre l'ancre si elle bouge ensuite.
        SmoothPosition(cam, desiredPos, dt, 0f, forceSnap);
        SmoothLookAt(
            cam,
            lookPos,
            dt,
            rotationLerpSpeed,
            GetAttackRotationNoise(BattleCameraRole.OverShoulderCasterLookTarget),
            forceSnap);

        cam.Lens.FieldOfView = forceSnap
            ? shoulderStayFov
            : Mathf.Lerp(cam.Lens.FieldOfView, shoulderStayFov, dt * 0.8f);
    }

    private Vector3 ResolveShoulderStayPosition()
    {
        Transform anchor = ResolveCasterAnchor(new[] { ShoulderStayAnchorName }, "OverShoulder Stay", logWarning: false);
        if (anchor)
            return anchor.position;

        Debug.LogWarning($"[BattleCameraRig] Point '{ShoulderStayAnchorName}' introuvable sur {caster?.name ?? "(caster nul)"}. Retombée sur un focus épaule générique.");
        return GetCasterFocusPosition(LegacyShoulderHeight);
    }

    private Transform FindAnchorTransform(Transform root, string anchorName)
    {
        if (!root || string.IsNullOrEmpty(anchorName))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, anchorName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Retrouve une ancre portée par le lanceur parmi une liste de candidats, en prenant en compte
    /// les éventuelles surcharges passées par le BattleCameraManager (casterFocusAnchor).
    /// </summary>
    private Transform ResolveCasterAnchor(string[] candidates, string context, bool logWarning = true)
    {
        return ResolveAnchorInternal(candidates, context, logWarning, casterFocusAnchor, casterUnit, caster, allowGlobalSearch: false);
    }

    /// <summary>
    /// Identique à <see cref="ResolveCasterAnchor"/> mais appliqué à la cible.
    /// </summary>
    private Transform ResolveTargetAnchor(string[] candidates, string context, bool logWarning = true)
    {
        return ResolveAnchorInternal(candidates, context, logWarning, targetFocusAnchor, targetUnit, target, allowGlobalSearch: false);
    }

    /// <summary>
    /// Recherche un point d'ancrage indépendant des unités (environnement, décor) avec, si nécessaire,
    /// une recherche globale dans la scène.
    /// </summary>
    private Transform ResolveWorldAnchor(string[] candidates, string context, bool logWarning = true, bool allowGlobalSearch = true)
    {
        return ResolveAnchorInternal(candidates, context, logWarning, null, null, transform, allowGlobalSearch);
    }

    /// <summary>
    /// Implémentation factorisée de la résolution d'ancre. Les paramètres permettent de couvrir les
    /// différents cas (caster, cible, décor) tout en conservant un point unique de journalisation.
    /// </summary>
    private Transform ResolveAnchorInternal(
        string[] candidates,
        string context,
        bool logWarning,
        Transform manualOverride,
        CharacterUnit unit,
        Transform root,
        bool allowGlobalSearch)
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate))
                continue;

            if (manualOverride && string.Equals(manualOverride.name, candidate, StringComparison.OrdinalIgnoreCase))
                return manualOverride;

            Transform anchor = null;
            if (unit != null)
                anchor = unit.GetCameraAnchor(candidate);

            if (!anchor && root)
                anchor = FindAnchorTransform(root, candidate);

            if (!anchor && allowGlobalSearch)
            {
                // Recherche globale ponctuelle : exécutée uniquement lorsque les hiérarchies locales n'exposent pas le point.
                GameObject worldAnchor = GameObject.Find(candidate);
                if (worldAnchor)
                    anchor = worldAnchor.transform;
            }

            if (anchor)
                return anchor;
        }

        if (logWarning)
        {
            string key = $"{context}:{string.Join("|", candidates)}";
            if (missingAnchorWarnings.Add(key))
            {
                Debug.LogWarning($"[BattleCameraRig] Aucun point caméra correspondant ({string.Join(", ", candidates)}) n'a été trouvé pour {context}. Utilisation d'un placement procédural en repli.");
            }
        }

        return null;
    }

    private void UpdateClosePushCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.ClosePushCaster, out var cam) || !caster)
            return;

        Vector3 casterFocus = GetCasterFocusPosition(LegacyPushHeight);
        Vector3 targetFocus = GetTargetFocusPosition();
        Vector3 toTarget = targetFocus - casterFocus;
        if (toTarget.sqrMagnitude < epsilon)
            toTarget = caster.forward;
        Vector3 direction = toTarget.normalized;

        Transform pushAnchor = ResolveCasterAnchor(ClosePushAnchorCandidates, "Close Push", logWarning: caster != null);
        Vector3 desiredPos;
        if (pushAnchor)
        {
            desiredPos = pushAnchor.position;
        }
        else
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            desiredPos = casterFocus - direction * LegacyPushDistance + side * LegacyPushSideSwing;
            desiredPos = ApplyAttackPositionNoise(desiredPos, BattleCameraRole.ClosePushCaster);
        }

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 1.1f, forceSnap);

        Vector3 lookPos = GetCasterFocusPosition(LegacyPushHeight + 0.2f) + direction * 0.15f;
        Vector3 rotationNoise = pushAnchor ? Vector3.zero : GetAttackRotationNoise(BattleCameraRole.ClosePushCaster);
        SmoothLookAt(
            cam,
            lookPos,
            dt,
            rotationLerpSpeed * 1.2f,
            rotationNoise,
            forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? pushFov
            : Mathf.Lerp(cam.Lens.FieldOfView, pushFov, dt * 1.5f);
    }

    private void UpdateTargetReactionCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.TargetReaction, out var cam) || !target)
            return;

        Vector3 targetFocus = GetTargetFocusPosition(LegacyReactionHeight);
        Vector3 fromCaster = targetFocus - GetCasterFocusPosition();
        if (fromCaster.sqrMagnitude < epsilon)
            fromCaster = target.forward.sqrMagnitude > epsilon ? -target.forward : Vector3.back;
        Vector3 direction = fromCaster.normalized;

        Transform reactionAnchor = ResolveTargetAnchor(TargetReactionAnchorCandidates, "Target Reaction", logWarning: target != null);
        Vector3 desiredPos;
        if (reactionAnchor)
        {
            desiredPos = reactionAnchor.position;
        }
        else
        {
            desiredPos = targetFocus + direction * LegacyReactionDistance;
            desiredPos = ApplyAttackPositionNoise(desiredPos, BattleCameraRole.TargetReaction);
        }

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.9f, forceSnap);
        Vector3 lookPos = GetTargetFocusPosition(LegacyReactionHeight + 0.1f);
        Vector3 rotationNoise = reactionAnchor ? Vector3.zero : GetAttackRotationNoise(BattleCameraRole.TargetReaction);
        SmoothLookAt(
            cam,
            lookPos,
            dt,
            rotationLerpSpeed,
            rotationNoise,
            forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? reactionFov
            : Mathf.Lerp(cam.Lens.FieldOfView, reactionFov, dt * 0.9f);
    }

    private void UpdateWideCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.WideEstablish, out var cam))
            return;

        Vector3 center = GetWeightedCenter();
        Transform wideAnchor = ResolveCasterAnchor(WideAnchorCandidates, "Wide Establish", logWarning: false)
            ?? ResolveTargetAnchor(WideAnchorCandidates, "Wide Establish", logWarning: false)
            ?? ResolveWorldAnchor(WideAnchorCandidates, "Wide Establish");

        Vector3 desiredPos;
        if (wideAnchor)
        {
            desiredPos = wideAnchor.position;
        }
        else
        {
            Vector3 separation = GetTargetFocusPosition() - GetCasterFocusPosition();
            if (separation.sqrMagnitude < epsilon)
                separation = caster ? caster.forward : Vector3.forward;

            Vector3 forward = separation.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

            desiredPos = center - forward * LegacyWideDepth + side * LegacyWideSide + Vector3.up * LegacyWideHeight;
        }

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.6f, forceSnap);
        SmoothLookAt(
            cam,
            center + Vector3.up * 1.2f,
            dt,
            rotationLerpSpeed * 0.6f,
            forceSnap: forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? wideFov
            : Mathf.Lerp(cam.Lens.FieldOfView, wideFov, dt * 0.5f);
    }

    private void UpdateProjectileCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.ProjectileFlyby, out var cam) || !caster || !target)
            return;

        Transform projectileAnchor = ResolveCasterAnchor(ProjectileAnchorCandidates, "Projectile Flyby", logWarning: false)
            ?? ResolveTargetAnchor(ProjectileAnchorCandidates, "Projectile Flyby", logWarning: false)
            ?? ResolveWorldAnchor(ProjectileAnchorCandidates, "Projectile Flyby");

        float t = 0.5f;
        Vector3 midFocus;

        if (projectileAnchor)
        {
            // Si un point dédié est disponible (timeline spécifique, travelling enregistré), on le suit strictement.
            cam.transform.position = projectileAnchor.position;
            ResetPositionSmoothing(cam);
            midFocus = GetTargetFocusPosition(LegacyProjectileFocusHeight);
        }
        else
        {
            if (forceSnap)
            {
                // Lors d'un snap on repart systématiquement du début du travelling pour garantir
                // que la transition démarre dans une pose cohérente.
                projectileTimer = 0f;
            }
            else if (currentActiveRole == BattleCameraRole.ProjectileFlyby)
                projectileTimer += dt * projectileSpeed;
            else
                projectileTimer = Mathf.Max(0f, projectileTimer - dt * projectileSpeed);

            t = Mathf.Clamp01(projectileTimer);
            Vector3 casterPos = GetCasterFocusPosition(LegacyProjectileHeight);
            Vector3 targetPos = GetTargetFocusPosition(LegacyProjectileHeight);
            Vector3 direction = targetPos - casterPos;
            if (direction.sqrMagnitude < epsilon)
                direction = caster.forward;
            Vector3 forward = direction.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 start = casterPos - forward * LegacyProjectileOffset;
            Vector3 end = targetPos + forward * LegacyProjectileOffset;
            Vector3 lerp = Vector3.Lerp(start, end, t);
            float sine = Mathf.Sin(projectileTimer * projectileSideFrequency) * projectileSideAmplitude;
            Vector3 desiredPos = lerp + side * sine;

            cam.transform.position = desiredPos;
            ResetPositionSmoothing(cam);
            midFocus = Vector3.Lerp(
                GetCasterFocusPosition(LegacyProjectileFocusHeight),
                GetTargetFocusPosition(LegacyProjectileFocusHeight),
                t);
        }

        SmoothLookAt(
            cam,
            midFocus,
            dt,
            rotationLerpSpeed * 1.4f,
            forceSnap: forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? 60f
            : Mathf.Lerp(cam.Lens.FieldOfView, 60f, dt);
    }

    private void UpdateVictoryCamera(float dt, bool forceSnap = false)
    {
        if (!TryGetCamera(BattleCameraRole.Victory, out var cam) || !caster)
            return;

        Vector3 focus = GetCasterFocusPosition(LegacyVictoryHeight);
        Transform victoryAnchor = ResolveCasterAnchor(VictoryAnchorCandidates, "Victory", logWarning: caster != null)
            ?? ResolveWorldAnchor(VictoryAnchorCandidates, "Victory");

        Vector3 desiredPos;
        if (victoryAnchor)
        {
            desiredPos = victoryAnchor.position;
        }
        else
        {
            Vector3 backward = caster.forward.sqrMagnitude > epsilon ? -caster.forward.normalized : Vector3.back;
            desiredPos = focus + backward * LegacyVictoryDistance;
        }

        SmoothPosition(cam, desiredPos, dt, baseLerpSpeed * 0.4f, forceSnap);
        Quaternion targetRot = Quaternion.LookRotation((focus - desiredPos).normalized, Vector3.up)
            * Quaternion.Euler(victoryTilt, 0f, 0f);
        SmoothRotation(cam, targetRot, dt, rotationLerpSpeed * 0.5f, forceSnap);
        cam.Lens.FieldOfView = forceSnap
            ? victoryFov
            : Mathf.Lerp(cam.Lens.FieldOfView, victoryFov, dt * 0.6f);
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

    private void SmoothPosition(CinemachineCamera cam, Vector3 desiredPos, float dt, float speed, bool forceSnap = false)
    {
        if (forceSnap)
        {
            cam.transform.position = desiredPos;
            ResetPositionSmoothing(cam);
            return;
        }

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

    private void SmoothLookAt(
        CinemachineCamera cam,
        Vector3 targetPos,
        float dt,
        float speed,
        Vector3 noiseEuler = default,
        bool forceSnap = false)
    {
        Vector3 direction = targetPos - cam.transform.position;
        if (direction.sqrMagnitude < epsilon)
            return;

        Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (noiseEuler != Vector3.zero)
            targetRot *= Quaternion.Euler(noiseEuler);

        SmoothRotation(cam, targetRot, dt, speed, forceSnap);
    }

    private void SmoothRotation(
        CinemachineCamera cam,
        Quaternion targetRot,
        float dt,
        float speed,
        bool forceSnap = false)
    {
        if (forceSnap)
        {
            cam.transform.rotation = targetRot;
            ResetRotationSmoothing(cam);
            return;
        }

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
