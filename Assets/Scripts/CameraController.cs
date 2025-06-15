using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private Coroutine currentTransition;

    [SerializeField] private float smoothSpeed = 5f;
    private Camera cam;

    [Header("Breathing effect")]
    public float amplitude = 0.05f;
    public float frequency = 1f;

    private Vector3 initialLocalPosition;
    private bool isBreathing = false;

    // === ORBIT MODE ===
    [Header("Orbit Settings")]
    public Transform orbitTarget;
    public float orbitDistance = 5f;
    public float orbitSpeed = 30f; // degrés par seconde
    public bool orbitX = false;
    public bool orbitY = true;
    public bool orbitZ = false;
    private bool isOrbiting = false;
    public bool IsOrbiting => isOrbiting;

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void Update()
    {
        if (isOrbiting && orbitTarget != null)
        {
            Vector3 axis = new Vector3(
                orbitX ? 1f : 0f,
                orbitY ? 1f : 0f,
                orbitZ ? 1f : 0f
            );

            if (axis == Vector3.zero) return;

            axis.Normalize();

            float angle = orbitSpeed * Time.deltaTime;
            transform.RotateAround(orbitTarget.position, axis, angle);

            // Recaler à la bonne distance pour éviter la dérive
            Vector3 offset = transform.position - orbitTarget.position;
            transform.position = orbitTarget.position + offset.normalized * orbitDistance;

            // Toujours regarder la cible
            transform.LookAt(orbitTarget);
        }
    }

    // === === ===

    /// <summary>
    /// Transition vers un point + LookAt une cible.
    /// </summary>
    public Coroutine SetCameraTarget(string gameObjectPositionName, string gameObjectToTargetName, float transitionSpeed = 2f)
    {
        StopOrbit(); // Si on change la cible, on désactive l'orbite

        Transform positionTransform = GameObject.Find(gameObjectPositionName)?.transform;
        if (positionTransform == null)
        {
            Debug.LogError($"Objet de position '{gameObjectPositionName}' introuvable !");
            return null;
        }

        Transform targetTransform = ResolveTransformFromString(gameObjectToTargetName);
        if (targetTransform == null)
        {
            Debug.LogError($"Objet à regarder '{gameObjectToTargetName}' introuvable !");
            return null;
        }

        Vector3 desiredPosition = positionTransform.position;
        Vector3 desiredLookAt = targetTransform.position;
        Quaternion baseLookRotation = Quaternion.LookRotation(desiredLookAt - desiredPosition);

        if (currentTransition != null)
            StopCoroutine(currentTransition);

        currentTransition = StartCoroutine(SmoothMoveAndLook(desiredPosition, baseLookRotation, transitionSpeed));
        return currentTransition;
    }

    /// <summary>
    /// Smooth transition
    /// </summary>
    public IEnumerator SmoothMoveAndLook(Vector3 targetPosition, Quaternion targetRotation, float speed)
    {
        float t = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * speed;
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        currentTransition = null;
    }

    /// <summary>
    /// Lance le mode Orbit autour d'une cible.
    /// </summary>
    public void OrbitAround(Transform target, float distance = 5f, float speed = 30f, bool x = false, bool y = true, bool z = false)
    {
        StopOrbit();

        orbitTarget = target;
        orbitDistance = distance;
        orbitSpeed = speed;
        orbitX = x;
        orbitY = y;
        orbitZ = z;

        // Position initiale correcte
        if (orbitTarget != null)
        {
            Vector3 dir = (transform.position - orbitTarget.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            transform.position = orbitTarget.position + dir * orbitDistance;
            transform.LookAt(orbitTarget);
        }

        isOrbiting = true;
    }

    /// <summary>
    /// Stoppe le mode Orbit.
    /// </summary>
    public void StopOrbit()
    {
        isOrbiting = false;
        orbitTarget = null;
    }

    private Transform ResolveTransformFromString(string reference)
    {
        if (!reference.StartsWith("$"))
        {
            GameObject go = GameObject.Find(reference);
            return go?.transform;
        }

        try
        {
            string[] parts = reference.Substring(1).Split('.');
            if (parts.Length != 3) return null;

            GameObject go = GameObject.Find(parts[0]);
            Component comp = go?.GetComponent(parts[1]);
            var field = comp?.GetType().GetField(parts[2]);

            object value = field?.GetValue(comp);
            return value switch
            {
                GameObject g => g.transform,
                Transform tf => tf,
                Component c => c.transform,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
