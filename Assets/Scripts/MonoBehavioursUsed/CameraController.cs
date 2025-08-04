using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public enum WorldCameraState
{
    Forced,
    ResearchClosestCamPoint,
    OrbitAround
}

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    /// <summary>
    /// Indique si un CameraPath est actuellement suivi.
    /// Permet aux autres scripts de connaître la priorité caméra.
    /// </summary>
    public static bool IsAnyPathPlaying => Instance != null && Instance.isFollowingPath;

    private Coroutine currentTransition;

    [Header("---------- Common ----------")]

    [Header("Managed Cameras")]
    public List<Camera> managedCameras = new();
    public WorldCameraState currentWorldCameraState = WorldCameraState.ResearchClosestCamPoint; // ✅ Par défaut en recherche de point

    [Header("Path Follow")]
    [Range(0f, 1f)] public float pathPosition;
    public bool isFollowingPath;
    public bool IsFollowingPath => isFollowingPath;
    public CameraPath currentCameraPath;
    [SerializeField] private float followLerpSpeed = 5f;
    [SerializeField] private float rotateLerpSpeed = 5f;
    private float pathElapsedTime = 0f;
    private float pathTotalDuration = 0f;
    private bool forceLookAt;
    private Transform forcedLookTarget;
    private Camera activeCamera;
    private Camera worldCamera; // Référence directe à la WorldCamera

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
    public bool isForcedCamMoving;
    public bool worldCamForced;

    public Transform forcedCameraPoint; // Ancien système, conservé pour compatibilité mais non utilisé
    public float forcedCamZoomSpeed = 5f;
    public float forcedCamRotationSpeed = 50f;
    public float forcedCamMinDistance = 2f;
    public float forcedCamMaxDistance = 10f;
    public float forcedCamMinPitch = -20f;
    public float forcedCamMaxPitch = 60f;

    private float forcedCamYaw = 0f;
    private float forcedCamPitch = 20f;
    private float forcedCamDistance = 5f;
    private Vector3 forcedCamOffset = Vector3.zero; // Décalage monde utilisé en mode forcé

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
    /// Gère l'état de la caméra à chaque frame. Peut entrer en conflit avec OrbitAround ou PathFollow actifs.
    /// </summary>
    void Update()
    {
        Vector2 fixedCameraMove = InputsManager.Instance.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();

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

        if (currentWorldCameraState == WorldCameraState.OrbitAround && orbitTarget != null && activeCamera != null)
        {
            UpdateOrbit();
            return;
        }

        if (Application.isPlaying)
        {
            player ??= GameObject.FindGameObjectWithTag("Player")?.transform;
            eventsManager ??= FindFirstObjectByType<EventsManager>();
            HandleCameraBehaviour();
        }
    }

    /// <summary>
    /// Effectue les ajustements de caméra en fin de frame pour éviter les conflits.
    /// </summary>
    void LateUpdate()
    {
        if (Application.isPlaying)
        {
            // Les Timelines prennent le contrôle total de la caméra
            if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
                return;

            if (currentWorldCameraState == WorldCameraState.Forced)
            {
                FollowForcedCameraPoint();
            }
        }
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
        if (isFollowingPath)
        {
            Debug.Log("[CameraController] CameraPath en cours - OrbitAround ignoré.");
            return;
        }

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
        if (isFollowingPath)
        {
            Debug.Log("[CameraController] CameraPath en cours - SetCameraTarget ignoré.");
            return null;
        }

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
        currentTransition = StartCoroutine(SmoothMoveAndLook(Camera.main.transform, desiredPos, desiredRot, transitionSpeed));
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

    #region Caméra Forcée
    /// <summary>
    /// Désactive la logique automatique pour forcer un point de vue fixe.
    /// Conflit avec OrbitAround et PathFollow.
    /// </summary>
    public void ForceCam()
    {
        StopOrbit();

        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
        activeCamera = worldCamera;

        // Récupération du point d'épaule pour positionner la caméra au plus près du joueur
        Transform shoulder = FindChildRecursive(player, "Camera_Shoulder_Left_Near");
        forceLookPoint = FindChildRecursive(player, cameraTargetName);

        if (shoulder != null)
        {
            // Calcul initial du décalage sans tenir compte de la rotation du joueur
            forcedCamOffset = shoulder.position - player.position;
            forcedCamDistance = forcedCamOffset.magnitude;
            forcedCamYaw = Mathf.Atan2(forcedCamOffset.x, forcedCamOffset.z) * Mathf.Rad2Deg;
            forcedCamPitch = Mathf.Asin(forcedCamOffset.y / forcedCamDistance) * Mathf.Rad2Deg;
        }
        else
        {
            // Repli : on conserve la position actuelle de la caméra
            forcedCamOffset = worldCamera.transform.position - player.position;
            forcedCamDistance = forcedCamOffset.magnitude;
            forcedCamYaw = Mathf.Atan2(forcedCamOffset.x, forcedCamOffset.z) * Mathf.Rad2Deg;
            forcedCamPitch = Mathf.Asin(forcedCamOffset.y / forcedCamDistance) * Mathf.Rad2Deg;
        }

        // Application immédiate pour éviter un décalage visuel lors de l'activation
        FollowForcedCameraPoint();

        currentWorldCameraState = WorldCameraState.Forced;
        cameraHandlerEnabled = false;
        worldCamForced = true;

        Debug.Log("[CameraController] ForcedCam activated");
    }

    /// <summary>
    /// Réactive la gestion automatique de la caméra après un mode forcé.
    /// </summary>
    public void ReleaseCam()
    {
        currentWorldCameraState = WorldCameraState.ResearchClosestCamPoint;
        cameraHandlerEnabled = true;
        worldCamForced = false;

        // Réinitialise l'offset pour ne pas garder de résidus
        forcedCamOffset = Vector3.zero;

        // Oublie le dernier point pour relancer une transition propre
        lastClosestCameraPoint = null;

        Debug.Log("[CameraController] ForcedCam disabled");
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
    /// Applique la position forcée de la caméra en suivant le joueur sans reprendre sa rotation.
    /// </summary>
    void FollowForcedCameraPoint()
    {
        if (worldCamera == null || player == null) return;

        Transform camOrigin = worldCamera.transform;
        Transform look = forceLookPoint != null ? forceLookPoint : player;

        // Position collée aux déplacements du joueur mais indépendante de sa rotation
        camOrigin.position = player.position + forcedCamOffset;
        camOrigin.rotation = Quaternion.LookRotation(look.position - camOrigin.position);
    }

    /// <summary>
    /// Met à jour l'offset de la caméra forcée en fonction des entrées utilisateur (rotation autour du joueur).
    /// </summary>
    void UpdateForcedCameraPoint()
    {
        if (player == null) return;

        Vector2 input = InputsManager.Instance.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();
        isForcedCamMoving = input.magnitude > 0.1f;

        // Rotation horizontale et verticale autour du joueur
        forcedCamYaw += input.x * forcedCamRotationSpeed * Time.deltaTime;
        forcedCamPitch -= input.y * forcedCamRotationSpeed * Time.deltaTime;
        forcedCamPitch = Mathf.Clamp(forcedCamPitch, forcedCamMinPitch, forcedCamMaxPitch);

        float yawRad = forcedCamYaw * Mathf.Deg2Rad;
        float pitchRad = forcedCamPitch * Mathf.Deg2Rad;

        // Conversion sphérique → cartésienne pour obtenir le décalage monde
        forcedCamOffset = new Vector3(
            forcedCamDistance * Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            forcedCamDistance * Mathf.Sin(pitchRad),
            forcedCamDistance * Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        );
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

                    currentTransition = StartCoroutine(SmoothMoveAndLook(Camera.main.transform, desiredPos, desiredRot, 2f));
                }
                else if (currentTransition == null)
                {
                    // Ajuste simplement la rotation pour suivre le joueur sans recréer une transition
                    Camera.main.transform.rotation = Quaternion.Slerp(
                        Camera.main.transform.rotation,
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

    /// <summary>
    /// Convertit un temps écoulé en position normalisée sur le chemin actuel.
    /// </summary>
    private float GetPathPositionFromTime(float elapsedTime)
    {
        if (currentCameraPath == null || currentCameraPath.durations == null || currentCameraPath.durations.Count == 0)
            return 0f;

        float total = pathTotalDuration;
        if (total <= 0f) return 0f;

        float accumulated = 0f;
        for (int i = 0; i < currentCameraPath.durations.Count; i++)
        {
            float segDuration = currentCameraPath.durations[i];
            if (elapsedTime <= accumulated + segDuration)
            {
                float segmentT = (elapsedTime - accumulated) / segDuration;
                float t = (i + segmentT) / (currentCameraPath.points.Count - 1);
                return Mathf.Clamp01(t);
            }
            accumulated += segDuration;
        }

        return 1f;
    }

    #endregion
}
