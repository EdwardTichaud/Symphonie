using UnityEngine;

/// <summary>
/// Fait avancer ce GameObject sur X, Y, Z selon des bool.
/// D�clenchement contr�l� apr�s un d�lai automatique ou manuellement.
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

    [Header("Contr�le")]
    [Tooltip("D�marre le mouvement apr�s un d�lai automatique")]
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
    /// D�clenche le mouvement par script ou par d�lai automatique.
    /// </summary>
    public void StartMoving()
    {
        isMoving = true;
    }

    /// <summary>
    /// Arr�te le mouvement par script.
    /// </summary>
    public void StopMoving()
    {
        isMoving = false;
    }
}
