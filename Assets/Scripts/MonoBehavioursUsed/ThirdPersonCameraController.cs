using UnityEngine;

/// <summary>
/// Caméra de poursuite pour Munin qui reste automatiquement derrière Lucian.
/// Aucune entrée utilisateur n'est lue : seules la position et la rotation du joueur
/// déterminent l'orientation de la caméra. L'effet de respiration peut ensuite être
/// appliqué librement sur la caméra elle-même.
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Cible à suivre")]
    [Tooltip("Transform du joueur (Lucian) autour duquel la caméra se place.")]
    public Transform target;

    [Header("Paramètres de suivi")]
    [Tooltip("Distance entre la caméra et la cible.")]
    public float distance = 5f;
    [Tooltip("Angle vertical fixe de la caméra en degrés.")]
    public float pitch = 20f;

    // Écart initial de rotation pour que la caméra reste derrière le joueur.
    private float yawOffset;

    // Référence vers l'origine de la caméra (Cam_Origin). Si absente, on utilise la Transform courante.
    private Transform camOrigin;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[ThirdPersonCameraController] Aucune cible assignée, recherche d'un objet tagué 'Player'.");
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        // Détermination de l'origine de la caméra pour déplacer le parent plutôt que la caméra elle-même.
        camOrigin = transform.parent != null ? transform.parent : transform;

        // Mémorisation de l'écart initial de rotation entre la caméra et le joueur.
        if (target != null)
        {
            yawOffset = camOrigin.eulerAngles.y - target.eulerAngles.y;
        }

        // Conservation de l'angle vertical actuel pour garder la même hauteur de vue.
        pitch = camOrigin.eulerAngles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // La caméra suit simplement la rotation du joueur sans lire la souris ou le stick.
        float yaw = target.eulerAngles.y + yawOffset;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        // On applique le déplacement sur l'origine pour laisser la caméra libre de ses décalages locaux (Munin).
        camOrigin.position = target.position + offset;
        camOrigin.LookAt(target.position);
    }
}

