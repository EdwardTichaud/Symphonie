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
        public Vector3 planeUp;
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

        public ResolvedSettings(ZoneSettings source, Transform follow, Vector3 defaultPlaneNormal, Vector3 defaultPlaneUp)
        {
            // La normale est dérivée de l'orientation du prefab ou d'une sous-zone éventuelle.
            planeNormal = NormalizePlaneNormal(defaultPlaneNormal);
            planeUp = ResolvePlaneUp(planeNormal, defaultPlaneUp);
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
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public float fieldOfView;
        public Vector3 rigPosition;
        public Quaternion rigRotation;
        public bool usedRig;

        // Hiérarchie d'origine à restaurer pour la WorldCamera et pour son éventuel rig (WorldCam_Origin).
        public Transform cameraParent;
        public Transform rigParent;
    }

    private CameraBackup previousState;

    // Références runtime
    private Camera runtimeCamera;
    private Transform runtimeCameraMotionRoot;
    private Transform activeActor;
    private bool zoneActive;
    private Vector3 velocity;
    private float fovVelocity;
    private readonly List<SubZone2D> activeSubZones = new();

    // Référence optionnelle vers le contrôleur de la WorldCamera. On le met en pause
    // quand la zone 2D prend la main afin d'éviter tout conflit de mise à jour.
    private WorldCameraController cachedWorldCameraController;
    private bool worldCameraControllerTemporarilyDisabled;

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

    private void OnDisable()
    {
        // Si la zone est désactivée (ex : changement de scène, activation/désactivation dynamique),
        // on force la sortie propre afin de restaurer immédiatement la hiérarchie et l'état de la caméra.
        // Sans cette mesure, la WorldCamera pouvait rester attachée à un parent temporaire défini
        // pendant la séquence 2D, ce qui provoquait des sauts ou des comportements inattendus ensuite.
        DeactivateZone();
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

        runtimeCameraMotionRoot = DetermineCameraMotionRoot(runtimeCamera);

        activeActor = actor;

        // On suspend immédiatement le contrôleur World pour que la caméra physique ne soit plus
        // déplacée par son comportement par défaut tant que nous sommes dans la zone 2D.
        cachedWorldCameraController = WorldCameraController.Instance;
        worldCameraControllerTemporarilyDisabled = false;
        if (cachedWorldCameraController != null && cachedWorldCameraController.enabled)
        {
            cachedWorldCameraController.enabled = false;
            worldCameraControllerTemporarilyDisabled = true;
        }

        Transform cameraTransform = runtimeCamera.transform;
        previousState.cameraPosition = cameraTransform.position;
        previousState.cameraRotation = cameraTransform.rotation;
        previousState.fieldOfView = runtimeCamera.fieldOfView;
        previousState.usedRig = runtimeCameraMotionRoot != null && runtimeCameraMotionRoot != cameraTransform;
        // Conservation du parent pour restaurer la hiérarchie une fois sorti de la zone.
        previousState.cameraParent = cameraTransform.parent;
        previousState.rigParent = runtimeCameraMotionRoot != null ? runtimeCameraMotionRoot.parent : null;
        if (runtimeCameraMotionRoot != null)
        {
            // On mémorise systématiquement la position du pivot (WorldCam_Origin) afin de pouvoir
            // restaurer précisément le setup de la WorldCamera une fois la zone quittée.
            previousState.rigPosition = runtimeCameraMotionRoot.position;
            previousState.rigRotation = runtimeCameraMotionRoot.rotation;
        }

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

        Transform cameraTransform = runtimeCamera != null ? runtimeCamera.transform : null;

        // On restaure en priorité la hiérarchie d'origine pour garantir que les prochaines mises à jour
        // (par le WorldCameraController ou d'autres systèmes) retrouvent exactement la même organisation.
        if (previousState.usedRig && runtimeCameraMotionRoot != null)
        {
            RestoreTransformParent(runtimeCameraMotionRoot, previousState.rigParent);
        }

        if (cameraTransform != null)
        {
            RestoreTransformParent(cameraTransform, previousState.cameraParent);
        }

        if (runtimeCamera != null)
        {
            runtimeCamera.transform.position = previousState.cameraPosition;
            runtimeCamera.transform.rotation = previousState.cameraRotation;
            runtimeCamera.fieldOfView = previousState.fieldOfView;
        }

        if (runtimeCameraMotionRoot != null && previousState.usedRig)
        {
            // Lorsque la WorldCamera est montée sur un parent "WorldCam_Origin", on restaure aussi
            // ce pivot afin de conserver les offsets et transitions définis ailleurs dans le projet.
            runtimeCameraMotionRoot.position = previousState.rigPosition;
            runtimeCameraMotionRoot.rotation = previousState.rigRotation;
        }

        // Restauration du contrôleur World uniquement si nous l'avons désactivé nous-même.
        if (cachedWorldCameraController != null)
        {
            if (worldCameraControllerTemporarilyDisabled)
            {
                cachedWorldCameraController.enabled = true;
            }

            cachedWorldCameraController = null;
            worldCameraControllerTemporarilyDisabled = false;
        }

        zoneActive = false;
        activeActor = null;
        runtimeCamera = null;
        runtimeCameraMotionRoot = null;
        activeSubZones.Clear();
    }

    /// <summary>
    /// Replace un transform sous son parent d'origine en conservant sa transformation monde.
    /// </summary>
    /// <param name="target">Transform à replacer.</param>
    /// <param name="originalParent">Parent qui était actif avant l'entrée dans la zone.</param>
    private static void RestoreTransformParent(Transform target, Transform originalParent)
    {
        if (target == null)
        {
            return;
        }

        Transform currentParent = target.parent;
        if (currentParent == originalParent)
        {
            // Rien à faire si la hiérarchie n'a pas changé.
            return;
        }

        if (originalParent != null)
        {
            // On conserve la transformation monde pour éviter tout déplacement indésirable.
            target.SetParent(originalParent, true);
        }
        else
        {
            // Cas d'une caméra initialement à la racine : on détache simplement le transform.
            target.SetParent(null, true);
        }
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

        Transform motionRoot = runtimeCameraMotionRoot != null ? runtimeCameraMotionRoot : runtimeCamera.transform;
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
        Vector3 planeUp = ResolvePlaneUp(planeNormal, settings.planeUp);

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
            motionRoot.position = desiredPosition;
        }
        else
        {
            motionRoot.position = Vector3.SmoothDamp(motionRoot.position, desiredPosition, ref velocity, settings.positionSmoothTime);
        }

        if (instant || !settings.smoothRotation)
        {
            motionRoot.rotation = desiredRotation;
        }
        else
        {
            motionRoot.rotation = Quaternion.Slerp(motionRoot.rotation, desiredRotation, Time.deltaTime * settings.rotationSmoothSpeed);
        }
    }

    /// <summary>
    /// Identifie le transform à piloter pour la caméra. Dans le cas de la WorldCamera,
    /// c'est son parent "WorldCam_Origin" qui doit être déplacé afin de préserver les rigs existants.
    /// </summary>
    private Transform DetermineCameraMotionRoot(Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        Transform cameraTransform = camera.transform;
        Transform parent = cameraTransform.parent;

        if (parent != null && parent.name == "WorldCam_Origin")
        {
            // On retourne explicitement le parent pour éviter de rompre le pipeline mis en place
            // par le CameraSystem (transitions, animation curves, etc.).
            return parent;
        }

        // Pour toutes les autres caméras (ex : caméras temporaires de cinématiques),
        // on continue à déplacer directement le transform de la caméra.
        return cameraTransform;
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
        ResolvedSettings settings = new ResolvedSettings(baseSettings, followTarget, GetDefaultPlaneNormal(), GetDefaultPlaneUp());

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
        // On verrouille la caméra sur le plan XY local de la zone : la normale correspond donc
        // à l'axe Z (Forward) du GameObject qui porte le script. Ainsi, l'artiste peut orienter
        // librement la zone dans la scène et obtenir automatiquement un plan cohérent.
        return transform.forward;
    }

    /// <summary>
    /// Détermine le vecteur "up" local associé au plan XY utilisé pour contraindre la caméra.
    /// </summary>
    private Vector3 GetDefaultPlaneUp()
    {
        // Le plan XY local est défini par les axes Right et Up du GameObject. On renvoie donc
        // l'axe Up pour fournir un indice de rotation cohérent à la caméra 2D.
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
    internal static Vector3 ResolvePlaneUp(Vector3 planeNormal, Vector3 upHint)
    {
        // L'indice fourni par la zone (souvent son axe Up local) nous permet d'aligner la caméra
        // avec l'espace désiré. On retombe sur l'axe global par défaut en cas de valeur dégénérée.
        Vector3 up = upHint.sqrMagnitude < 0.0001f ? Vector3.up : upHint.normalized;

        // Si l'indice est quasi parallèle à la normale, on calcule un nouvel axe en utilisant un
        // produit vectoriel pour éviter les problèmes de LookRotation (gimbal lock).
        Vector3 normalizedNormal = planeNormal.sqrMagnitude < 0.0001f ? Vector3.forward : planeNormal.normalized;
        if (Mathf.Abs(Vector3.Dot(up, normalizedNormal)) > 0.99f)
        {
            up = Vector3.Cross(normalizedNormal, Vector3.right);
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.Cross(normalizedNormal, Vector3.up);
            }

            up = up.sqrMagnitude < 0.0001f ? Vector3.up : up.normalized;
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
