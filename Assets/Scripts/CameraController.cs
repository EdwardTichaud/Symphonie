using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public enum CameraState
{
    Forced,
    ResearchClosestCamPoint,
    OrbitAround
}

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private Coroutine currentTransition;

    [Header("Orbit Settings")]
    public Transform orbitTarget;
    public float orbitDistance = 5f;
    public float orbitSpeed = 30f;
    public bool orbitX, orbitY = true, orbitZ;

    [Header("Path Follow")]
    [Range(0f, 1f)] public float pathPosition;
    private bool isFollowingPath;
    public bool IsFollowingPath => isFollowingPath;
    private CameraPath currentCameraPath;
    [SerializeField] private float followLerpSpeed = 5f;
    [SerializeField] private float rotateLerpSpeed = 5f;
    private float pathElapsedTime = 0f;
    private float pathTotalDuration = 0f;

    [Header("Managed Cameras")]
    public CameraState currentCameraState = CameraState.ResearchClosestCamPoint; // ✅ Par défaut en recherche de point
    public List<Camera> managedCameras = new();

    private Camera activeCamera;

    [Header("Fixed Camera Points")]
    public bool cameraHandlerEnabled = true; // ✅ Par défaut activé
    public List<Transform> cameraPositions; // auto from LevelCameraHandler tag
    public Transform forceCamPoint, forceLookPoint;

    private string cameraTargetName;
    private Transform player;
    private EventsManager eventsManager;

    [Header("Forced Camera Point Control")]
    public bool isForcedCamMoving;

    public Transform forcedCameraPoint; // drag ton Point_ForcedCameraDirection ici
    public float forcedCamZoomSpeed = 5f;
    public float forcedCamRotationSpeed = 50f;
    public float forcedCamMinDistance = 2f;
    public float forcedCamMaxDistance = 10f;
    public float forcedCamMinPitch = -20f;
    public float forcedCamMaxPitch = 60f;

    private float forcedCamYaw = 0f;
    private float forcedCamPitch = 20f;
    private float forcedCamDistance = 5f;

    void Awake()
    {
        Instance = this;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogError("[CameraController] Player not found!");

        cameraTargetName = FindChildRecursive(player, "spine_03")?.name;
        eventsManager = FindFirstObjectByType<EventsManager>();

        UpdateCameraPositionsFromHandler();
        FindManagedCameras();
    }

    void OnValidate()
    {
        UpdateCameraPositionsFromHandler();
        FindManagedCameras();
    }

    void Update()
    {
        Vector2 fixedCameraMove = InputsManager.Instance.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();

        if (cameraPositions == null || cameraPositions.Count == 0)
            UpdateCameraPositionsFromHandler();

        if (TimelineStatus.IsTimelinePlaying)
            return;

        // ✅ Si la MainCamera est désactivée → skip toute logique
        if (Camera.main != null && !Camera.main.enabled)
            return;

        if (currentCameraState == CameraState.OrbitAround && orbitTarget != null && activeCamera != null)
        {
            UpdateOrbit();
            return;
        }

        if (isFollowingPath && currentCameraPath != null && activeCamera != null && currentCameraPath.points.Count >= 2)
        {
            UpdatePathFollow();
            return;
        }

        if (Application.isPlaying)
        {
            player ??= GameObject.FindGameObjectWithTag("Player")?.transform;
            eventsManager ??= FindFirstObjectByType<EventsManager>();
            HandleCameraBehaviour();
        }
    }

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

    void UpdatePathFollow()
    {
        pathElapsedTime += Time.deltaTime;
        float clampedTime = Mathf.Clamp(pathElapsedTime, 0f, pathTotalDuration);
        pathPosition = GetPathPositionFromTime(clampedTime);

        Vector3 pos;
        Quaternion rot;
        currentCameraPath.EvaluatePath(pathPosition, out pos, out rot);

        activeCamera.transform.position = Vector3.Lerp(activeCamera.transform.position, pos, followLerpSpeed * Time.deltaTime);
        activeCamera.transform.rotation = Quaternion.Slerp(activeCamera.transform.rotation, rot, rotateLerpSpeed * Time.deltaTime);

        if (clampedTime >= pathTotalDuration)
        {
            StopPathFollow();
        }
    }

    /// <summary>
    /// Lance le suivi du chemin pour la caméra correspondant au tag.
    /// </summary>
    public void StartPathFollow(CameraPath path, string cameraTag, float startPosition = 0f, bool alignImmediately = true)
    {
        StopOrbit();

        Camera cam = FindCameraByTag(cameraTag);
        if (cam == null)
        {
            Debug.LogError($"[CameraController] Aucun Camera trouvé avec le tag '{cameraTag}' !");
            return;
        }

        if (path.points == null || path.points.Count < 2 || path.durations == null || path.durations.Count != path.points.Count - 1)
        {
            Debug.LogError($"[CameraController] Path invalid or durations mismatch!");
            return;
        }

        currentCameraPath = path;
        pathElapsedTime = Mathf.Clamp01(startPosition) * path.GetTotalDuration();
        pathTotalDuration = path.GetTotalDuration();
        isFollowingPath = true;

        activeCamera = cam;

        if (alignImmediately)
        {
            pathPosition = GetPathPositionFromTime(pathElapsedTime);
            path.EvaluatePath(pathPosition, out Vector3 pos, out Quaternion rot);

            activeCamera.transform.position = pos;
            activeCamera.transform.rotation = rot;
        }
    }

    public void StopPathFollow()
    {
        if (currentCameraPath != null)
        {
            currentCameraPath.StopSequence();  // ✅ fait IsPlaying = false + autres resets si tu veux
            currentCameraPath = null;
        }

        isFollowingPath = false;
        activeCamera = null;
    }

    public void OrbitAround(string cameraTag, Transform target, float distance = 5f, float speed = 30f, bool x = false, bool y = true, bool z = false)
    {
        StopOrbit();
        StopPathFollow();

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

        currentCameraState = CameraState.OrbitAround; // ✅ Maintenant géré par l'état
        Debug.Log("[CameraController] OrbitAround démarré !");
    }

    public void StopOrbit()
    {
        orbitTarget = null;
        if (currentCameraState == CameraState.OrbitAround)
        {
            currentCameraState = CameraState.ResearchClosestCamPoint;
        }
        Debug.Log("[CameraController] OrbitAround stoppé.");
    }

    private Camera FindCameraByTag(string cameraTag)
    {
        foreach (Camera cam in managedCameras)
        {
            if (cam != null && cam.CompareTag(cameraTag))
                return cam;
        }
        return null;
    }

    private void FindManagedCameras()
    {
        managedCameras.Clear();
        Camera[] allCams = GetComponentsInChildren<Camera>(true);
        managedCameras.AddRange(allCams);
    }

    public Coroutine SetCameraTarget(string positionName, string lookAtName, float transitionSpeed = 2f)
    {
        StopOrbit();
        StopPathFollow();

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

    public void ForceCam()
    {
        StopOrbit();
        StopPathFollow();

        // Plus de SmoothMoveAndLook pour Forced : on va suivre direct
        if (currentTransition != null) StopCoroutine(currentTransition);

        currentCameraState = CameraState.Forced;
        cameraHandlerEnabled = false;

        Debug.Log("[CameraController] ForcedCam ACTIVATED");
    }

    public void ReleaseCam()
    {
        currentCameraState = CameraState.ResearchClosestCamPoint;
        cameraHandlerEnabled = true;
        Debug.Log("[CameraController] ForcedCam DISABLED");
    }

    void HandleCameraBehaviour()
    {
        switch (currentCameraState)
        {
            case CameraState.Forced:
                UpdateForcedCameraPoint();
                FollowForcedCameraPoint();  // 🔑 Suivi direct, fluide
                break;

            case CameraState.ResearchClosestCamPoint:
                ApplyClosestCamera();
                break;

            default:
                Debug.LogWarning($"[CameraController] Unhandled CameraState: {currentCameraState}");
                break;
        }
    }

    void FollowForcedCameraPoint()
    {
        if (forcedCameraPoint == null || forceLookPoint == null || Camera.main == null) return;

        Transform cam = Camera.main.transform;

        // Suivi direct (aucun blocage, aucun retard)
        cam.position = Vector3.Lerp(cam.position, forcedCameraPoint.position, 10f * Time.deltaTime);
        cam.rotation = Quaternion.Slerp(
            cam.rotation,
            Quaternion.LookRotation(forceLookPoint.position - cam.position),
            10f * Time.deltaTime
        );
    }

    void UpdateForcedCameraPoint()
    {
        if (forcedCameraPoint == null || player == null) return;

        Vector2 input = InputsManager.Instance.playerInputs.World.ForcedCamMove.ReadValue<Vector2>();
        isForcedCamMoving = input.magnitude > 0.1f;

        forcedCamDistance -= input.y * forcedCamZoomSpeed * Time.deltaTime;
        forcedCamDistance = Mathf.Clamp(forcedCamDistance, forcedCamMinDistance, forcedCamMaxDistance);

        forcedCamYaw += input.x * forcedCamRotationSpeed * Time.deltaTime;
        forcedCamPitch = Mathf.Clamp(forcedCamPitch, forcedCamMinPitch, forcedCamMaxPitch);

        float yawRad = forcedCamYaw * Mathf.Deg2Rad;
        float pitchRad = forcedCamPitch * Mathf.Deg2Rad;

        float x = forcedCamDistance * Mathf.Sin(yawRad) * Mathf.Cos(pitchRad);
        float y = forcedCamDistance * Mathf.Sin(pitchRad);
        float z = forcedCamDistance * Mathf.Cos(yawRad) * Mathf.Cos(pitchRad);

        forcedCameraPoint.position = player.position + new Vector3(x, y, z);
        forcedCameraPoint.LookAt(player.position);
    }

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
                Vector3 desiredPos = closest.position;
                Transform look = FindChildRecursive(player, cameraTargetName);
                if (look == null) return;
                Quaternion desiredRot = Quaternion.LookRotation(look.position - desiredPos);

                if (currentTransition != null) StopCoroutine(currentTransition);
                currentTransition = StartCoroutine(SmoothMoveAndLook(Camera.main.transform, desiredPos, desiredRot, 2f));
            }
        }
        else
        {
            Debug.LogWarning("[CameraController] CameraHandler désactivé, pas de recherche de point.");
        }
    }

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
}
