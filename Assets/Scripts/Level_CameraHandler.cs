using UnityEngine;
using System.Collections.Generic;

public class Level_CameraHandler : MonoBehaviour
{
    [Header("Points potentiels de la caméra")]
    public List<Transform> cameraPositions;

    [Header("Force Camera Settings")]
    [Tooltip("Point de caméra forcée (ex : Player_Points_Camera_Shoulder_Left_Near)")]
    public Transform forceCamPoint;
    [Tooltip("Point à regarder lors du forçage (ex : Player_Points_Camera_Shoulder_PlayerLook)")]
    public Transform forceLookPoint;

    [Header("CameraController Selection")]
    [Tooltip("Nom du GameObject contenant le CameraController à utiliser")]
    public string cameraControllerObjectName = "MainCamera";

    [HideInInspector]
    public bool isForcingCam = false;

    private string cameraTargetName;
    private CameraController cameraController;
    private EventsManager eventsManager;
    private Transform player;

    void Awake()
    {
        // Références initiales
        eventsManager = FindFirstObjectByType<EventsManager>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
            Debug.LogError("[CameraHandler] Aucun joueur trouvé avec le tag 'Player' !");

        GameObject targetObj = FindChildRecursive(player, "spine_03").gameObject;
        if (targetObj != null)
            cameraTargetName = targetObj.name;

        // Tentative de récupération spécifique du CameraController
        FindSpecificCameraController();
    }

    void Start()
    {
        // Log des erreurs après Awake
        if (string.IsNullOrEmpty(cameraTargetName))
            Debug.LogError("[CameraHandler] Impossible de trouver 'Player_Points_Chest' dans la scène !");
        if (cameraController == null)
            Debug.LogWarning($"[CameraHandler] Aucun CameraController nommé '{cameraControllerObjectName}' trouvé.");
        if (forceCamPoint == null || forceLookPoint == null)
            Debug.LogWarning("[CameraHandler] Points pour forçage de caméra non assignés !");
    }

    void Update()
    {
        // Si non trouvé, retenter
        if (cameraController == null)
            FindSpecificCameraController();
        if (eventsManager == null)
            eventsManager = FindFirstObjectByType<EventsManager>();

        HandleCameraBehaviour();
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }

    void FindSpecificCameraController()
    {
        // Cherche tous les CameraController et sélectionne celui qui correspond au nom
        var controllers = FindObjectsOfType<CameraController>();
        foreach (var ctrl in controllers)
        {
            if (ctrl.gameObject.name == cameraControllerObjectName)
            {
                cameraController = ctrl;
                return;
            }
        }
        // Fallback sur le premier trouvé
        if (controllers.Length > 0)
            cameraController = controllers[0];
    }

    void HandleCameraBehaviour()
    {
        // ❗ Vérifier si une Timeline est en cours
        if (TimelineStatus.IsTimelinePlaying)
            return;

        if (eventsManager != null && eventsManager.eventInProgress)
            return;

        // Toggle force mode
        if (InputsManager.Instance.playerInputs.Player.ForceCam.triggered)
        {
            isForcingCam = !isForcingCam;

            if (isForcingCam)
                ApplyForcedCamera();
            else
                ApplyClosestCamera();

            return;
        }

        // Si en mode forçage manuel : ne pas appliquer Orbit ni point le plus proche
        if (isForcingCam)
        {
            ApplyForcedCamera();
            return;
        }

        // Si Orbit actif → ne pas appliquer closest point
        if (cameraController != null && cameraController.IsOrbiting)
        {
            // Ne rien faire, laisser Orbit contrôler la caméra
            return;
        }

        // Suivi normal
        ApplyClosestCamera();
    }

    void ApplyForcedCamera()
    {
        if (forceCamPoint == null || forceLookPoint == null)
        {
            Debug.LogWarning("[CameraHandler] Points de forçage non définis.");
            return;
        }
        if (cameraController == null)
        {
            Debug.LogWarning("[CameraHandler] CameraController introuvable pour forçage.");
            return;
        }
        cameraController.SetCameraTarget(
            forceCamPoint.gameObject.name,
            forceLookPoint.gameObject.name,
            5f
        );
    }

    void ApplyClosestCamera()
    {
        if (cameraController == null || player == null || string.IsNullOrEmpty(cameraTargetName))
            return;

        Transform closestCamPoint = null;
        float minDistance = float.MaxValue;
        foreach (Transform camPoint in cameraPositions)
        {
            if (camPoint == null) continue;
            float distance = Vector3.Distance(player.position, camPoint.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestCamPoint = camPoint;
            }
        }

        if (closestCamPoint != null)
        {
            cameraController.SetCameraTarget(
                closestCamPoint.gameObject.name,
                cameraTargetName,
                2f
            );
        }
    }
}