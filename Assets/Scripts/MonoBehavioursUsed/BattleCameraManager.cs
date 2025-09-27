using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gère l'activation et le placement des caméras Cinemachine pendant les combats.
/// Le système associe chaque "CMV_" à un point "CMVPoint_" exposé sur les <see cref="CharacterUnit"/>.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Nom de la caméra d'introduction explicitement référencé dans les scripts.</summary>
    private const string IntroCameraName = "CMV_BattleIntro";

    /// <summary>Seuil évitant un LookRotation instable lorsque le point visé est trop proche.</summary>
    private const float IntroLookDirectionThreshold = 0.0001f;

    /// <summary>Accès global au gestionnaire de caméras de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    /// <summary>
    /// Fournit un accès en lecture à l'unité actuellement propriétaire du tour.
    /// Ce getter est principalement utilisé par le <see cref="NewBattleManager"/>
    /// lorsque celui-ci doit reconstruire le contexte d'affichage des menus sans
    /// perdre l'alignement des Cinemachine sur leurs ancres « CMVPoint_ ».
    /// </summary>
    public CharacterUnit CurrentTurnOwner => currentTurnOwner;

    /// <summary>BlendSwitcher responsable des transitions (0,5 s smooth imposé).</summary>
    private CinemachineBlendSwitcher blendSwitcher;

    /// <summary>Accès direct aux CinemachineCamera par leur nom.</summary>
    private readonly Dictionary<string, CinemachineCamera> cameraByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Liste des CinemachineCamera découvertes afin de proposer un fallback aléatoire.</summary>
    private readonly List<CinemachineCamera> availableCameras = new();

    [Header("Effet de respiration")] // 👉 regroupe les paramètres d'oscillation douce
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

    [Header("Réaction aux dégâts")] // 👉 paramètres dédiés au tremblement déclenché par un impact
    [Tooltip("Active un tremblement court lorsque les membres de l'escouade subissent une attaque.")]
    [SerializeField] private bool enableDamageShake = true;

    [Tooltip("Durée en secondes de l'impulsion de caméra appliquée après un coup.")]
    [SerializeField] private float damageShakeDuration = 0.35f;

    [Tooltip("Fréquence des oscillations appliquées pendant le tremblement.")]
    [SerializeField] private float damageShakeFrequency = 18f;

    [Tooltip("Amplitude maximale (en mètres) du déplacement de caméra généré par l'impact.")]
    [SerializeField] private float damageShakePositionAmplitude = 0.08f;

    [Tooltip("Amplitude maximale (en degrés) de la rotation de caméra générée par l'impact.")]
    [SerializeField] private float damageShakeRotationAmplitude = 1.25f;

    [Tooltip("Multiplicateur appliqué lorsque le coup est considéré comme dévastateur.")]
    [SerializeField] private float damageShakeDevastatingMultiplier = 1.75f;

    [Tooltip("Courbe définissant l'atténuation du tremblement (1 = départ fort, 0 = repos).")]
    [SerializeField] private AnimationCurve damageShakeEnvelope = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    /// <summary>Référence de la caméra d'introduction pour gérer son travelling manuel.</summary>
    private Transform introCameraTransform;

    /// <summary>Position locale de base de la caméra d'introduction.</summary>
    private Vector3 introCameraInitialLocalPosition;

    /// <summary>Rotation locale de base de la caméra d'introduction.</summary>
    private Quaternion introCameraInitialLocalRotation;

    /// <summary>Routine de déplacement en cours pour la caméra d'introduction.</summary>
    private Coroutine introCameraMoveRoutine;

    /// <summary>Routine différée pour la remise en place de la caméra d'introduction.</summary>
    private Coroutine introCameraResetRoutine;

    /// <summary>Unité actuellement en train de jouer son tour.</summary>
    private CharacterUnit currentTurnOwner;

    /// <summary>Unité considérée comme lanceur pour les caméras d'action.</summary>
    private CharacterUnit currentCaster;

    /// <summary>Unité ciblée par l'action en cours ou par la sélection.</summary>
    private CharacterUnit currentTarget;

    /// <summary>Ancre explicite fournie lors d'une configuration de cibles (caster).</summary>
    private Transform casterAnchorOverride;

    /// <summary>Ancre explicite fournie lors d'une configuration de cibles (target).</summary>
    private Transform targetAnchorOverride;

    /// <summary>Ensemble des points manquants déjà signalés pour ne pas inonder la console.</summary>
    private readonly HashSet<string> missingAnchorWarnings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nom de la caméra actuellement prioritaire.</summary>
    private string currentCameraName;

    /// <summary>Nom de la caméra actuellement secouée par un impact.</summary>
    private string damageShakeCameraName;

    /// <summary>Durée réellement utilisée pour la secousse en cours.</summary>
    private float damageShakeDurationCurrent;

    /// <summary>Temps écoulé depuis le début de la secousse en cours.</summary>
    private float damageShakeElapsed;

    /// <summary>Intensité globale de la secousse (1 par défaut, >1 pour un coup dévastateur).</summary>
    private float damageShakeIntensity = 1f;

    /// <summary>Décalage positionnel calculé pour la frame courante.</summary>
    private Vector3 damageShakeOffset = Vector3.zero;

    /// <summary>Rotation additive calculée pour la frame courante.</summary>
    private Quaternion damageShakeRotationOffset = Quaternion.identity;

    /// <summary>Direction latérale privilégiée pour l'oscillation de la caméra.</summary>
    private Vector3 damageShakeRight = Vector3.right;

    /// <summary>Direction verticale utilisée pour générer l'effet de secousse.</summary>
    private Vector3 damageShakeUp = Vector3.up;

    /// <summary>Phase initiale de l'onde principale du tremblement.</summary>
    private float damageShakePrimaryPhase;

    /// <summary>Phase initiale de l'onde secondaire (composante verticale).</summary>
    private float damageShakeSecondaryPhase;

    /// <summary>
    /// Décalages de phase uniques par caméra afin que les oscillations ne soient pas synchronisées.
    /// Cela garantit un rendu plus naturel : chaque Cinemachine vibre légèrement mais avec un décalage
    /// propre, ce qui évite un mouvement collectif artificiel.
    /// </summary>
    private readonly Dictionary<string, float> breathingPhaseOffsets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rôle logique actuellement prioritaire.</summary>
    private BattleCameraRole currentRole = BattleCameraRole.None;

    /// <summary>Modes d'association entre une caméra Cinemachine et un CharacterUnit.</summary>
    private enum CameraAnchorOwner
    {
        Manual,
        TurnOwner,
        Caster,
        Target
    }

    /// <summary>Description complète de l'association entre une Cinemachine et un point "CMVPoint_".</summary>
    private struct CameraBindingConfig
    {
        public CameraAnchorOwner Owner;
        public string Suffix;
        public bool LooksAtTarget;

        public CameraBindingConfig(CameraAnchorOwner owner, string suffix, bool looksAtTarget)
        {
            Owner = owner;
            Suffix = suffix;
            LooksAtTarget = looksAtTarget;
        }
    }

    /// <summary>Nom de la Cinemachine orbitale utilisée lors du ciblage d'objet.</summary>
    private const string OrbitAroundCameraName = "CMV_OrbitAroundUnit";

    /// <summary>
    /// Durée souhaitée (en secondes) pour le retour en douceur de la caméra orbitale
    /// vers la position par défaut du lanceur. La valeur est volontairement exposée
    /// comme constante afin de conserver un comportement homogène sur l'ensemble du
    /// projet et d'éviter les ajustements manuels au cas par cas.
    /// </summary>
    private const float OrbitReturnDurationSeconds = 5f;

    /// <summary>
    /// Indique si une interpolation de retour est en cours pour « CMV_OrbitAroundUnit ».
    /// Ce flag évite de réinitialiser les valeurs de départ à chaque LateUpdate alors que
    /// la transition est déjà lancée.
    /// </summary>
    private bool orbitReturnInProgress;

    /// <summary>Temps écoulé depuis le lancement de l'interpolation de retour.</summary>
    private float orbitReturnElapsed;

    /// <summary>Position d'origine mémorisée afin de produire une interpolation fluide.</summary>
    private Vector3 orbitReturnStartPosition;

    /// <summary>Rotation d'origine mémorisée pour un retour sans à-coups.</summary>
    private Quaternion orbitReturnStartRotation;

    /// <summary>
    /// Transform cible utilisé durant l'interpolation. On le mémorise afin de redéclencher
    /// correctement la transition si la référence venait à changer (nouveau caster, cinématique...).
    /// </summary>
    private Transform orbitReturnTarget;

    /// <summary>Configuration déclarative de toutes les caméras "CMV_" présentes dans la scène.</summary>
    private static readonly Dictionary<string, CameraBindingConfig> CameraBindings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CMV_MainMenu"] = new(CameraAnchorOwner.TurnOwner, "MainMenu", false),
        ["CMV_SkillsMenu"] = new(CameraAnchorOwner.TurnOwner, "SkillsMenu", false),
        ["CMV_ItemsMenu"] = new(CameraAnchorOwner.TurnOwner, "ItemsMenu", false),
        // Caméra orbitale utilisée pour mettre en valeur la cible durant le choix d'un objet.
        ["CMV_OrbitAroundUnit"] = new(CameraAnchorOwner.Target, "OrbitAroundUnit", false),
        ["CMV_TargetReaction"] = new(CameraAnchorOwner.Target, "TargetReaction", false),
        ["CMV_Projectile_Flyby"] = new(CameraAnchorOwner.Caster, "Projectile_Flyby", false),
        ["CMV_OverShoulder_CasterLookTarget"] = new(CameraAnchorOwner.Caster, "OverShoulder_CasterLookTarget", true),
        ["CMV_OverShoulder_CasterToTarget"] = new(CameraAnchorOwner.Caster, "OverShoulder_CasterToTarget", true),
        ["CMV_OverHead_CasterLookTarget"] = new(CameraAnchorOwner.Caster, "OverHead_CasterLookTarget", true),
        ["CMV_BattleIntro"] = new(CameraAnchorOwner.Manual, "BattleIntro", false),
        ["CMV_Victory"] = new(CameraAnchorOwner.Manual, "Victory", false)
    };

    /// <summary>Association par défaut entre les rôles historiques et les nouvelles caméras "CMV_".</summary>
    private static readonly Dictionary<BattleCameraRole, string> DefaultRoleToCameraName = new()
    {
        { BattleCameraRole.MainMenuIdle, "CMV_MainMenu" },
        { BattleCameraRole.OverShoulderCasterToTarget, "CMV_OverShoulder_CasterToTarget" },
        { BattleCameraRole.OverShoulderCasterLookTarget, "CMV_OverShoulder_CasterLookTarget" },
        { BattleCameraRole.ClosePushCaster, "CMV_OverShoulder_CasterLookTarget" },
        { BattleCameraRole.TargetReaction, "CMV_TargetReaction" },
        { BattleCameraRole.WideEstablish, "CMV_OverHead_CasterLookTarget" },
        { BattleCameraRole.ProjectileFlyby, "CMV_Projectile_Flyby" },
        { BattleCameraRole.Victory, "CMV_Victory" }
    };

    /// <summary>Style de blend homogène imposé par le <see cref="CinemachineBlendSwitcher"/>.</summary>
    public CinemachineBlendDefinition.Styles SmoothBlendStyle =>
        blendSwitcher ? blendSwitcher.SmoothBlendStyle : CinemachineBlendSwitcher.ResolveSmoothBlendStyle();

    /// <summary>Durée (0,5 s) imposée à toutes les transitions caméra.</summary>
    public float SmoothBlendDuration =>
        blendSwitcher ? blendSwitcher.SmoothBlendDuration : CinemachineBlendSwitcher.GlobalSmoothBlendDurationSeconds;

    void Awake()
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

        // Par défaut, les rôles pointent sur l'association statique décrite plus haut.
        foreach (var kvp in DefaultRoleToCameraName)
        {
            if (!cameraByName.ContainsKey(kvp.Value))
                Debug.LogWarning($"[BattleCameraManager] La caméra '{kvp.Value}' est introuvable pour le rôle {kvp.Key}.");
        }

        // Retour immédiat sur la caméra Unity de base au lancement.
        if (blendSwitcher)
            blendSwitcher.DisplayCamera(null, SmoothBlendDuration);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        // 🎢 Met à jour l'effet de secousse avant de replacer les caméras sur leurs ancres.
        UpdateDamageShakeState();

        // Les caméras doivent coller en permanence aux points "CMVPoint_".
        RefreshAllCameraPlacements();
    }

    /// <summary>Réactualise le cache des CinemachineCamera disponibles.</summary>
    private void RefreshCameraCache()
    {
        cameraByName.Clear();
        availableCameras.Clear();

        foreach (var cam in FindObjectsOfType<CinemachineCamera>())
        {
            if (cam == null)
                continue;

            string name = cam.gameObject.name;

            if (!cameraByName.ContainsKey(name))
                cameraByName.Add(name, cam);

            availableCameras.Add(cam);
        }

        CacheIntroCameraReference();
    }

    /// <summary>Replace toutes les caméras "CMV_" sur leurs points d'ancrage respectifs.</summary>
    private void RefreshAllCameraPlacements()
    {
        foreach (var kvp in CameraBindings)
            RefreshCameraPlacement(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Met à jour le tremblement déclenché lors d'un impact et prépare les offsets appliqués
    /// au prochain rafraîchissement de caméra.
    /// </summary>
    private void UpdateDamageShakeState()
    {
        if (!enableDamageShake || damageShakeDurationCurrent <= 0f)
        {
            // Sans effet actif, on s'assure que la caméra reste strictement calée à son ancre.
            damageShakeOffset = Vector3.zero;
            damageShakeRotationOffset = Quaternion.identity;
            damageShakeCameraName = null;
            return;
        }

        damageShakeElapsed += Time.deltaTime;

        // Normalisation du temps écoulé pour interroger la courbe d'atténuation.
        float normalizedTime = Mathf.Clamp01(damageShakeElapsed / Mathf.Max(damageShakeDurationCurrent, 0.0001f));
        float envelope = damageShakeEnvelope != null && damageShakeEnvelope.length > 0
            ? Mathf.Max(0f, damageShakeEnvelope.Evaluate(normalizedTime))
            : 1f - normalizedTime;

        if (envelope <= 0f)
        {
            // L'enveloppe est retombée à zéro : on annule toute transformation résiduelle.
            damageShakeDurationCurrent = 0f;
            damageShakeOffset = Vector3.zero;
            damageShakeRotationOffset = Quaternion.identity;
            damageShakeCameraName = null;
            return;
        }

        float angularFrequency = Mathf.Max(damageShakeFrequency, 0.0001f) * Mathf.PI * 2f;
        float mainAngle = damageShakePrimaryPhase + (damageShakeElapsed * angularFrequency);
        float secondaryAngle = damageShakeSecondaryPhase + (damageShakeElapsed * angularFrequency * 1.35f);

        float intensity = damageShakeIntensity * envelope;
        float positionAmplitude = damageShakePositionAmplitude * intensity;
        float rotationAmplitude = damageShakeRotationAmplitude * intensity;

        // Oscillation principale (latérale) + secondaire (verticale) pour un effet plus organique.
        float horizontal = Mathf.Sin(mainAngle);
        float vertical = Mathf.Sin(secondaryAngle);
        damageShakeOffset = (damageShakeRight * horizontal + damageShakeUp * vertical) * positionAmplitude;

        // On combine une légère rotation de lacet (autour de l'axe vertical) et de tangage.
        Quaternion yawRotation = Quaternion.AngleAxis(Mathf.Sin(mainAngle * 0.5f) * rotationAmplitude, damageShakeUp);
        Quaternion pitchRotation = Quaternion.AngleAxis(Mathf.Cos(secondaryAngle * 0.5f) * rotationAmplitude * 0.5f, damageShakeRight);
        damageShakeRotationOffset = yawRotation * pitchRotation;

        if (damageShakeElapsed >= damageShakeDurationCurrent)
        {
            // Arrive en fin de secousse : la prochaine frame remettra la caméra sur son ancre exacte.
            damageShakeDurationCurrent = 0f;
            damageShakeCameraName = null;
        }
    }

    /// <summary>Replace une caméra donnée sur son point d'ancrage.</summary>
    private void RefreshCameraPlacement(string cameraName, CameraBindingConfig config)
    {
        if (!TryGetCameraByName(cameraName, out var camera) || camera == null)
            return;

        if (config.Owner == CameraAnchorOwner.Manual)
            return; // Caméras positionnées manuellement via l'inspecteur.

        CharacterUnit ownerUnit = ResolveAnchorOwner(config.Owner);
        if (ownerUnit == null)
        {
            // 🌀 Cas particulier : lorsque la caméra orbitale perd sa cible (sortie du mode
            // de sélection d'objet, interruption d'une action…), elle se raccroche par défaut
            // au Transform du lanceur avec une translation instantanée. Pour éviter ce retour
            // trop brusque, on déclenche une interpolation contrôlée sur deux secondes.
            if (string.Equals(cameraName, OrbitAroundCameraName, StringComparison.OrdinalIgnoreCase))
                UpdateOrbitReturn(camera);

            return;
        }

        // Dès qu'une cible légitime est retrouvée, on annule l'éventuelle interpolation en cours
        // afin que la caméra recolle immédiatement à l'ancre prévue par les artistes.
        ResetOrbitReturnStateIfNeeded(cameraName);

        Transform anchor = ResolveAnchorTransform(ownerUnit, config);
        if (anchor == null)
            return;

        camera.transform.position = anchor.position;

        if (config.LooksAtTarget)
        {
            Transform lookTarget = ResolveLookAtTarget();
            if (lookTarget != null)
            {
                Vector3 forward = lookTarget.position - anchor.position;
                if (forward.sqrMagnitude > 0.0001f)
                    camera.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

                // 🎯 En parallèle de la rotation manuelle, on informe explicitement Cinemachine de la cible à viser.
                // Sans cela, l'activation de « CMV_OverShoulder_CasterToTarget » conservait la dernière orientation
                // connue pendant un court instant, le temps que le pipeline interne recalcule le cadrage.
                // En imposant immédiatement la cible via TargetSettings, on supprime ce délai et la caméra se
                // verrouille sur le bon point dès qu'elle obtient la priorité d'affichage.
                var targetSettings = camera.Target;
                targetSettings.CustomLookAtTarget = true;
                targetSettings.LookAtTarget = lookTarget;
                camera.Target = targetSettings;
            }
            else
            {
                camera.transform.rotation = anchor.rotation;

                // 🧭 Aucun point de regard valable : on désactive la redirection forcée pour éviter un résidu d'état.
                var targetSettings = camera.Target;
                targetSettings.CustomLookAtTarget = false;
                targetSettings.LookAtTarget = null;
                camera.Target = targetSettings;
            }
        }
        else
        {
            camera.transform.rotation = anchor.rotation;

            // 🔁 Les caméras qui ne suivent pas une cible doivent s'appuyer uniquement sur leur rotation locale.
            // On nettoie donc toute instruction précédente d'orientation transmise au CinemachineCamera.
            var targetSettings = camera.Target;
            targetSettings.CustomLookAtTarget = false;
            targetSettings.LookAtTarget = null;
            camera.Target = targetSettings;
        }

        // 🎥 Finalise la pose en ajoutant un très léger flottement « respirant » pour bannir les plans figés.
        ApplyBreathingMotion(camera, cameraName);

        // 💥 Superpose éventuellement une secousse ponctuelle déclenchée lors d'un coup subi.
        ApplyDamageShake(camera, cameraName);
    }

    /// <summary>Identifie l'unité responsable d'une caméra donnée.</summary>
    private CharacterUnit ResolveAnchorOwner(CameraAnchorOwner owner)
    {
        return owner switch
        {
            CameraAnchorOwner.TurnOwner => currentTurnOwner,
            CameraAnchorOwner.Caster => currentCaster ?? currentTurnOwner,
            CameraAnchorOwner.Target => currentTarget,
            _ => null
        };
    }

    /// <summary>Récupère l'ancre "CMVPoint_" appropriée sur l'unité fournie.</summary>
    private Transform ResolveAnchorTransform(CharacterUnit unit, CameraBindingConfig config)
    {
        if (unit == null || string.IsNullOrEmpty(config.Suffix))
            return null;

        Transform anchorOverride = null;
        if (config.Owner == CameraAnchorOwner.Caster)
            anchorOverride = casterAnchorOverride;
        else if (config.Owner == CameraAnchorOwner.Target)
            anchorOverride = targetAnchorOverride;

        if (anchorOverride != null)
            return anchorOverride;

        string pointName = $"CMVPoint_{config.Suffix}";
        Transform anchor = null;

        // 🎯 Cas particulier : la caméra "CMV_OverShoulder_CasterLookTarget" doit se caler sur
        // le point généré directement sous le parent du CharacterUnit (PlayerPosition_X / EnemyPosition_X).
        // Les artistes ajustent ce repère pour garantir une composition identique quel que soit le
        // modèle instancié ; on force donc cette recherche avant de retomber sur la logique standard.
        if (ShouldUseParentCasterLookTarget(config))
            anchor = ResolveParentCasterLookTargetAnchor(unit);

        // 🧭 Si aucune ancre spécifique au parent n'a été trouvée, on revient au comportement par défaut
        // en interrogeant les points « CMVPoint_… » directement disponibles sur l'unité.
        anchor ??= unit.GetCameraAnchor(pointName);
        if (anchor == null && missingAnchorWarnings.Add(pointName))
            Debug.LogWarning($"[BattleCameraManager] Point '{pointName}' introuvable sur '{unit.name}'.");

        return anchor;
    }

    /// <summary>
    /// Détermine si la configuration de caméra doit privilégier le point placé sur le parent
    /// du CharacterUnit plutôt que sur l'unité elle-même.
    /// </summary>
    private static bool ShouldUseParentCasterLookTarget(CameraBindingConfig config)
    {
        return config.Owner == CameraAnchorOwner.Caster
            && !string.IsNullOrEmpty(config.Suffix)
            && string.Equals(config.Suffix, "OverShoulder_CasterLookTarget", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Recherche le point « CMVPoint_OverShoulder_CasterLookTarget » placé sur le parent du personnage.
    /// </summary>
    private static Transform ResolveParentCasterLookTargetAnchor(CharacterUnit unit)
    {
        if (unit == null)
            return null;

        const string anchorName = "CMVPoint_OverShoulder_CasterLookTarget";

        Transform parent = unit.transform.parent;
        if (parent == null)
            return null;

        // 🎬 Priorité au child direct : c'est la configuration privilégiée par le BattleManager.
        Transform directChild = parent.Find(anchorName);
        if (directChild != null)
            return directChild;

        // 🔄 Certains environnements peuvent regrouper le point dans une sous-hiérarchie.
        // On balaie donc l'ensemble des enfants du parent (hors unité actuelle) pour couvrir ces variantes.
        foreach (Transform sibling in parent.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (sibling == null || sibling == unit.transform)
                continue;

            if (string.Equals(sibling.name, anchorName, StringComparison.OrdinalIgnoreCase))
                return sibling;
        }

        return null;
    }

    /// <summary>Détermine le Transform à regarder lorsque la caméra doit suivre la cible.</summary>
    private Transform ResolveLookAtTarget()
    {
        if (targetAnchorOverride != null)
            return targetAnchorOverride;

        // Tentative 1 : exploiter les nouveaux points globaux "CMVPoint_OverShoulderLookTarget_*".
        // Ces points ne sont pas portés par l'unité directement, mais regroupés sous le parent
        // contenant toute l'escouade (alliée ou ennemie). On commence par interroger la cible,
        // puis on retente avec le lanceur (utile durant la préparation d'une action sans cible).
        Transform sharedLookAnchor = ResolveSharedLookTargetAnchor(currentTarget);
        if (sharedLookAnchor == null)
            sharedLookAnchor = ResolveSharedLookTargetAnchor(currentCaster);
        if (sharedLookAnchor != null)
            return sharedLookAnchor;

        if (currentTarget != null)
        {
            Transform reactionPoint = currentTarget.GetCameraAnchor("CMVPoint_TargetReaction");
            return reactionPoint != null ? reactionPoint : currentTarget.transform;
        }

        return null;
    }

    /// <summary>
    /// Met à jour l'interpolation de retour douce pour « CMV_OrbitAroundUnit » lorsque la caméra
    /// n'a plus de cible explicite. L'objectif est d'éviter un snap immédiat vers la position locale
    /// (0,0,0) du lanceur et de conserver une transition lisible pour le joueur.
    /// </summary>
    private void UpdateOrbitReturn(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        Transform fallbackAnchor = ResolveOrbitReturnAnchor();
        if (fallbackAnchor == null)
        {
            // Sans ancre de secours, on annule purement et simplement le lissage afin de ne pas
            // laisser l'état actif indéfiniment. La caméra reste alors sur sa dernière position connue.
            orbitReturnInProgress = false;
            orbitReturnTarget = null;
            return;
        }

        // Si la référence change (nouveau caster, Timeline spécifique, etc.), on relance l'interpolation
        // avec la nouvelle destination afin d'éviter un saut visuel.
        bool targetChanged = orbitReturnTarget != fallbackAnchor;
        if (!orbitReturnInProgress || targetChanged)
        {
            orbitReturnInProgress = true;
            orbitReturnElapsed = 0f;
            orbitReturnTarget = fallbackAnchor;
            orbitReturnStartPosition = camera.transform.position;
            orbitReturnStartRotation = camera.transform.rotation;
        }

        orbitReturnElapsed += Time.deltaTime;

        float duration = Mathf.Max(OrbitReturnDurationSeconds, 0.0001f);
        float t = Mathf.Clamp01(orbitReturnElapsed / duration);

        // Interpolation de type Lerp/Slerp pour une transition douce et déterministe.
        camera.transform.position = Vector3.Lerp(orbitReturnStartPosition, fallbackAnchor.position, t);
        camera.transform.rotation = Quaternion.Slerp(orbitReturnStartRotation, fallbackAnchor.rotation, t);

        if (orbitReturnElapsed >= duration)
        {
            // Une fois la transition terminée, on s'aligne définitivement sur l'ancre cible et on
            // libère l'état afin de permettre une future interpolation si nécessaire.
            camera.transform.SetPositionAndRotation(fallbackAnchor.position, fallbackAnchor.rotation);
            orbitReturnInProgress = false;
        }
    }

    /// <summary>
    /// Réinitialise les informations de retour orbital lorsque la caméra retrouve une cible classique.
    /// Sans cette étape, la prochaine perte de cible ne déclencherait pas correctement l'interpolation.
    /// </summary>
    private void ResetOrbitReturnStateIfNeeded(string cameraName)
    {
        if (!string.Equals(cameraName, OrbitAroundCameraName, StringComparison.OrdinalIgnoreCase))
            return;

        orbitReturnInProgress = false;
        orbitReturnTarget = null;
        orbitReturnElapsed = 0f;
    }

    /// <summary>
    /// Applique un léger mouvement sinusoïdal à la caméra sélectionnée afin de simuler une respiration.
    /// L'effet agit à la fois sur la position (verticale + latérale) et sur une micro-rotation
    /// (tangage + lacet). Les paramètres sont volontairement faibles pour conserver un cadrage lisible
    /// tout en ajoutant de la vie au plan.
    /// </summary>
    private void ApplyBreathingMotion(CinemachineCamera camera, string cameraName)
    {
        if (!enableBreathingMotion || camera == null)
            return; // 🛑 On ne modifie rien si l'effet est désactivé ou si la caméra n'existe pas.

        // 🕒 On convertit la fréquence (en oscillations par seconde) vers un angle en radians.
        float frequency = Mathf.Max(breathingFrequency, 0.0001f);
        float baseAngle = (Time.time * frequency * Mathf.PI * 2f) + ResolveBreathingPhase(cameraName);

        float sin = Mathf.Sin(baseAngle); // Valeur principale pour le mouvement vertical + tangage.
        float cos = Mathf.Cos(baseAngle); // Décalage en quadrature pour le mouvement latéral + lacet.

        // 🚶‍♀️ Translation douce selon les axes locaux (up/right) pour simuler le thorax qui se soulève.
        if (Mathf.Abs(breathingVerticalAmplitude) > 0.0001f || Mathf.Abs(breathingHorizontalAmplitude) > 0.0001f)
        {
            Vector3 offset = Vector3.zero;

            if (Mathf.Abs(breathingVerticalAmplitude) > 0.0001f)
                offset += camera.transform.up * (sin * breathingVerticalAmplitude);

            if (Mathf.Abs(breathingHorizontalAmplitude) > 0.0001f)
                offset += camera.transform.right * (cos * breathingHorizontalAmplitude);

            camera.transform.position += offset;
        }

        // 🔄 Micro-rotation pour accompagner la translation et éviter un mouvement purement rigide.
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

    /// <summary>
    /// Applique le décalage de tremblement précédemment calculé si la caméra correspond à celle touchée.
    /// </summary>
    private void ApplyDamageShake(CinemachineCamera camera, string cameraName)
    {
        if (!enableDamageShake || damageShakeDurationCurrent <= 0f || camera == null)
            return;

        if (!string.Equals(cameraName, damageShakeCameraName, StringComparison.OrdinalIgnoreCase))
            return; // ✅ Une autre caméra est active : on n'injecte aucune secousse ici.

        camera.transform.position += damageShakeOffset;
        camera.transform.rotation = damageShakeRotationOffset * camera.transform.rotation;
    }

    /// <summary>
    /// Calcule et mémorise un décalage de phase propre à chaque caméra pour l'effet de respiration.
    /// On se base sur le hash Unity du nom afin d'obtenir une valeur déterministe entre les sessions.
    /// </summary>
    private float ResolveBreathingPhase(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName))
            return 0f; // 🧭 Sans nom, on se replie sur une phase neutre.

        if (breathingPhaseOffsets.TryGetValue(cameraName, out float cachedPhase))
            return cachedPhase; // ✅ Valeur déjà connue : on la réutilise immédiatement.

        // 🔢 On réutilise Animator.StringToHash (rapide et déterministe) pour produire une graine.
        int hash = Animator.StringToHash(cameraName);

        // Le hash est converti en angle [0 ; 2π] afin d'introduire un décalage de phase stable.
        float phase = (hash & 0xFFFF) / 65535f * Mathf.PI * 2f;

        breathingPhaseOffsets[cameraName] = phase;
        return phase;
    }

    /// <summary>
    /// Détermine l'ancre vers laquelle la caméra orbitale doit revenir lorsque aucune cible n'est suivie.
    /// On privilégie les overrides explicites configurés par le gameplay (casterAnchorOverride) avant de
    /// retomber sur l'ancre standard « CMVPoint_OrbitAroundUnit » du lanceur, puis sur son transform racine.
    /// </summary>
    private Transform ResolveOrbitReturnAnchor()
    {
        if (targetAnchorOverride != null)
            return targetAnchorOverride;

        if (casterAnchorOverride != null)
            return casterAnchorOverride;

        CharacterUnit fallbackUnit = currentCaster ?? currentTurnOwner;
        if (fallbackUnit == null)
            return null;

        Transform casterOrbitAnchor = fallbackUnit.GetCameraAnchor("CMVPoint_OrbitAroundUnit");
        if (casterOrbitAnchor != null)
            return casterOrbitAnchor;

        return fallbackUnit.transform;
    }

    /// <summary>
    /// Recherche les ancres communes "CMVPoint_OverShoulderLookTarget_*".
    /// </summary>
    /// <remarks>
    /// Ces points sont placés sur le parent contenant l'ensemble des combattants d'un camp.
    /// Ils permettent d'orienter la caméra épaulière vers un repère stable même lorsque la
    /// cible se déplace ou disparaît momentanément. Chaque point est suffixé par un indice
    /// (01 à 03) correspondant à la position de l'unité dans la hiérarchie.
    /// </remarks>
    private Transform ResolveSharedLookTargetAnchor(CharacterUnit referenceUnit)
    {
        if (referenceUnit == null)
            return null;

        Transform parent = referenceUnit.transform != null ? referenceUnit.transform.parent : null;
        if (parent == null)
            return null;

        bool isPlayer = referenceUnit.Data != null && referenceUnit.Data.isPlayerControlled;

        // On détermine l'indice de l'unité à l'intérieur de son groupe (1..N) afin de sélectionner
        // l'ancre dédiée. L'ordre est simplement basé sur la hiérarchie des enfants du parent commun.
        int slotIndex = ResolveUnitSlotIndex(referenceUnit, parent, isPlayer);
        if (slotIndex <= 0)
            return null;

        string prefix = isPlayer
            ? "CMVPoint_OverShoulderLookTarget_Player_"
            : "CMVPoint_OverShoulderLookTarget_Enemy_";
        string anchorName = prefix + slotIndex.ToString("00");

        // Les ancres peuvent être nichées à n'importe quel niveau sous le parent : on parcourt
        // récursivement la hiérarchie en incluant les objets désactivés pour couvrir tous les cas.
        foreach (var child in parent.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child == null)
                continue;

            if (string.Equals(child.name, anchorName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Calcule la position (1..N) de l'unité au sein de son groupe afin de choisir l'ancre adaptée.
    /// </summary>
    private static int ResolveUnitSlotIndex(CharacterUnit referenceUnit, Transform groupParent, bool isPlayer)
    {
        if (referenceUnit == null || groupParent == null)
            return -1;

        int index = 0;
        foreach (Transform child in groupParent)
        {
            if (child == null)
                continue;

            CharacterUnit childUnit = child.GetComponent<CharacterUnit>();
            if (childUnit == null || childUnit.Data == null)
                continue;

            if (childUnit.Data.isPlayerControlled != isPlayer)
                continue; // On ignore les unités appartenant au camp opposé.

            index++;

            if (childUnit == referenceUnit)
                return index;
        }

        return -1;
    }

    /// <summary>Enregistre l'unité actuellement active (tour en cours).</summary>
    public void SetTurnOwner(CharacterUnit unit, bool alsoSetAsCaster = true)
    {
        currentTurnOwner = unit;
        if (alsoSetAsCaster && unit != null)
            currentCaster = unit;

        // 🎨 Synchronise immédiatement le filtre d'urgence pour refléter l'état de santé
        // de la SquadUnit active. L'appel est sécurisé si aucun filtre n'est présent.
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
    public void ConfigureActionTargets(
        CharacterUnit caster,
        CharacterUnit target,
        Vector3? midpoint = null,
        Transform casterAnchor = null,
        Transform targetAnchor = null)
    {
        currentCaster = caster ?? currentCaster;
        currentTarget = target;
        casterAnchorOverride = casterAnchor;
        targetAnchorOverride = targetAnchor;

        RefreshAllCameraPlacements();
    }

    /// <summary>Efface les informations associées au move en cours.</summary>
    public void ClearRigTargets()
    {
        casterAnchorOverride = null;
        targetAnchorOverride = null;
        currentCaster = null;
        currentTarget = null;

        RefreshAllCameraPlacements();
    }

    /// <summary>Active une caméra via son rôle cinématique.</summary>
    public void SwitchToCamera(
        BattleCameraRole role,
        float blendTime = -1f,
        CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (role == BattleCameraRole.None)
        {
            currentRole = BattleCameraRole.None;
            currentCameraName = null;
            DisplayCameraWithBlend(null, blendTime, overrideStyle);
            return;
        }

        if (!DefaultRoleToCameraName.TryGetValue(role, out string cameraName))
        {
            Debug.LogWarning($"[BattleCameraManager] Aucun GameObject associé au rôle caméra {role}.");
            return;
        }

        currentRole = role;
        SwitchToCamera(cameraName, blendTime, overrideStyle);
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
            currentCameraName = null;
            DisplayCameraWithBlend(null, blendTime, overrideStyle);
            return;
        }

        RefreshCameraPlacement(cameraName, CameraBindings.TryGetValue(cameraName, out var config)
            ? config
            : new CameraBindingConfig(CameraAnchorOwner.Caster, cameraName.Replace("CMV_", string.Empty), false));

        currentCameraName = cameraName;
        DisplayCameraWithBlend(cameraName, blendTime, overrideStyle);
    }

    /// <summary>Replace la caméra ciblée avant de lui donner la priorité.</summary>
    public void SwitchToCameraAndAlign(
        string cameraName,
        Transform anchor,
        float blendTime = -1f,
        CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        // Les ancres manuelles appartiennent à l'ancien système : on déclenche simplement un rafraîchissement automatique.
        if (!string.IsNullOrEmpty(cameraName) && CameraBindings.TryGetValue(cameraName, out var config))
            RefreshCameraPlacement(cameraName, config);
        else if (anchor != null && TryGetCameraByName(cameraName, out var manualCamera))
            manualCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        SwitchToCamera(cameraName, blendTime, overrideStyle);
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

    /// <summary>
    /// Déclenche une secousse de caméra lorsque l'escouade reçoit un coup, afin de renforcer l'impact.
    /// </summary>
    /// <param name="damagedUnit">Unité blessée afin de déterminer l'orientation de l'effet.</param>
    /// <param name="devastatingHit">Indique si le coup est considéré comme particulièrement violent.</param>
    /// <param name="attacker">Transform de l'assaillant pour accentuer la direction de l'onde.</param>
    public void TriggerDamageShake(CharacterUnit damagedUnit, bool devastatingHit, Transform attacker = null)
    {
        if (!enableDamageShake || damagedUnit == null || !blendSwitcher || !blendSwitcher.HasActiveCamera)
            return;

        string activeCameraName = blendSwitcher.CurrentCameraName;
        if (string.IsNullOrEmpty(activeCameraName))
            return;

        damageShakeCameraName = activeCameraName;
        damageShakeDurationCurrent = Mathf.Max(damageShakeDuration, 0.01f);
        damageShakeElapsed = 0f;
        damageShakeIntensity = devastatingHit ? damageShakeDevastatingMultiplier : 1f;
        damageShakeOffset = Vector3.zero;
        damageShakeRotationOffset = Quaternion.identity;
        damageShakePrimaryPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        damageShakeSecondaryPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        damageShakeRight = ResolveDamageShakeLateralAxis(damagedUnit, attacker, activeCameraName);
        damageShakeUp = Vector3.up;
    }

    /// <summary>
    /// Détermine l'axe horizontal privilégié pour le tremblement en combinant direction d'attaque
    /// et orientation de la caméra active.
    /// </summary>
    private Vector3 ResolveDamageShakeLateralAxis(CharacterUnit damagedUnit, Transform attacker, string cameraName)
    {
        if (damagedUnit == null)
            return Vector3.right;

        // 1️⃣ Tentative : utiliser la direction d'impact afin de retranscrire la poussée du coup.
        if (attacker != null)
        {
            Vector3 attackDirection = damagedUnit.transform.position - attacker.position;
            if (attackDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, attackDirection.normalized);
                if (lateral.sqrMagnitude > 0.0001f)
                    return lateral.normalized;
            }
        }

        // 2️⃣ Fallback : se baser sur la position de la caméra active pour conserver une lecture cohérente.
        if (!string.IsNullOrEmpty(cameraName) && TryGetCameraByName(cameraName, out var camera) && camera != null)
        {
            Vector3 toCamera = camera.transform.position - damagedUnit.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, toCamera.normalized);
                if (lateral.sqrMagnitude > 0.0001f)
                    return lateral.normalized;
            }

            Vector3 cameraRight = camera.transform.right;
            if (cameraRight.sqrMagnitude > 0.0001f)
                return cameraRight.normalized;
        }

        // 3️⃣ Dernier recours : axe monde fixe afin de garantir un comportement déterministe.
        return Vector3.right;
    }

    /// <summary>Garantit que la caméra d'introduction est bien référencée.</summary>
    private bool EnsureIntroCameraReference()
    {
        if (introCameraTransform != null)
            return true;

        if (!TryGetCameraByName(IntroCameraName, out var camera) || camera == null)
            return false;

        introCameraTransform = camera.transform;
        introCameraInitialLocalPosition = introCameraTransform.localPosition;
        introCameraInitialLocalRotation = introCameraTransform.localRotation;
        return true;
    }

    /// <summary>Mémorise la position de base de la caméra d'introduction dès que possible.</summary>
    private void CacheIntroCameraReference()
    {
        if (!TryGetCameraByName(IntroCameraName, out var camera) || camera == null)
        {
            introCameraTransform = null;
            return;
        }

        introCameraTransform = camera.transform;
        introCameraInitialLocalPosition = introCameraTransform.localPosition;
        introCameraInitialLocalRotation = introCameraTransform.localRotation;
    }

    /// <summary>Démarre le travelling linéaire de la caméra d'introduction.</summary>
    public void StartBattleIntroCameraTravel(float moveSpeed = 1f, Vector3? lookAtPoint = null)
    {
        if (!EnsureIntroCameraReference())
            return;

        if (introCameraMoveRoutine != null)
        {
            StopCoroutine(introCameraMoveRoutine);
            introCameraMoveRoutine = null;
        }

        if (introCameraResetRoutine != null)
        {
            StopCoroutine(introCameraResetRoutine);
            introCameraResetRoutine = null;
        }

        introCameraTransform.localPosition = introCameraInitialLocalPosition;
        introCameraTransform.localRotation = introCameraInitialLocalRotation;

        Vector3 targetPoint = lookAtPoint ?? Vector3.zero;
        Vector3 lookDirection = targetPoint - introCameraTransform.position;
        if (lookDirection.sqrMagnitude > IntroLookDirectionThreshold)
            introCameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        introCameraMoveRoutine = StartCoroutine(IntroCameraTravelRoutine(moveSpeed, targetPoint));
    }

    /// <summary>Stoppe le travelling de la caméra d'introduction et programme sa remise en place.</summary>
    public void StopBattleIntroCameraTravel(float delayBeforeReset = 0f)
    {
        if (introCameraMoveRoutine != null)
        {
            StopCoroutine(introCameraMoveRoutine);
            introCameraMoveRoutine = null;
        }

        if (introCameraTransform == null)
            return;

        if (introCameraResetRoutine != null)
        {
            StopCoroutine(introCameraResetRoutine);
            introCameraResetRoutine = null;
        }

        if (delayBeforeReset <= 0f)
        {
            introCameraTransform.localPosition = introCameraInitialLocalPosition;
            introCameraTransform.localRotation = introCameraInitialLocalRotation;
        }
        else
        {
            introCameraResetRoutine = StartCoroutine(ResetIntroCameraAfterDelay(delayBeforeReset));
        }
    }

    /// <summary>Coroutine appliquant le déplacement linéaire tout en gardant un point de mire fixe.</summary>
    private IEnumerator IntroCameraTravelRoutine(float moveSpeed, Vector3 lookAtPoint)
    {
        while (true)
        {
            if (introCameraTransform == null)
                yield break;

            float delta = Time.unscaledDeltaTime;
            Vector3 worldPosition = introCameraTransform.position;
            worldPosition += Vector3.forward * moveSpeed * delta;
            introCameraTransform.position = worldPosition;

            Vector3 lookDirection = lookAtPoint - worldPosition;
            if (lookDirection.sqrMagnitude > IntroLookDirectionThreshold)
                introCameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            yield return null;
        }
    }

    /// <summary>Ramène la caméra d'introduction sur ses coordonnées d'origine après un fondu.</summary>
    private IEnumerator ResetIntroCameraAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

        if (introCameraTransform != null)
        {
            introCameraTransform.localPosition = introCameraInitialLocalPosition;
            introCameraTransform.localRotation = introCameraInitialLocalRotation;
        }

        introCameraResetRoutine = null;
    }
}
