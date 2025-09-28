using UnityEngine;

/// <summary>
/// Sous-zone permettant d'ajuster temporairement les paramètres d'une <see cref="TwoDZone"/>.
/// Idéal pour créer des variations de distance, de FOV ou de comportement à l'intérieur
/// d'une même zone principale (par exemple lors d'un couloir qui se resserre).
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Camera/2D Sub Zone")]
public class TwoDZoneSubZone : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Zone 2D parente. Si vide, sera automatiquement recherchée dans les parents.")]
    public TwoDZone parentZone;

    [Header("Overrides")]
    [Tooltip("Remplace la distance de caméra définie par la zone principale.")]
    public bool overrideDistance;
    public float distance = 6f;

    [Tooltip("Remplace le champ de vision.")]
    public bool overrideFieldOfView;
    [Range(1f, 179f)]
    public float fieldOfView = 55f;

    [Tooltip("Remplace l'offset appliqué au suivi de la cible.")]
    public bool overrideFollowOffset;
    public Vector3 followOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("Remplace l'offset appliqué à la position de la caméra.")]
    public bool overrideCameraOffset;
    public Vector3 cameraOffset = Vector3.zero;

    [Tooltip("Remplace l'offset de rotation.")]
    public bool overrideRotationOffset;
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Remplace les réglages de lissage de position.")]
    public bool overridePositionSmoothing;
    public bool smoothPosition = true;
    public float positionSmoothTime = 0.3f;

    [Tooltip("Remplace les réglages de lissage de rotation.")]
    public bool overrideRotationSmoothing;
    public bool smoothRotation = true;
    public float rotationSmoothSpeed = 6f;

    [Tooltip("Remplace la durée de lissage du FOV.")]
    public bool overrideFovSmoothTime;
    public float fovSmoothTime = 0.25f;

    [Tooltip("Remplace le plan de déplacement.")]
    public bool overridePlane;
    public TwoDZone.CameraPlane plane = TwoDZone.CameraPlane.XZ;
    public Vector3 customPlaneNormal = Vector3.up;

    [Tooltip("Forcer la caméra à rester fixe depuis cette sous-zone.")]
    public bool overrideFixedBehaviour;
    public bool keepCameraFixed;
    public Transform fixedCameraAnchor;

    [Tooltip("Override de la cible suivie.")]
    public bool overrideFollowTarget;
    public Transform followTargetOverride;

    private Collider cachedCollider;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider != null)
        {
            cachedCollider.isTrigger = true;
        }

        if (parentZone == null)
        {
            parentZone = GetComponentInParent<TwoDZone>();
        }
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentZone == null)
        {
            return;
        }

        if (other.CompareTag(parentZone.playerTag))
        {
            parentZone.RegisterSubZone(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentZone == null)
        {
            return;
        }

        if (other.CompareTag(parentZone.playerTag))
        {
            parentZone.UnregisterSubZone(this);
        }
    }

    private void OnDisable()
    {
        if (parentZone != null)
        {
            parentZone.UnregisterSubZone(this);
        }
    }

    /// <summary>
    /// Applique les overrides actifs aux réglages courants de la zone.
    /// </summary>
    /// <param name="settings">Réglages à modifier.</param>
    public void ApplyOverrides(ref TwoDZone.ResolvedSettings settings)
    {
        if (overrideDistance)
        {
            settings.distance = Mathf.Max(0f, distance);
        }

        if (overrideFieldOfView)
        {
            settings.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        }

        if (overrideFollowOffset)
        {
            settings.followOffset = followOffset;
        }

        if (overrideCameraOffset)
        {
            settings.cameraOffset = cameraOffset;
        }

        if (overrideRotationOffset)
        {
            settings.rotationOffset = rotationOffset;
        }

        if (overridePositionSmoothing)
        {
            settings.smoothPosition = smoothPosition;
            settings.positionSmoothTime = Mathf.Max(0.01f, positionSmoothTime);
        }

        if (overrideRotationSmoothing)
        {
            settings.smoothRotation = smoothRotation;
            settings.rotationSmoothSpeed = Mathf.Max(0.01f, rotationSmoothSpeed);
        }

        if (overrideFovSmoothTime)
        {
            settings.fovSmoothTime = Mathf.Max(0f, fovSmoothTime);
        }

        if (overridePlane)
        {
            settings.planeNormal = TwoDZone.ResolvePlaneNormal(plane, customPlaneNormal);
        }

        if (overrideFixedBehaviour)
        {
            settings.keepCameraFixed = keepCameraFixed;
            settings.fixedCameraAnchor = fixedCameraAnchor;
        }

        if (overrideFollowTarget)
        {
            settings.followTarget = followTargetOverride != null ? followTargetOverride : settings.followTarget;
        }
    }
}
