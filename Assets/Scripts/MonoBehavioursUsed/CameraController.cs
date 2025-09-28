using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Permet de distinguer les CinemachineCamera des Camera classiques pour éviter les conflits de pilotage.

public enum WorldCameraState
{
    Forced,
    ResearchClosestCamPoint,
    OrbitAround
}

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private Coroutine currentTransition;

    [Header("---------- Common ----------")]

    [Header("Managed Cameras")]
    public List<Camera> managedCameras = new();
    public WorldCameraState currentWorldCameraState = WorldCameraState.ResearchClosestCamPoint; // ✅ Par défaut en recherche de point

    private bool forceLookAt;
    private Transform forcedLookTarget;
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

    [Header("Fixed Camera Points")]
    public bool cameraHandlerEnabled = true; // ✅ Par défaut activé
    public List<Transform> cameraPositions; // auto from LevelCameraHandler tag
    public Transform forceCamPoint, forceLookPoint;

    // Mémorise le dernier point de caméra appliqué pour éviter les transitions répétées
    private Transform lastClosestCameraPoint;

    private string cameraTargetName;
    private Transform player;
    private EventsManager eventsManager;

    [Header("Forced Camera Point Control")]
    public bool worldCamForced;

    public Transform forcedCameraPoint; // Ancien système, conservé pour compatibilité mais non utilisé
    public float forcedCamZoomSpeed = 5f;
    public float forcedCamRotationSpeed = 50f;
    public float forcedCamMinDistance = 2f;
    public float forcedCamMaxDistance = 10f;
    public float forcedCamMinPitch = -20f;
    public float forcedCamMaxPitch = 60f;

    [Tooltip("Vitesse d'interpolation utilisée pour suivre le joueur en mode forcé.")]
    [SerializeField] private float forcedCamFollowLerpSpeed = 10f;

    private float forcedCamYaw = 0f;
    private float forcedCamPitch = 20f;
    private float forcedCamDistance = 5f;
    private Vector3 forcedCamOffset = Vector3.zero; // Décalage monde utilisé en mode forcé

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

        cameraTargetName = FindChildRecursive(player, "Point_Chest")?.name;
        eventsManager = FindFirstObjectByType<EventsManager>();

        UpdateCameraPositionsFromHandler();
        FindManagedCameras();
    }

    /// <summary>
    /// Synchronise les points de caméra dans l'éditeur. Aucune redondance connue.
    /// </summary>
    void OnValidate()
    {
        UpdateCameraPositionsFromHandler();
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
        if (cameraPositions == null || cameraPositions.Count == 0)
            UpdateCameraPositionsFromHandler();

        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
        {
            // Stoppe toute transition en cours afin d'éviter des conflits avec les Timelines
            if (currentTransition != null)
            {
                StopCoroutine(currentTransition);
                currentTransition = null;
            }
            return;
        }

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

        // Les Timelines prennent le contrôle total de la caméra
        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
            return;

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

            // En mode forcé, l'application de l'offset doit être faite après la mise à jour
            if (currentWorldCameraState == WorldCameraState.Forced)
            {
                FollowForcedCameraPoint();
            }
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
    /// Interrompt le mouvement orbital et remet la recherche de point actif.
    /// </summary>
    public void StopOrbit()
    {
        orbitTarget = null;
        if (currentWorldCameraState == WorldCameraState.OrbitAround)
        {
            currentWorldCameraState = WorldCameraState.ResearchClosestCamPoint;
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
            case WorldCameraState.Forced:
                UpdateForcedCameraPoint();
                break;

            case WorldCameraState.ResearchClosestCamPoint:
                ApplyClosestCamera();
                break;

            default:
                Debug.LogWarning($"[CameraController] Unhandled WorldCameraState: {currentWorldCameraState}");
                break;
        }
    }

    /// <summary>
    /// Suit le joueur en mode forcé en interpolant position et rotation pour un mouvement fluide.
    /// </summary>
    void FollowForcedCameraPoint()
    {
        if (worldCamera == null || player == null) return;

        // On manipule le parent pour ne pas écraser l'offset de respiration appliqué à la caméra
        Transform camOrigin = worldCameraParent != null ? worldCameraParent : worldCamera.transform;
        Transform look = forceLookPoint != null ? forceLookPoint : player;

        // Position désirée calculée à partir du joueur et de l'offset courant
        Vector3 desiredPos = player.position + forcedCamOffset;
        // Rotation désirée pour que la caméra regarde la cible adéquate
        Quaternion desiredRot = Quaternion.LookRotation(look.position - desiredPos);

        // Interpolation pour un mouvement et une rotation plus smooth
        camOrigin.position = Vector3.Lerp(
            camOrigin.position,
            desiredPos,
            forcedCamFollowLerpSpeed * Time.deltaTime
        );
        camOrigin.rotation = Quaternion.Slerp(
            camOrigin.rotation,
            desiredRot,
            forcedCamFollowLerpSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Met à jour l'offset de la caméra forcée en fonction des entrées utilisateur (rotation autour du joueur).
    /// </summary>
    void UpdateForcedCameraPoint()
    {
        if (player == null) return;

        Vector2 input = InputsManager.Instance.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();

        // Rotation horizontale et verticale autour du joueur
        forcedCamYaw += input.x * forcedCamRotationSpeed * Time.deltaTime;
        forcedCamPitch -= input.y * forcedCamRotationSpeed * Time.deltaTime;
        forcedCamPitch = Mathf.Clamp(forcedCamPitch, forcedCamMinPitch, forcedCamMaxPitch);

        // Conversion sphérique → cartésienne pour obtenir le nouvel offset
        RecalculateForcedCamOffset();
    }

    /// <summary>
    /// Choisit et applique la caméra la plus proche du joueur.
    /// Peut entrer en conflit avec ForceCam.
    /// </summary>
    void ApplyClosestCamera()
    {
        if (cameraHandlerEnabled)
        {
            if (player == null || string.IsNullOrEmpty(cameraTargetName) || Camera.main == null) return;

            Transform closest = null;
            float minDist = float.MaxValue;
            foreach (Transform cp in cameraPositions)
            {
                if (cp == null) continue;
                float dist = Vector3.Distance(player.position, cp.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = cp;
                }
            }

            if (closest != null)
            {
                Transform look = FindChildRecursive(player, cameraTargetName);
                if (look == null) return;

                Vector3 desiredPos = closest.position;
                Quaternion desiredRot = Quaternion.LookRotation(look.position - desiredPos);

                // Lance une transition uniquement si le point de caméra a changé
                if (closest != lastClosestCameraPoint)
                {
                    lastClosestCameraPoint = closest;

                    if (currentTransition != null)
                        StopCoroutine(currentTransition);

                    // Déplace le parent pour éviter d'écraser l'offset de respiration de la caméra
                    currentTransition = StartCoroutine(SmoothMoveAndLook(Camera.main.transform.parent, desiredPos, desiredRot, 2f));
                }
                else if (currentTransition == null)
                {
                    // Ajuste simplement la rotation pour suivre le joueur sans recréer une transition
                    Camera.main.transform.parent.rotation = Quaternion.Slerp(
                        Camera.main.transform.parent.rotation,
                        desiredRot,
                        2f * Time.deltaTime
                    );
                }
            }
        }
        else
        {
            Debug.LogWarning("[CameraController] CameraHandler désactivé, pas de recherche de point.");
        }
    }

    #endregion

    #region Utilitaires
    /// <summary>
    /// Recalcule l'offset de la caméra forcée à partir de l'angle actuel et de la distance.
    /// Centralise la conversion sphérique → cartésienne pour faciliter les modifications.
    /// </summary>
    void RecalculateForcedCamOffset()
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
    /// Récupère dynamiquement les positions caméra à partir du handler dédié.
    /// </summary>
    void UpdateCameraPositionsFromHandler()
    {
        GameObject handler = GameObject.FindGameObjectWithTag("LevelCameraHandler");
        if (handler != null)
        {
            List<Transform> camPoints = new List<Transform>();
            foreach (Transform child in handler.transform)
                if (child != null)
                    camPoints.Add(child);
            cameraPositions = camPoints;
        }
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
