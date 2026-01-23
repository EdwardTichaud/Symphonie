using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôleur de caméra simple permettant à Munin (la caméra) de pivoter autour de Lucian.
/// Utilise la souris ou le stick droit pour orienter la vue.
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Cible à suivre")]
    [Tooltip("Transform du joueur (Lucian) autour duquel la caméra orbite.")]
    public Transform target;

    [Header("Bindings")]
    [SerializeField] private SceneBindings sceneBindings;

    [Header("Paramètres d'orbite")]
    [Tooltip("Distance par défaut entre la caméra et la cible.")]
    public float distance = 5f;
    [Tooltip("Distance minimale autorisée, utile dans les couloirs étroits.")]
    public float minDistance = 2f;
    [Tooltip("Distance maximale pour admirer le paysage.")]
    public float maxDistance = 8f;
    [Tooltip("Vitesse de modification de la distance (zoom manuel ou auto).")]
    public float zoomSpeed = 5f;
    [Tooltip("Sensibilité de la caméra sur les axes horizontal et vertical.")]
    public Vector2 sensitivity = new Vector2(120f, 120f);
    [Tooltip("Limites de l'angle vertical en degrés.")]
    public Vector2 pitchLimits = new Vector2(-20f, 60f);

    // La gestion des obstacles a été volontairement retirée pour simplifier le système.
    // L'occlusion visuelle est désormais la seule protection : si un mur s'interpose,
    // un autre script se charge de rendre l'obstacle transparent.

    [Header("Ajustements dynamiques")]
    [Tooltip("Temps de lissage du déplacement de la caméra.")]
    public float smoothTime = 0.05f;

    private Vector3 currentVelocity; // Vecteur utilisé par SmoothDamp
    private float yaw;
    private float pitch;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[ThirdPersonCameraController] Aucune cible assignée, recherche d'un objet tagué 'Player'.");
            if (sceneBindings == null)
                sceneBindings = ServiceRegistry.GetOrFind<SceneBindings>(FindObjectsInactive.Include);
            if (sceneBindings != null && sceneBindings.Player != null)
                target = sceneBindings.Player.transform;
        }

        // Initialisation des angles à partir de la rotation actuelle de la caméra.
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        // On s'assure que la distance initiale respecte bien les bornes min/max.
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // --- Gestion des entrées ---
        // Récupération des mouvements de souris et du stick droit pour orienter la caméra.
        Vector2 lookInput = Vector2.zero;
        if (Mouse.current != null)
        {
            lookInput += Mouse.current.delta.ReadValue();
        }
        if (Gamepad.current != null)
        {
            lookInput += Gamepad.current.rightStick.ReadValue();
        }

        // Zoom manuel via la molette de la souris (ou triggers de manette si nécessaire).
        float scroll = 0f;
        if (Mouse.current != null)
        {
            scroll = Mouse.current.scroll.ReadValue().y;
        }
        distance = Mathf.Clamp(distance - scroll * zoomSpeed * Time.deltaTime, minDistance, maxDistance);

        // Application de la sensibilité et du delta temps pour une rotation fluide.
        yaw += lookInput.x * sensitivity.x * Time.deltaTime;
        pitch -= lookInput.y * sensitivity.y * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        // Calcul de la nouvelle position de la caméra autour de la cible.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        // Direction normalisée opposée à la cible (vecteur vers l'arrière selon l'orientation).
        Vector3 backwardDir = rotation * Vector3.back;

        // Distance cible initiale (après zoom manuel).
        float targetDistance = distance;

        // Sans gestion d'obstacles : si aucun zoom manuel n'est utilisé, on s'éloigne
        // progressivement pour offrir une meilleure vue d'ensemble du décor.
        if (Mathf.Approximately(scroll, 0f))
        {
            targetDistance = Mathf.MoveTowards(targetDistance, maxDistance, zoomSpeed * Time.deltaTime);
        }

        // Position désirée calculée uniquement à partir de la distance cible.
        Vector3 desiredPosition = target.position + backwardDir * targetDistance;

        // Lissage du déplacement de la caméra pour un mouvement plus agréable, notamment dans les espaces exigus.
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
        transform.LookAt(target.position);
    }
}

