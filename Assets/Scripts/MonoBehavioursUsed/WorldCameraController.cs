using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// États disponibles pour la World Camera.
/// Chaque valeur représente un comportement distinct appliqué au rig.
/// </summary>
public enum WorldCameraState
{
    ThirdPerson,
    OrbitAround
}

/// <summary>
/// Contrôleur entièrement dédié à la WorldCamera.
/// Il se concentre sur la vue TPS, les transitions et la respiration du point de vue exploratoire.
/// </summary>
[DisallowMultipleComponent]
public class WorldCameraController : MonoBehaviour
{
    /// <summary>
    /// Accès global au contrôleur principal pour faciliter les interactions inter-systèmes.
    /// </summary>
    public static WorldCameraController Instance { get; private set; }

    [Header("Références essentielles")]
    [Tooltip("Caméra physique utilisée pour l'exploration du monde.")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("Rig de déplacement. Laisser vide pour utiliser le parent direct de la caméra.")]
    [SerializeField] private Transform cameraRig;

    [Tooltip("Transform du joueur suivi. Recherché automatiquement via le tag 'Player' si vide.")]
    [SerializeField] private Transform player;

    [Tooltip("Point forcé à regarder. Permet d'ignorer la recherche automatique du Point_Chest.")]
    [SerializeField] private Transform forceLookPoint;

    [Header("Paramètres de suivi TPS")]
    [SerializeField] private Vector3 thirdPersonFocusOffset = new(0f, 1.6f, 0f);
    [SerializeField] private float forcedCamRotationSpeed = 120f;
    [SerializeField] private float mouseRotationSensitivity = 0.15f;
    [SerializeField] private float mouseZoomSensitivity = 0.05f;
    [SerializeField] private float gamepadZoomSpeed = 2f;
    [SerializeField] private bool invertLookY = false;
    [SerializeField] private bool requireRightClickForMouseRotation = true;
    [SerializeField] private float forcedCamFollowLerpSpeed = 10f;
    [SerializeField] private float forcedCamMinDistance = 2f;
    [SerializeField] private float forcedCamMaxDistance = 10f;
    [SerializeField] private float forcedCamMinPitch = -35f;
    [SerializeField] private float forcedCamMaxPitch = 65f;

    [Header("Effet de respiration")]
    [Tooltip("Active le léger mouvement vertical simulant la respiration humaine.")]
    [SerializeField] private bool enableBreathing = true;
    [Tooltip("Amplitude maximale du mouvement de respiration (en mètres).")]
    [SerializeField] private float breathingAmplitude = 0.05f;
    [Tooltip("Fréquence de la respiration (oscillations par seconde).")]
    [SerializeField] private float breathingFrequency = 1f;

    [Header("Orbite automatique")]
    [SerializeField] private float orbitDistance = 5f;
    [SerializeField] private float orbitSpeed = 30f;
    [SerializeField] private bool orbitX;
    [SerializeField] private bool orbitY = true;
    [SerializeField] private bool orbitZ;

    /// <summary>
    /// État courant appliqué à la caméra du monde.
    /// </summary>
    [SerializeField] private WorldCameraState currentState = WorldCameraState.ThirdPerson;

    private Coroutine currentTransition;
    private Transform forcedLookTarget;
    private string cachedLookTargetName;
    private Transform orbitTarget;
    private Vector3 forcedCamOffset;
    private float forcedCamYaw = 180f;
    private float forcedCamPitch = 20f;
    private float forcedCamDistance = 5f;

    private float breathingOffset;
    private Vector3 baseLocalPosition;
    private bool baseCaptured;

    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private Vector3 savedRigPosition;
    private Quaternion savedRigRotation;

    private void Reset()
    {
        // Permet une configuration en un clic dans l'inspecteur Unity.
        worldCamera = GetComponent<Camera>();
        cameraRig = transform.parent;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WorldCameraController] Instance en double détectée, destruction du composant excédentaire.");
            Destroy(this);
            return;
        }

        Instance = this;

        worldCamera ??= GetComponent<Camera>();
        if (worldCamera == null)
        {
            Debug.LogError("[WorldCameraController] Aucune caméra n'est associée au contrôleur.", this);
        }

        cameraRig ??= transform.parent;

        player ??= GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("[WorldCameraController] Joueur introuvable au démarrage. Une recherche sera relancée dynamiquement.");
        }

        CaptureInitialTransforms();

        forcedLookTarget = player != null ? FindChildRecursive(player, "Point_Chest") : null;
        cachedLookTargetName = forcedLookTarget != null ? forcedLookTarget.name : null;

        RecalculateThirdPersonOffset();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        worldCamera ??= GetComponent<Camera>();
        if (worldCamera != null && cameraRig == null)
        {
            cameraRig = transform.parent;
        }
    }

    private void LateUpdate()
    {
        if (!enabled || !Application.isPlaying)
            return;

        if (TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying)
            return;

        player ??= GameObject.FindGameObjectWithTag("Player")?.transform;

        if (currentState == WorldCameraState.OrbitAround && orbitTarget != null)
        {
            UpdateOrbit();
        }
        else
        {
            UpdateThirdPersonControls();
            FollowThirdPersonCamera();
        }

        ApplyBreathing();
    }

    /// <summary>
    /// Enregistre les positions globales et locales pour restaurer la caméra après une cinématique.
    /// </summary>
    private void CaptureInitialTransforms()
    {
        if (cameraRig != null)
        {
            savedRigPosition = cameraRig.position;
            savedRigRotation = cameraRig.rotation;
        }

        if (worldCamera != null)
        {
            savedCameraPosition = transform.position;
            savedCameraRotation = transform.rotation;
        }

        if (!baseCaptured)
        {
            baseLocalPosition = transform.localPosition;
            baseCaptured = true;
        }
    }

    /// <summary>
    /// Suit la cible principale en vue TPS avec interpolation pour un rendu fluide.
    /// </summary>
    private void FollowThirdPersonCamera()
    {
        if (worldCamera == null || player == null)
            return;

        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 focusPoint = GetThirdPersonFocusPoint();
        Vector3 desiredPosition = focusPoint + forcedCamOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up);

        float smoothing = 1f - Mathf.Exp(-forcedCamFollowLerpSpeed * Time.deltaTime);
        rig.position = Vector3.Lerp(rig.position, desiredPosition, smoothing);
        rig.rotation = Quaternion.Slerp(rig.rotation, desiredRotation, smoothing);
    }

    /// <summary>
    /// Interprète les entrées joueurs pour piloter la caméra TPS (stick droit, souris, zoom).
    /// </summary>
    private void UpdateThirdPersonControls()
    {
        if (player == null)
            return;

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

    /// <summary>
    /// Calcule la position d'observation privilégiée (Point_Chest ou fallback).
    /// </summary>
    private Vector3 GetThirdPersonFocusPoint()
    {
        if (player == null)
            return cameraRig != null ? cameraRig.position : transform.position;

        Transform focusTarget = ResolveThirdPersonLookTarget();
        if (focusTarget != null && focusTarget != player)
            return focusTarget.position;

        return player.position + thirdPersonFocusOffset;
    }

    /// <summary>
    /// Recalcule l'offset sphérique -> cartésien pour la vue TPS.
    /// </summary>
    private void RecalculateThirdPersonOffset()
    {
        float yawRad = forcedCamYaw * Mathf.Deg2Rad;
        float pitchRad = forcedCamPitch * Mathf.Deg2Rad;

        forcedCamOffset = new Vector3(
            forcedCamDistance * Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            forcedCamDistance * Mathf.Sin(pitchRad),
            forcedCamDistance * Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        );
    }

    /// <summary>
    /// Gère le léger mouvement sinusoïdal de respiration.
    /// </summary>
    private void ApplyBreathing()
    {
        if (worldCamera == null)
            return;

        if (!baseCaptured)
        {
            baseLocalPosition = transform.localPosition;
            baseCaptured = true;
        }

        if (!enableBreathing)
        {
            if (!Mathf.Approximately(breathingOffset, 0f))
            {
                transform.localPosition = baseLocalPosition;
                breathingOffset = 0f;
            }
            return;
        }

        float newOffset = Mathf.Sin(Time.time * breathingFrequency) * breathingAmplitude;
        transform.localPosition = baseLocalPosition + Vector3.up * newOffset;
        breathingOffset = newOffset;
    }

    /// <summary>
    /// Active un mouvement d'orbite autour d'une cible donnée.
    /// </summary>
    public void StartOrbit(Transform target, float distance, float speed, bool x, bool y, bool z)
    {
        orbitTarget = target;
        orbitDistance = distance;
        orbitSpeed = speed;
        orbitX = x;
        orbitY = y;
        orbitZ = z;

        if (orbitTarget == null)
        {
            Debug.LogWarning("[WorldCameraController] Impossible de démarrer l'orbite : aucune cible fournie.");
            return;
        }

        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 direction = (rig.position - orbitTarget.position).normalized;
        if (direction == Vector3.zero)
        {
            direction = Vector3.forward;
        }

        rig.position = orbitTarget.position + direction * orbitDistance;
        rig.LookAt(orbitTarget);
        currentState = WorldCameraState.OrbitAround;
    }

    /// <summary>
    /// Stoppe le mouvement orbital et revient au comportement TPS.
    /// </summary>
    public void StopOrbit()
    {
        orbitTarget = null;
        currentState = WorldCameraState.ThirdPerson;
    }

    private void UpdateOrbit()
    {
        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 axis = new Vector3(orbitX ? 1f : 0f, orbitY ? 1f : 0f, orbitZ ? 1f : 0f);
        if (axis == Vector3.zero)
            return;

        axis.Normalize();
        float angle = orbitSpeed * Time.deltaTime;

        rig.RotateAround(orbitTarget.position, axis, angle);
        Vector3 offset = rig.position - orbitTarget.position;
        rig.position = orbitTarget.position + offset.normalized * orbitDistance;
        rig.LookAt(orbitTarget);
    }

    /// <summary>
    /// Sauvegarde la position actuelle du rig et de la caméra pour les restaurer plus tard.
    /// </summary>
    public void SaveWorldCameraTransform()
    {
        if (cameraRig != null)
        {
            savedRigPosition = cameraRig.position;
            savedRigRotation = cameraRig.rotation;
        }

        if (worldCamera != null)
        {
            savedCameraPosition = transform.position;
            savedCameraRotation = transform.rotation;
        }
    }

    /// <summary>
    /// Restaure les positions précédemment mémorisées.
    /// </summary>
    public void RestoreWorldCameraTransform()
    {
        if (cameraRig != null)
        {
            cameraRig.SetPositionAndRotation(savedRigPosition, savedRigRotation);
        }

        if (worldCamera != null)
        {
            transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
        }
    }

    /// <summary>
    /// Déplace le rig vers un point précis puis oriente la caméra vers un second point.
    /// </summary>
    public Coroutine SetCameraTarget(string positionName, string lookAtName, float transitionSpeed = 2f)
    {
        StopOrbit();

        Transform positionTarget = GameObject.Find(positionName)?.transform;
        Transform lookTarget = GameObject.Find(lookAtName)?.transform;

        if (positionTarget == null || lookTarget == null)
        {
            Debug.LogWarning($"[WorldCameraController] Points de caméra introuvables : {positionName} / {lookAtName}");
            return null;
        }

        Transform rig = cameraRig != null ? cameraRig : transform;
        Vector3 desiredPos = positionTarget.position;
        Quaternion desiredRot = Quaternion.LookRotation(lookTarget.position - desiredPos);

        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }

        currentTransition = StartCoroutine(SmoothMoveAndLook(rig, desiredPos, desiredRot, transitionSpeed));
        return currentTransition;
    }

    private IEnumerator SmoothMoveAndLook(Transform targetTransform, Vector3 targetPos, Quaternion targetRot, float speed)
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

        targetTransform.SetPositionAndRotation(targetPos, targetRot);
        currentTransition = null;
    }

    /// <summary>
    /// Recherche récursive d'un enfant par son nom.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

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

    /// <summary>
    /// Détermine le point de regard idéal à partir du joueur ou du point forcé.
    /// </summary>
    private Transform ResolveThirdPersonLookTarget()
    {
        if (forceLookPoint != null)
            return forceLookPoint;

        if (player == null)
            return null;

        if (forcedLookTarget != null)
        {
            if (!forcedLookTarget || (forcedLookTarget.root != null && player.root != null && forcedLookTarget.root != player.root))
            {
                forcedLookTarget = null;
            }
        }

        if (forcedLookTarget == null && !string.IsNullOrEmpty(cachedLookTargetName))
        {
            forcedLookTarget = FindChildRecursive(player, cachedLookTargetName);
        }

        if (forcedLookTarget == null)
        {
            forcedLookTarget = FindChildRecursive(player, "Point_Chest");
            if (forcedLookTarget != null)
            {
                cachedLookTargetName = forcedLookTarget.name;
            }
        }

        return forcedLookTarget != null ? forcedLookTarget : player;
    }
}
