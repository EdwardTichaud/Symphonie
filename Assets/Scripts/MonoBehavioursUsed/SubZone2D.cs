using UnityEngine;

/// <summary>
/// Sous-zone permettant d'ajuster temporairement les paramètres d'une <see cref="Zone2D"/>.
/// Idéal pour créer des variations de distance, de FOV ou de comportement à l'intérieur
/// d'une même zone principale (par exemple lors d'un couloir qui se resserre).
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Camera/2D Sub Zone")]
public class SubZone2D : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Zone 2D parente. Si vide, sera automatiquement recherchée dans les parents.")]
    public Zone2D parentZone;

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

    [Tooltip("Remplace le plan de déplacement en utilisant le plan XY local de cette sous-zone (axe Forward comme normale).")]
    public bool overridePlaneWithTransformUp;

    [Tooltip("Transform optionnel servant de référence pour définir le plan XY. Laisser vide pour utiliser ce GameObject.")]
    public Transform planeReference;

    [Tooltip("Forcer la caméra à rester fixe depuis cette sous-zone.")]
    public bool overrideFixedBehaviour;
    public bool keepCameraFixed;
    public Transform fixedCameraAnchor;

    [Tooltip("Override de la cible suivie.")]
    public bool overrideFollowTarget;
    public Transform followTargetOverride;

    private Collider cachedCollider;

    // Suivi de l'état d'occupation afin de savoir si le joueur est toujours présent lorsque la sous-zone
    // est désactivée. Cela nous permet de prévenir la zone principale pour qu'elle restaure le parent
    // de la WorldCamera immédiatement et évite des transitions bancales.
    private bool playerInside;

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
            parentZone = GetComponentInParent<Zone2D>();
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
            // Dès que le joueur pénètre dans la sous-zone, on notifie la zone parente afin qu'elle
            // recalcule immédiatement ses paramètres (y compris la nouvelle cible WorldCam_Origin).
            parentZone.RegisterSubZone(this);
            playerInside = true; // Mémorise l'état pour une désactivation éventuelle de la sous-zone.
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
            // Lorsque le joueur quitte le volume, on retire la sous-zone pour restaurer les réglages
            // par défaut de la zone 2D et redonner la main aux autres sous-zones éventuelles.
            parentZone.UnregisterSubZone(this);
            playerInside = false;
        }
    }

    private void OnDisable()
    {
        if (playerInside && parentZone != null)
        {
            // Si la sous-zone s'éteint alors que le joueur est encore dedans (ex : activation/désactivation
            // dynamique de volumes), on force immédiatement la zone principale à recalculer ses paramètres.
            // Cela évite qu'un override temporaire laisse la WorldCamera attachée à un parent inattendu.
            parentZone.UnregisterSubZone(this);
        }

        // Réinitialise l'état interne pour éviter toute incohérence à la prochaine activation.
        playerInside = false;
    }

    /// <summary>
    /// Applique les overrides actifs aux réglages courants de la zone.
    /// </summary>
    /// <param name="settings">Réglages à modifier.</param>
    public void ApplyOverrides(ref Zone2D.ResolvedSettings settings)
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

        if (overridePlaneWithTransformUp)
        {
            Transform reference = planeReference != null ? planeReference : transform;
            // On s'appuie sur l'axe Forward du transform afin de cibler le plan XY local
            // défini par la sous-zone. Cela garantit un verrouillage cohérent avec l'orientation
            // choisie par les level designers dans la scène.
            settings.planeNormal = Zone2D.NormalizePlaneNormal(reference.forward);
            settings.planeUp = Zone2D.ResolvePlaneUp(settings.planeNormal, reference.up);
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
