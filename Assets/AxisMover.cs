using UnityEngine;

/// <summary>
/// Fait avancer ce GameObject sur X, Y, Z selon des bool.
/// Déclenchement contrôlé après un délai automatique ou manuellement.
/// </summary>
public class AxisMover : MonoBehaviour
{
    [Header("Mouvement")]
    public bool moveX = false;
    public bool moveY = false;
    public bool moveZ = true;

    public float speedX = 1f;
    public float speedY = 1f;
    public float speedZ = 1f;

    [Header("Contrôle")]
    [Tooltip("Démarre le mouvement après un délai automatique")]
    public bool startAfterDelay = false;

    [Tooltip("Temps avant de commencer le mouvement (en secondes)")]
    public float delayTime = 2f;

    [Tooltip("Indique si le mouvement est actif")]
    public bool isMoving = false;

    private float timer = 0f;

    void Start()
    {
        if (startAfterDelay)
        {
            isMoving = false;
            timer = 0f;
        }
    }

    void Update()
    {
        if (startAfterDelay && !isMoving)
        {
            timer += Time.deltaTime;
            if (timer >= delayTime)
            {
                StartMoving();
            }
        }

        if (!isMoving) return;

        Vector3 movement = Vector3.zero;

        if (moveX)
            movement.x += speedX * Time.deltaTime;

        if (moveY)
            movement.y += speedY * Time.deltaTime;

        if (moveZ)
            movement.z += speedZ * Time.deltaTime;

        transform.Translate(movement, Space.Self);
    }

    /// <summary>
    /// Déclenche le mouvement par script ou par délai automatique.
    /// </summary>
    public void StartMoving()
    {
        isMoving = true;
    }

    /// <summary>
    /// Arrête le mouvement par script.
    /// </summary>
    public void StopMoving()
    {
        isMoving = false;
    }
}
