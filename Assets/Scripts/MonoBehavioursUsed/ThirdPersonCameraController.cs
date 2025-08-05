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

    [Header("Paramètres d'orbite")]
    [Tooltip("Distance entre la caméra et la cible.")]
    public float distance = 5f;
    [Tooltip("Sensibilité de la caméra sur les axes horizontal et vertical.")]
    public Vector2 sensitivity = new Vector2(120f, 120f);
    [Tooltip("Limites de l'angle vertical en degrés.")]
    public Vector2 pitchLimits = new Vector2(-20f, 60f);

    private float yaw;
    private float pitch;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[ThirdPersonCameraController] Aucune cible assignée, recherche d'un objet tagué 'Player'.");
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        // Initialisation des angles à partir de la rotation actuelle de la caméra.
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Récupération des entrées souris / manette via le nouveau système d'input.
        Vector2 lookInput = Vector2.zero;
        if (Mouse.current != null)
        {
            lookInput += Mouse.current.delta.ReadValue();
        }
        if (Gamepad.current != null)
        {
            lookInput += Gamepad.current.rightStick.ReadValue();
        }

        // Application de la sensibilité et du delta temps pour une rotation fluide.
        yaw += lookInput.x * sensitivity.x * Time.deltaTime;
        pitch -= lookInput.y * sensitivity.y * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        // Calcul de la nouvelle position de la caméra autour de la cible.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}

