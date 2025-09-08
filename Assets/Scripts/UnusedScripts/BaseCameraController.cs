using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôleur de caméra minimaliste servant de base saine pour Symphonie.
/// Munin, la caméra incarnant le frère survivant, suit Lucian avec fluidité et
/// autorise une rotation libre autour de lui.
/// </summary>
[AddComponentMenu("Camera/Base Camera Controller")]
public class BaseCameraController : MonoBehaviour
{
    [Header("Cible à suivre")]
    [Tooltip("Transform de Lucian ou de l'objet suivit par Munin.")]
    public Transform target;                                   // Référence de la cible à suivre

    [Header("Paramètres de position")]
    [Tooltip("Décalage par rapport à la cible dans son espace local.")]
    public Vector3 offset = new Vector3(0f, 3f, -6f);           // Position relative à la cible
    [Tooltip("Vitesse de lissage du suivi.")]
    public float followSpeed = 5f;                             // Vitesse de déplacement de la caméra

    [Header("Paramètres de rotation")]
    [Tooltip("Vitesse de rotation horizontale et verticale.")]
    public float rotationSpeed = 120f;                         // Sensibilité des mouvements de souris/manette
    [Tooltip("Limites de l'angle vertical (pitch).")]
    public Vector2 pitchLimits = new Vector2(-30f, 70f);       // Contraintes de pitch pour éviter les renversements

    private float yaw;                                         // Angle horizontal cumulé
    private float pitch;                                       // Angle vertical cumulé

    void Start()
    {
        // Si aucune cible n'est assignée, recherche automatique du joueur.
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
            else Debug.LogWarning("[BaseCameraController] Aucun objet tagué 'Player' trouvé dans la scène.");
        }

        // Initialisation des angles à partir de la rotation actuelle de la caméra.
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.Clamp(angles.x, pitchLimits.x, pitchLimits.y);
    }

    void LateUpdate()
    {
        if (target == null) return; // Sécurité : aucune cible à suivre

        // --- Récupération des entrées de rotation ---
        Vector2 lookInput = Vector2.zero;                      // Stocke l'entrée cumulée

        // Souris
        if (Mouse.current != null)
            lookInput += Mouse.current.delta.ReadValue();

        // Manette
        if (Gamepad.current != null)
            lookInput += Gamepad.current.rightStick.ReadValue();

        // Mise à jour des angles en fonction des entrées
        yaw += lookInput.x * rotationSpeed * Time.deltaTime;
        pitch -= lookInput.y * rotationSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y); // Clamp du pitch

        // Calcul de la rotation et de la position souhaitées
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);  // Rotation calculée
        Vector3 desiredPosition = target.position + rotation * offset; // Position cible

        // Lissage du mouvement pour éviter les mouvements brusques
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target.position);                      // Munin garde toujours un oeil sur Lucian
    }
}

