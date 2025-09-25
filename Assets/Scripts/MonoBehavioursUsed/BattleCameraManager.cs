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

    /// <summary>Replace une caméra donnée sur son point d'ancrage.</summary>
    private void RefreshCameraPlacement(string cameraName, CameraBindingConfig config)
    {
        if (!TryGetCameraByName(cameraName, out var camera) || camera == null)
            return;

        if (config.Owner == CameraAnchorOwner.Manual)
            return; // Caméras positionnées manuellement via l'inspecteur.

        CharacterUnit ownerUnit = ResolveAnchorOwner(config.Owner);
        if (ownerUnit == null)
            return;

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
            }
            else
            {
                camera.transform.rotation = anchor.rotation;
            }
        }
        else
        {
            camera.transform.rotation = anchor.rotation;
        }
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
        Transform anchor = unit.GetCameraAnchor(pointName);
        if (anchor == null && missingAnchorWarnings.Add(pointName))
            Debug.LogWarning($"[BattleCameraManager] Point '{pointName}' introuvable sur '{unit.name}'.");

        return anchor;
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
