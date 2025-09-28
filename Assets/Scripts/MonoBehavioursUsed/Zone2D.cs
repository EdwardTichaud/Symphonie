using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zone de caméra dédiée aux séquences « en 2D » de Symphonie.
/// Lorsqu'un joueur entre dans le volume de déclenchement, la caméra
/// est repositionnée pour ne se déplacer que sur un plan 2D défini par la zone.
/// Cette classe offre un ensemble complet d'options : orientation définie par le prefab, distance,
/// champ de vision, comportement fixe ou en suivi, offsets et lissage.
/// 
/// L'objectif est de faciliter les transitions entre phases 3D et phases plus
/// cinématiques tout en respectant la narration du jeu.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Camera/2D Zone")]
public class Zone2D : MonoBehaviour
{
    /// <summary>
    /// Paramètres principaux de la zone. Cette classe est sérialisée pour
    /// faciliter la configuration dans l'inspecteur et permettre aux sous-zones
    /// de créer des overrides temporaires.
    /// </summary>
    [System.Serializable]
    public class ZoneSettings
    {
        [Header("Placement de la caméra")]
        [Tooltip("Distance de la caméra par rapport au point suivi, le long de la normale du plan.")]
        public float distance = 8f;

        [Tooltip("Champ de vision appliqué lors de l'entrée dans la zone.")]
        public float fieldOfView = 55f;

        [Tooltip("La caméra reste-t-elle fixe (ancrée sur un transform) ?")]
        public bool keepCameraFixed = false;

        [Tooltip("Point d'ancrage utilisé si la caméra reste fixe.")]
        public Transform fixedCameraAnchor;

        [Header("Suivi d'une cible")]
        [Tooltip("Par défaut, suivre automatiquement le joueur qui entre dans la zone.")]
        public bool followPlayerByDefault = true;

        [Tooltip("Cible explicite à suivre. Si renseignée, remplace le suivi automatique du joueur.")]
        public Transform explicitFollowTarget;

        [Tooltip("Décalage appliqué au point de suivi (par exemple pour viser la tête du personnage).")]
        public Vector3 followOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Offset supplémentaire appliqué à la position finale de la caméra.")]
        public Vector3 cameraOffset = Vector3.zero;

        [Tooltip("Décalage en Euler appliqué à la rotation finale.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Lissage des mouvements")]
        [Tooltip("Activer le lissage de la position.")]
        public bool smoothPosition = true;

        [Tooltip("Temps de lissage (SmoothDamp) pour la position.")]
        public float positionSmoothTime = 0.3f;

        [Tooltip("Activer un lissage de rotation (Slerp).")]
        public bool smoothRotation = true;

        [Tooltip("Vitesse du Slerp de rotation.")]
        public float rotationSmoothSpeed = 6f;

        [Tooltip("Temps de lissage appliqué au champ de vision.")]
        public float fovSmoothTime = 0.25f;
    }

    /// <summary>
    /// Structure runtime permettant de manipuler les paramètres actifs
    /// sans modifier l'instance sérialisée.
    /// </summary>
    public struct ResolvedSettings
    {
        public Vector3 planeNormal;
        public float distance;
        public float fieldOfView;
        public bool keepCameraFixed;
        public Transform fixedCameraAnchor;
        public Transform followTarget;
        public Vector3 followOffset;
        public Vector3 cameraOffset;
        public Vector3 rotationOffset;
        public bool smoothPosition;
        public float positionSmoothTime;
        public bool smoothRotation;
        public float rotationSmoothSpeed;
        public float fovSmoothTime;

        public ResolvedSettings(ZoneSettings source, Transform follow, Vector3 defaultPlaneNormal)
        {
            // La normale est dérivée de l'orientation du prefab ou d'une sous-zone éventuelle.
            planeNormal = NormalizePlaneNormal(defaultPlaneNormal);
            distance = Mathf.Max(0f, source.distance);
            fieldOfView = Mathf.Clamp(source.fieldOfView, 1f, 179f);
            keepCameraFixed = source.keepCameraFixed;
            fixedCameraAnchor = source.fixedCameraAnchor;
            followTarget = follow;
            followOffset = source.followOffset;
            cameraOffset = source.cameraOffset;
            rotationOffset = source.rotationOffset;
            smoothPosition = source.smoothPosition;
            positionSmoothTime = Mathf.Max(0.01f, source.positionSmoothTime);
            smoothRotation = source.smoothRotation;
            rotationSmoothSpeed = Mathf.Max(0.01f, source.rotationSmoothSpeed);
            fovSmoothTime = Mathf.Max(0f, source.fovSmoothTime);
        }
    }

    [Header("Références")]
    [Tooltip("Caméra à piloter. Laisser vide pour utiliser Camera.main lors de l'entrée dans la zone.")]
    public Camera targetCamera;

    [Tooltip("Tag utilisé pour identifier le joueur. Par défaut 'Player'.")]
    public string playerTag = "Player";

    [Header("Configuration principale")]
    public ZoneSettings baseSettings = new ZoneSettings();

    // Sauvegarde de l'état de la caméra avant d'entrer dans la zone
    private struct CameraBackup
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
    }

    private CameraBackup previousState;

    // Références runtime
    private Camera runtimeCamera;
    private Transform activeActor;
    private bool zoneActive;
    private Vector3 velocity;
    private float fovVelocity;
    private readonly List<SubZone2D> activeSubZones = new();

    private Vector3 PlaneOrigin => transform.position;

    private void Reset()
    {
        // Garantit que le collider agit comme un déclencheur.
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        // Sécurise l'état du collider même si un designer change le prefab.
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void LateUpdate()
    {
        if (!zoneActive || runtimeCamera == null)
        {
            return;
        }

        ApplyCameraBehaviour(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        ActivateZone(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (activeActor == other.transform)
        {
            DeactivateZone();
        }
    }

    /// <summary>
    /// Enregistre une sous-zone actuellement traversée par le joueur.
    /// </summary>
    /// <param name="subZone">Sous-zone qui vient d'être activée.</param>
    internal void RegisterSubZone(SubZone2D subZone)
    {
        if (subZone == null)
        {
            return;
        }

        if (!activeSubZones.Contains(subZone))
        {
            activeSubZones.Add(subZone);
            // Application immédiate des nouveaux paramètres pour éviter un frame de décalage.
            ApplyCameraBehaviour(false);
        }
    }

    /// <summary>
    /// Retire une sous-zone du suivi lorsque le joueur en sort.
    /// </summary>
    /// <param name="subZone">Sous-zone à retirer.</param>
    internal void UnregisterSubZone(SubZone2D subZone)
    {
        if (subZone == null)
        {
            return;
        }

        if (activeSubZones.Remove(subZone))
        {
            ApplyCameraBehaviour(false);
        }
    }

    /// <summary>
    /// Point d'entrée lorsqu'un joueur pénètre dans la zone.
    /// </summary>
    /// <param name="actor">Transform du joueur.</param>
    private void ActivateZone(Transform actor)
    {
        runtimeCamera = targetCamera != null ? targetCamera : Camera.main;
        if (runtimeCamera == null)
        {
            Debug.LogWarning("[TwoDZone] Aucune caméra n'a été trouvée pour activer la zone.", this);
            return;
        }

        activeActor = actor;

        previousState.position = runtimeCamera.transform.position;
        previousState.rotation = runtimeCamera.transform.rotation;
        previousState.fieldOfView = runtimeCamera.fieldOfView;

        velocity = Vector3.zero;
        fovVelocity = 0f;
        zoneActive = true;

        // Placement instantané pour éviter un flash en entrée.
        ApplyCameraBehaviour(true);
    }

    /// <summary>
    /// Restaure la caméra lorsque le joueur quitte la zone.
    /// </summary>
    private void DeactivateZone()
    {
        if (!zoneActive)
        {
            return;
        }

        if (runtimeCamera != null)
        {
            runtimeCamera.transform.position = previousState.position;
            runtimeCamera.transform.rotation = previousState.rotation;
            runtimeCamera.fieldOfView = previousState.fieldOfView;
        }

        zoneActive = false;
        activeActor = null;
        runtimeCamera = null;
        activeSubZones.Clear();
    }

    /// <summary>
    /// Applique le comportement défini par la zone et les éventuelles sous-zones.
    /// </summary>
    /// <param name="instant">Si vrai, ignore les lissages pour un positionnement immédiat.</param>
    private void ApplyCameraBehaviour(bool instant)
    {
        if (runtimeCamera == null)
        {
            return;
        }

        Transform followTarget = ResolveFollowTarget();
        ResolvedSettings settings = ResolveSettings(followTarget);

        // Mise à jour du champ de vision avec un éventuel lissage.
        float targetFov = settings.fieldOfView;
        if (instant || settings.fovSmoothTime <= 0f)
        {
            runtimeCamera.fieldOfView = targetFov;
        }
        else
        {
            runtimeCamera.fieldOfView = Mathf.SmoothDamp(runtimeCamera.fieldOfView, targetFov, ref fovVelocity, settings.fovSmoothTime);
        }

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        Vector3 planeNormal = settings.planeNormal.sqrMagnitude < 0.0001f
            ? Vector3.up
            : settings.planeNormal.normalized;
        Vector3 planeUp = ResolvePlaneUp(planeNormal);

        if (settings.keepCameraFixed)
        {
            Transform anchor = settings.fixedCameraAnchor != null ? settings.fixedCameraAnchor : transform;
            desiredPosition = anchor.position + settings.cameraOffset;
            desiredRotation = anchor.rotation * Quaternion.Euler(settings.rotationOffset);
        }
        else
        {
            Vector3 focusPoint = DetermineFocusPoint(settings);
            Vector3 projectedPoint = ProjectPointOnPlane(focusPoint, planeNormal, PlaneOrigin);
            desiredPosition = projectedPoint + settings.cameraOffset - planeNormal * settings.distance;

            Vector3 lookDirection = focusPoint - desiredPosition;
            if (lookDirection.sqrMagnitude < 0.001f)
            {
                lookDirection = -planeNormal;
            }

            desiredRotation = Quaternion.LookRotation(lookDirection.normalized, planeUp) * Quaternion.Euler(settings.rotationOffset);
        }

        if (instant || !settings.smoothPosition)
        {
            runtimeCamera.transform.position = desiredPosition;
        }
        else
        {
            runtimeCamera.transform.position = Vector3.SmoothDamp(runtimeCamera.transform.position, desiredPosition, ref velocity, settings.positionSmoothTime);
        }

        if (instant || !settings.smoothRotation)
        {
            runtimeCamera.transform.rotation = desiredRotation;
        }
        else
        {
            runtimeCamera.transform.rotation = Quaternion.Slerp(runtimeCamera.transform.rotation, desiredRotation, Time.deltaTime * settings.rotationSmoothSpeed);
        }
    }

    /// <summary>
    /// Détermine la cible de suivi en fonction des réglages de la zone.
    /// </summary>
    private Transform ResolveFollowTarget()
    {
        if (baseSettings.explicitFollowTarget != null)
        {
            return baseSettings.explicitFollowTarget;
        }

        if (baseSettings.followPlayerByDefault)
        {
            return activeActor;
        }

        return null;
    }

    /// <summary>
    /// Combine les réglages de base avec ceux des sous-zones actives.
    /// </summary>
    private ResolvedSettings ResolveSettings(Transform followTarget)
    {
        // On initialise les réglages avec la normale issue du placement manuel du prefab.
        ResolvedSettings settings = new ResolvedSettings(baseSettings, followTarget, GetDefaultPlaneNormal());

        // Les sous-zones s'appliquent dans l'ordre d'entrée, la dernière ayant la priorité.
        for (int i = 0; i < activeSubZones.Count; i++)
        {
            SubZone2D subZone = activeSubZones[i];
            if (subZone == null)
            {
                continue;
            }

            subZone.ApplyOverrides(ref settings);
        }

        return settings;
    }

    /// <summary>
    /// Détermine le point de focus de la caméra (centre de l'écran).
    /// </summary>
    private Vector3 DetermineFocusPoint(ResolvedSettings settings)
    {
        if (settings.followTarget != null)
        {
            return settings.followTarget.position + settings.followOffset;
        }

        // Si aucune cible n'est définie, on se base sur l'origine de la zone.
        return PlaneOrigin + settings.followOffset;
    }

    /// <summary>
    /// Détermine le plan par défaut de la zone en se basant sur l'orientation du prefab.
    /// Les designers peuvent ainsi placer manuellement la zone pour définir la normale souhaitée.
    /// </summary>
    private Vector3 GetDefaultPlaneNormal()
    {
        // L'axe "Up" du transform est utilisé afin d'offrir une lecture intuitive dans la scène.
        return transform.up;
    }

    /// <summary>
    /// Garantit que la normale du plan reste exploitable même si elle est quasi nulle.
    /// </summary>
    internal static Vector3 NormalizePlaneNormal(Vector3 normal)
    {
        return normal.sqrMagnitude < 0.0001f ? Vector3.up : normal.normalized;
    }

    /// <summary>
    /// Fournit un vecteur "up" cohérent pour LookRotation, même si la normale pointe vers le haut.
    /// </summary>
    private static Vector3 ResolvePlaneUp(Vector3 planeNormal)
    {
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(up.normalized, planeNormal.normalized)) > 0.99f)
        {
            // Si la normale est quasiment parallèle à l'up mondial, on choisit un autre axe.
            up = Vector3.right;
        }

        return up;
    }

    /// <summary>
    /// Projette un point sur le plan défini par une normale et une origine.
    /// </summary>
    private static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 normal, Vector3 origin)
    {
        float distanceToPlane = Vector3.Dot(point - origin, normal);
        return point - normal * distanceToPlane;
    }
}
