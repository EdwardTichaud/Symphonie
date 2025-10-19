using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Permet de distinguer les CinemachineCamera des Camera classiques pour éviter les conflits de pilotage.
using UnityEngine.InputSystem; // Accès direct aux périphériques afin de gérer la caméra TPS (stick droit, souris, molette...).

public enum WorldCameraState
{
    ThirdPerson,
    OrbitAround
}

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private Coroutine currentTransition;

    /// <summary>
    /// Indique si le contrôleur de caméra est volontairement mis en pause.
    /// Lorsque cette valeur est vraie, aucune logique de suivi ou d'input
    /// n'est exécutée afin de laisser une Timeline (ou un autre système)
    /// piloter librement la WorldCamera.
    /// </summary>
    private bool isPaused = false;

    /// <summary>
    /// Lecture publique de l'état de pause. Pratique pour les systèmes qui
    /// souhaitent vérifier si la caméra est disponible avant de lancer un
    /// comportement spécifique.
    /// </summary>
    public bool IsPaused => isPaused;

    [Header("---------- Common ----------")]

    [Header("Managed Cameras")]
    public List<Camera> managedCameras = new();
    public WorldCameraState currentWorldCameraState = WorldCameraState.ThirdPerson; // ✅ Par défaut en vue TPS libre

    private Transform forcedLookTarget;         // Cible privilégiée (Point_Chest) si disponible
    private Camera activeCamera;
    private Camera worldCamera; // Référence directe à la WorldCamera
    private Transform worldCameraParent;       // Parent direct de la WorldCamera (utilisé pour le forçage)

    // Sauvegarde de la WorldCamera pour assurer la continuité après combats ou timelines
    private Vector3 savedWorldCamPosition;
    private Quaternion savedWorldCamRotation;
    private Vector3 savedWorldCamParentPosition;
    private Quaternion savedWorldCamParentRotation;

    [Header("---------- Effet de respiration ----------")]
    [Tooltip("Amplitude du mouvement vertical simulant la respiration.")]
    [SerializeField] private float breathingAmplitude = 0.05f;
    [Tooltip("Fréquence de l'oscillation de respiration.")]
    [SerializeField] private float breathingFrequency = 1f;

    [Tooltip("Autorise l'effet de respiration sur la WorldCamera. Pratique à désactiver pour aligner WorldCam_Cam sur WorldCam_Origin lors du debug ou des Timeline.")]
    [SerializeField] private bool enableWorldCameraBreathing = true;
    [Tooltip("Autorise l'effet de respiration sur la BattleCamera. À couper si la caméra est gérée par Cinemachine.")]
    [SerializeField] private bool enableBattleCameraBreathing = true;

    private GameObject battleCamera;               // Référence directe à la BattleCamera
    private Transform battleCameraParent;      // Parent direct de la BattleCamera (utilisé pour le forçage)
    private bool battleCameraSupportsBreathing; // Indique si l'effet de respiration peut être appliqué sans perturber Cinemachine.
    private float worldBreathOffset;           // Dernier décalage appliqué à la WorldCamera elle-même
    private float battleBreathOffset;          // Dernier décalage appliqué à la BattleCamera elle-même
    private Vector3 worldCameraBaseLocalPosition;    // Position locale de référence (sans respiration)
    private bool worldCameraBaseCaptured;             // Flag garantissant que la base a bien été lue une première fois
    private Vector3 battleCameraBaseLocalPosition;   // Position locale de référence pour la BattleCamera
    private bool battleCameraBaseCaptured;            // Flag pour éviter de relire la base à chaque LateUpdate

    [Header("---------- World Camera ----------")]

    [Header("Third Person Settings")]
    [Tooltip("Décalage appliqué au point de focus (souvent le torse du joueur).")]
    [SerializeField] private Vector3 thirdPersonFocusOffset = new(0f, 1.6f, 0f);
    [Tooltip("Référence explicite pour le focus (prioritaire sur l'offset). Laisser vide pour utiliser automatiquement Point_Chest.")]
    public Transform forceLookPoint;
    [Tooltip("Vitesse de rotation appliquée aux axes du stick droit / pad souris (degrés par seconde).")]
    public float forcedCamRotationSpeed = 120f;
    [Tooltip("Sensibilité de rotation appliquée au delta souris (degrés par pixel).")]
    [SerializeField] private float mouseRotationSensitivity = 0.15f;
    [Tooltip("Sensibilité appliquée à la molette de souris (unités de distance par cran).")]
    [SerializeField] private float mouseZoomSensitivity = 0.05f;
    [Tooltip("Vitesse de zoom par seconde quand on utilise les gâchettes de la manette.")]
    [SerializeField] private float gamepadZoomSpeed = 2f;
    [Tooltip("Active l'inversion de l'axe vertical pour la caméra TPS.")]
    [SerializeField] private bool invertLookY = false;
    [Tooltip("N'autorise la rotation souris que lorsque le bouton droit est maintenu (comme dans de nombreux TPS PC).")]
    [SerializeField] private bool requireRightClickForMouseRotation = true;
    [Tooltip("Facteur d'assouplissement de suivi. Plus la valeur est élevée, plus la caméra colle vite au joueur.")]
    [SerializeField] private float forcedCamFollowLerpSpeed = 10f;
    [Tooltip("Distance minimum autorisée entre la caméra et la cible.")]
    public float forcedCamMinDistance = 2f;
    [Tooltip("Distance maximum autorisée entre la caméra et la cible.")]
    public float forcedCamMaxDistance = 10f;
    [Tooltip("Inclinaison minimale (en degrés). Valeur négative = regarder vers le bas.")]
    public float forcedCamMinPitch = -35f;
    [Tooltip("Inclinaison maximale (en degrés). Valeur positive = regarder vers le haut.")]
    public float forcedCamMaxPitch = 65f;

    private string cameraTargetName;
    private Transform player;
    private EventsManager eventsManager;

    private float forcedCamYaw = 180f;     // 180° = caméra positionnée derrière le joueur au démarrage
    private float forcedCamPitch = 20f;
    private float forcedCamDistance = 5f;
    private Vector3 forcedCamOffset = Vector3.zero; // Décalage monde utilisé en mode TPS

    // Toute la gestion des collisions avec le sol a été supprimée pour alléger le système.
    // L'occlusion reste assurée par d'autres composants si un obstacle cache le joueur.

    [Header("Orbit Settings")]
    public Transform orbitTarget;
    public float orbitDistance = 5f;
    public float orbitSpeed = 30f;
    public bool orbitX, orbitY = true, orbitZ;

    //[Header("---------- WorldCamera ----------")]

    #region Initialisation
    /// <summary>
    /// Prépare les références et recherches initiales. Conflit possible si plusieurs contrôleurs existent.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraController] Instance en double détectée, destruction de l'objet en trop.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogError("[CameraController] Player not found!");

        // Récupération de la WorldCamera pour pouvoir la forcer indépendamment de la MainCamera
        worldCamera = GameObject.FindGameObjectWithTag("WorldCamera")?.GetComponent<Camera>();
        if (worldCamera == null) Debug.LogWarning("[CameraController] WorldCamera introuvable !");
        // Récupère le parent pour appliquer les déplacements forcés (suivi, transitions...)
        worldCameraParent = worldCamera != null && worldCamera.transform.parent != null
            ? worldCamera.transform.parent
            : worldCamera?.transform; // fallback si aucun parent

        if (worldCamera != null)
        {
            // On capture dès maintenant la position locale de référence afin de pouvoir remettre
            // WorldCam_Cam pile sur WorldCam_Origin quand l'effet de respiration est coupé.
            worldCameraBaseLocalPosition = worldCamera.transform.localPosition;
            worldCameraBaseCaptured = true;
        }

        // Initialise les valeurs sauvegardées pour pouvoir restaurer la caméra plus tard
        if (worldCamera != null)
        {
            savedWorldCamPosition = worldCamera.transform.position;
            savedWorldCamRotation = worldCamera.transform.rotation;
        }
        if (worldCameraParent != null)
        {
            savedWorldCamParentPosition = worldCameraParent.position;
            savedWorldCamParentRotation = worldCameraParent.rotation;
        }

        // Recherche de la BattleCamera et de son parent pour gérer séparément forçage et respiration
        battleCamera = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCamera == null)
        {
            Debug.LogWarning("[CameraController] BattleCamera introuvable !");
            battleCameraSupportsBreathing = false; // Sans référence valide on évite toute tentative d'animation.
        }
        else
        {
            battleCameraParent = battleCamera.transform.parent != null
                ? battleCamera.transform.parent
                : battleCamera.transform;

            // Idem que pour la WorldCamera : on mémorise la position locale de base pour
            // pouvoir supprimer proprement le décalage de respiration en cas de besoin.
            battleCameraBaseLocalPosition = battleCamera.transform.localPosition;
            battleCameraBaseCaptured = true;

            // Vérifie si l'objet tagué "BattleCamera" est une caméra Unity classique ou une entité Cinemachine
            // (caméra virtuelle ou caméra physique contrôlée par un CinemachineBrain).
            // Les entités Cinemachine sont désormais pilotées par BattleCameraManager : appliquer ici l'effet de respiration
            // provoquerait une lutte de positions (saccades observées en jeu).
            // On ne conserve donc l'effet que pour les caméras physiques libres.
            bool hasUnityCameraComponent = battleCamera.TryGetComponent(out Camera _);
            bool hasCinemachineComponent = battleCamera.TryGetComponent(out CinemachineCamera _);
            bool hasCinemachineBrain = battleCamera.TryGetComponent(out CinemachineBrain _);

            battleCameraSupportsBreathing = hasUnityCameraComponent && !hasCinemachineComponent && !hasCinemachineBrain;

            if (!battleCameraSupportsBreathing && (hasCinemachineComponent || hasCinemachineBrain))
            {
                // Par sécurité on remet l'offset à zéro afin d'éviter toute valeur résiduelle si l'on change de scène.
                battleBreathOffset = 0f;
                // Journalisation détaillée pour diagnostiquer plus facilement les conflits de caméra dans le futur.
                Debug.Log("[CameraController] Effet de respiration désactivé pour la BattleCamera car elle est pilotée par Cinemachine.");
            }
        }

        forcedLookTarget = FindChildRecursive(player, "Point_Chest");
        cameraTargetName = forcedLookTarget?.name;
        eventsManager = FindFirstObjectByType<EventsManager>();

        RecalculateThirdPersonOffset();
        FindManagedCameras();
    }

    /// <summary>
    /// Synchronise les points de caméra dans l'éditeur. Aucune redondance connue.
    /// </summary>
    void OnValidate()
    {
        FindManagedCameras();
    }

    #endregion

    #region Boucle Principale
    /// <summary>
    /// Gère l'état de la caméra. La logique de suivi a été déplacée en <c>LateUpdate</c>
    /// afin de garantir que le joueur a terminé son déplacement avant d'ajuster la caméra,
    /// ce qui évite les saccades lorsqu'il change brusquement de direction.
    /// </summary>
    void Update()
    {
        bool timelinePlaying = TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying;

        if (timelinePlaying && !isPaused)
        {
            // Une Timeline vient de prendre la main : on se met immédiatement en pause
            // pour éviter que nos transitions influencent la caméra animée.
            PauseController();
        }

        if (isPaused)
            return; // La caméra est figée (Timeline, zone spéciale, etc.).

        // ✅ Si la MainCamera est désactivée → skip toute logique
        if (Camera.main != null && !Camera.main.enabled)
            return;

        // Toute la logique de suivi (HandleCameraBehaviour, UpdateOrbit...) est
        // désormais appliquée dans LateUpdate pour une meilleure fluidité.
    }

    /// <summary>
    /// Effectue les ajustements de caméra en fin de frame pour éviter les conflits
    /// et assurer un suivi plus smooth.
    /// </summary>
    void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        bool timelinePlaying = TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying;

        if (timelinePlaying && !isPaused)
        {
            // Sécurité supplémentaire : si la Timeline démarre entre Update et
            // LateUpdate, on applique tout de même la pause pour éviter un frame parasite.
            PauseController();
        }

        if (isPaused)
            return; // Aucun suivi tant que la pause est active.

        // Mise à jour des références dynamiques (player, eventsManager)
        player ??= GameObject.FindGameObjectWithTag("Player")?.transform;
        eventsManager ??= FindFirstObjectByType<EventsManager>();

        // 🔁 Mise à jour de l'orbite ou suivi classique selon l'état courant
        if (currentWorldCameraState == WorldCameraState.OrbitAround && orbitTarget != null && activeCamera != null)
        {
            UpdateOrbit();
        }
        else
        {
            HandleCameraBehaviour();
        }

        // Applique le léger mouvement de respiration directement sur les GameObjects caméra
        // (leurs parents restent libres pour recevoir les déplacements forcés)
        ApplyBreathing(
            worldCamera != null ? worldCamera.transform : null,
            ref worldBreathOffset,
            enableWorldCameraBreathing,
            ref worldCameraBaseLocalPosition,
            ref worldCameraBaseCaptured
        );
        // Si la BattleCamera est contrôlée par Cinemachine, on évite tout déplacement manuel
        // pour ne pas contrarier BattleCameraManager.
        Transform battleBreathingTarget = battleCameraSupportsBreathing && battleCamera != null
            ? battleCamera.transform
            : null;
        ApplyBreathing(
            battleBreathingTarget,
            ref battleBreathOffset,
            enableBattleCameraBreathing,
            ref battleCameraBaseLocalPosition,
            ref battleCameraBaseCaptured
        );
    }

    /// <summary>
    /// Fait tourner la caméra autour de la cible. Incompatible avec PathFollow actif.
    /// </summary>
    void UpdateOrbit()
    {
        Vector3 axis = new Vector3(orbitX ? 1f : 0f, orbitY ? 1f : 0f, orbitZ ? 1f : 0f);
        if (axis == Vector3.zero) return;

        axis.Normalize();
        float angle = orbitSpeed * Time.deltaTime;
        activeCamera.transform.RotateAround(orbitTarget.position, axis, angle);

        Vector3 offset = activeCamera.transform.position - orbitTarget.position;
        activeCamera.transform.position = orbitTarget.position + offset.normalized * orbitDistance;

        activeCamera.transform.LookAt(orbitTarget);
    }

    #endregion

    #region Mode Orbite
    /// <summary>
    /// Démarre un mouvement orbital autour d'une cible. Conflit avec PathFollow et ForceCam.
    /// </summary>
    public void OrbitAround(string cameraTag, Transform target, float distance = 5f, float speed = 30f, bool x = false, bool y = true, bool z = false)
    {
        StopOrbit();

        Camera cam = FindCameraByTag(cameraTag);
        if (cam == null)
        {
            Debug.LogError($"[CameraController] Aucun Camera trouvé avec le tag '{cameraTag}' !");
            return;
        }

        activeCamera = cam;

        orbitTarget = target;
        orbitDistance = distance;
        orbitSpeed = speed;
        orbitX = x;
        orbitY = y;
        orbitZ = z;

        if (orbitTarget != null)
        {
            Vector3 dir = (activeCamera.transform.position - orbitTarget.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            activeCamera.transform.position = orbitTarget.position + dir * orbitDistance;
            activeCamera.transform.LookAt(orbitTarget);
        }

        currentWorldCameraState = WorldCameraState.OrbitAround; // ✅ Maintenant géré par l'état
        Debug.Log("[CameraController] OrbitAround démarré !");
    }

    /// <summary>
    /// Interrompt le mouvement orbital et rend la caméra au mode TPS classique.
    /// </summary>
    public void StopOrbit()
    {
        orbitTarget = null;
        if (currentWorldCameraState == WorldCameraState.OrbitAround)
        {
            currentWorldCameraState = WorldCameraState.ThirdPerson;
        }
        Debug.Log("[CameraController] OrbitAround stoppé.");
    }

    #endregion

    #region Recherche Caméra
    /// <summary>
    /// Retourne la caméra correspondant au tag fourni.
    /// </summary>
    private Camera FindCameraByTag(string cameraTag)
    {
        foreach (Camera cam in managedCameras)
        {
            if (cam != null && cam.CompareTag(cameraTag))
                return cam;
        }
        return null;
    }

    /// <summary>
    /// Met à jour la liste des caméras gérées par cet objet.
    /// </summary>
    private void FindManagedCameras()
    {
        managedCameras.Clear();
        Camera[] allCams = GetComponentsInChildren<Camera>(true);
        managedCameras.AddRange(allCams);
    }

    /// <summary>
    /// Déplace la caméra principale vers la position et la rotation cibles avec interpolation.
    /// Peut interférer avec ForceCam si celui-ci est actif.
    /// </summary>
    public Coroutine SetCameraTarget(string positionName, string lookAtName, float transitionSpeed = 2f)
    {
        StopOrbit();

        Transform pos = GameObject.Find(positionName)?.transform;
        Transform look = GameObject.Find(lookAtName)?.transform;

        if (pos == null || look == null)
        {
            Debug.LogWarning($"[CameraController] Not found: {positionName}, {lookAtName}");
            return null;
        }

        Vector3 desiredPos = pos.position;
        Quaternion desiredRot = Quaternion.LookRotation(look.position - desiredPos);

        if (currentTransition != null) StopCoroutine(currentTransition);
        currentTransition = StartCoroutine(SmoothMoveAndLook(Camera.main.transform.parent, desiredPos, desiredRot, transitionSpeed));
        return currentTransition;
    }

    /// <summary>
    /// Coroutine interne pour interpoler position et rotation.
    /// </summary>
    IEnumerator SmoothMoveAndLook(Transform targetTransform, Vector3 targetPos, Quaternion targetRot, float speed)
    {
        float t = 0f;
        Vector3 startPos = targetTransform.position;
        Quaternion startRot = targetTransform.rotation;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * speed;
            targetTransform.position = Vector3.Lerp(startPos, targetPos, t);
            targetTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        targetTransform.position = targetPos;
        targetTransform.rotation = targetRot;
        currentTransition = null;
    }

    #endregion

    #region Comportement
    /// <summary>
    /// Sélectionne quelle logique appliquer selon l'état courant.
    /// </summary>
    void HandleCameraBehaviour()
    {
        switch (currentWorldCameraState)
        {
            case WorldCameraState.ThirdPerson:
                UpdateThirdPersonControls();
                FollowThirdPersonCamera();
                break;

            default:
                Debug.LogWarning($"[CameraController] Unhandled WorldCameraState: {currentWorldCameraState}");
                break;
        }
    }

    /// <summary>
    /// Suit le joueur en vue TPS en interpolant position et rotation pour un mouvement fluide.
    /// </summary>
    void FollowThirdPersonCamera()
    {
        if (worldCamera == null || player == null) return;

        Transform camOrigin = worldCameraParent != null ? worldCameraParent : worldCamera.transform;
        Vector3 focusPoint = GetThirdPersonFocusPoint();
        Vector3 desiredPos = focusPoint + forcedCamOffset;
        Quaternion desiredRot = Quaternion.LookRotation(focusPoint - desiredPos, Vector3.up);

        // Conversion de la vitesse en facteur indépendant du framerate pour conserver une sensation constante.
        float smoothingFactor = 1f - Mathf.Exp(-forcedCamFollowLerpSpeed * Time.deltaTime);

        camOrigin.position = Vector3.Lerp(camOrigin.position, desiredPos, smoothingFactor);
        camOrigin.rotation = Quaternion.Slerp(camOrigin.rotation, desiredRot, smoothingFactor);
    }

    /// <summary>
    /// Lit les entrées (stick droit, souris, zoom) et recalcule la position relative de la caméra.
    /// </summary>
    void UpdateThirdPersonControls()
    {
        if (player == null) return;

        InputsManager inputsManager = InputsManager.Instance;
        Vector2 lookInput = Vector2.zero;

        if (inputsManager != null)
        {
            lookInput = inputsManager.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();
        }

        if (lookInput.sqrMagnitude > 0.0001f)
        {
            forcedCamYaw += lookInput.x * forcedCamRotationSpeed * Time.deltaTime;
            float vertical = lookInput.y * forcedCamRotationSpeed * Time.deltaTime;
            forcedCamPitch += invertLookY ? vertical : -vertical;
        }

        if (Mouse.current != null)
        {
            bool allowMouseRotation = !requireRightClickForMouseRotation
                || Mouse.current.rightButton.isPressed
                || Cursor.lockState == CursorLockMode.Locked;

            if (allowMouseRotation)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * mouseRotationSensitivity;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    forcedCamYaw += delta.x;
                    forcedCamPitch += invertLookY ? delta.y : -delta.y;
                }
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                forcedCamDistance -= scroll * mouseZoomSensitivity;
            }
        }

        if (Gamepad.current != null)
        {
            float triggerDelta = Gamepad.current.leftTrigger.ReadValue() - Gamepad.current.rightTrigger.ReadValue();
            if (!Mathf.Approximately(triggerDelta, 0f))
            {
                forcedCamDistance += triggerDelta * gamepadZoomSpeed * Time.deltaTime;
            }
        }

        forcedCamYaw = Mathf.Repeat(forcedCamYaw, 360f);
        forcedCamPitch = Mathf.Clamp(forcedCamPitch, forcedCamMinPitch, forcedCamMaxPitch);
        forcedCamDistance = Mathf.Clamp(forcedCamDistance, forcedCamMinDistance, forcedCamMaxDistance);

        RecalculateThirdPersonOffset();
    }

    #endregion

    #region Utilitaires
    /// <summary>
    /// Met en pause toutes les logiques internes du contrôleur afin de laisser
    /// un système externe (Timeline, zone 2D, script temporaire) manipuler la caméra.
    /// </summary>
    public void PauseController()
    {
        if (isPaused)
            return; // Déjà en pause, on évite une double initialisation.

        isPaused = true;

        // On stoppe toute transition en cours pour éviter qu'une coroutine continue
        // de déplacer la caméra en arrière-plan pendant la cinématique.
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }
    }

    /// <summary>
    /// Relance la logique du contrôleur une fois que les systèmes externes
    /// ont terminé de manipuler la caméra.
    /// </summary>
    public void ResumeController()
    {
        if (!isPaused)
            return; // Rien à faire si la caméra fonctionnait déjà normalement.

        isPaused = false;

        // Dès la prochaine Update, la caméra recalcule son offset et reprend
        // son suivi standard (TPS, orbite, etc.). Aucun autre traitement n'est
        // nécessaire ici : les méthodes existantes se chargeront du reste.
    }

    /// <summary>
    /// Recalcule l'offset de la caméra TPS à partir de l'angle actuel et de la distance.
    /// Centralise la conversion sphérique → cartésienne pour faciliter les modifications.
    /// </summary>
    void RecalculateThirdPersonOffset()
    {
        // Conversion des angles en radians pour les fonctions trigonométriques
        float yawRad = forcedCamYaw * Mathf.Deg2Rad;
        float pitchRad = forcedCamPitch * Mathf.Deg2Rad;

        // Calcule l'offset relatif dans l'espace monde
        forcedCamOffset = new Vector3(
            forcedCamDistance * Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            forcedCamDistance * Mathf.Sin(pitchRad),
            forcedCamDistance * Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        );
    }

    /// <summary>
    /// Détermine dynamiquement le point de focus utilisé par la caméra TPS.
    /// </summary>
    Vector3 GetThirdPersonFocusPoint()
    {
        if (player == null)
            return worldCameraParent != null ? worldCameraParent.position : Vector3.zero;

        Transform focusTarget = ResolveThirdPersonLookTarget();

        if (focusTarget != null && focusTarget != player)
            return focusTarget.position;

        // Fallback : on utilise la racine du joueur accompagnée d'un léger offset configurable.
        return player.position + thirdPersonFocusOffset;
    }

    /// <summary>
    /// Garantit que l'on dispose d'un transform valide à regarder (Point_Chest, override, etc.).
    /// </summary>
    Transform ResolveThirdPersonLookTarget()
    {
        if (forceLookPoint != null)
            return forceLookPoint;

        if (player == null)
            return null;

        if (forcedLookTarget != null)
        {
            // Si le joueur a changé (nouvelle instance) ou que le point n'existe plus, on invalide la référence.
            if (!forcedLookTarget || (forcedLookTarget.root != null && player.root != null && forcedLookTarget.root != player.root))
            {
                forcedLookTarget = null;
            }
        }

        if (forcedLookTarget == null && !string.IsNullOrEmpty(cameraTargetName))
        {
            forcedLookTarget = FindChildRecursive(player, cameraTargetName);
        }

        if (forcedLookTarget == null)
        {
            forcedLookTarget = FindChildRecursive(player, "Point_Chest");
            if (forcedLookTarget != null)
                cameraTargetName = forcedLookTarget.name;
        }

        return forcedLookTarget != null ? forcedLookTarget : player;
    }

    /// <summary>
    /// Applique un léger mouvement sinusoïdal pour simuler la respiration.
    /// Doit être appelé sur le GameObject possédant la caméra afin de laisser
    /// son parent libre pour les translations forcées.
    /// </summary>
    void ApplyBreathing(Transform camHolder, ref float lastOffset, bool breathingEnabled, ref Vector3 baseLocalPosition, ref bool baseCaptured)
    {
        if (camHolder == null) return; // Sécurité si la caméra n'a pas été trouvée

        if (!baseCaptured)
        {
            // La toute première fois, on mémorise la position actuelle pour en faire notre point zéro.
            baseLocalPosition = camHolder.localPosition;
            baseCaptured = true;
        }
        else
        {
            // Si un autre système a déplacé la caméra (ex : Timeline, script temporaire),
            // on recalcule la base autour de la nouvelle position pour éviter un snap brutal.
            Vector3 expectedPosition = baseLocalPosition + Vector3.up * lastOffset;
            if ((camHolder.localPosition - expectedPosition).sqrMagnitude > 0.000001f)
            {
                baseLocalPosition = camHolder.localPosition - Vector3.up * lastOffset;
            }
        }

        if (!breathingEnabled)
        {
            // Si la respiration est désactivée, on s'assure que la caméra revient exactement
            // sur son point de base (indispensable pour que WorldCam_Cam recolle visuellement au pivot WorldCam_Origin).
            if (!Mathf.Approximately(lastOffset, 0f) || (camHolder.localPosition - baseLocalPosition).sqrMagnitude > 0.000001f)
            {
                camHolder.localPosition = baseLocalPosition;
                lastOffset = 0f;
            }
            return;
        }

        // Calcul du nouvel offset basé sur une sinusoïde pour simuler la respiration
        float newOffset = Mathf.Sin(Time.time * breathingFrequency) * breathingAmplitude;

        // Application absolue à partir du point de base pour éviter tout drift et pour faciliter la compréhension dans l'inspecteur.
        camHolder.localPosition = baseLocalPosition + Vector3.up * newOffset;

        // Mémorise l'offset pour le prochain frame
        lastOffset = newOffset;
    }

    /// <summary>
    /// Mémorise la position et la rotation actuelles de la WorldCamera ainsi que celles de son parent.
    /// À appeler juste avant qu'un combat ou qu'une Timeline ne prenne la main sur la caméra.
    /// </summary>
    public void SaveWorldCameraTransform()
    {
        if (worldCamera != null)
        {
            savedWorldCamPosition = worldCamera.transform.position;
            savedWorldCamRotation = worldCamera.transform.rotation;
        }

        if (worldCameraParent != null)
        {
            savedWorldCamParentPosition = worldCameraParent.position;
            savedWorldCamParentRotation = worldCameraParent.rotation;
        }
    }

    /// <summary>
    /// Replace la WorldCamera et son parent à la dernière position sauvegardée.
    /// Utilisé à la fin d'un combat ou d'une Timeline pour assurer la continuité de la vue.
    /// </summary>
    public void RestoreWorldCameraTransform()
    {
        if (worldCameraParent != null)
            worldCameraParent.SetPositionAndRotation(savedWorldCamParentPosition, savedWorldCamParentRotation);

        if (worldCamera != null)
            worldCamera.transform.SetPositionAndRotation(savedWorldCamPosition, savedWorldCamRotation);
    }

    /// <summary>
    /// Recherche récursive d'un enfant portant le nom indiqué.
    /// </summary>
    Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null) return result;
        }
        return null;
    }
    #endregion
}
