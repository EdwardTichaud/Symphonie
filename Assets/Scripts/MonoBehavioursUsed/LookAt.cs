using UnityEngine;

/// <summary>
/// Fait pivoter ce GameObject pour qu'il regarde toujours vers un Transform cible.
/// Optionnellement avec un lissage.
/// </summary>
public class LookAtTarget : MonoBehaviour
{
    [Tooltip("Cible � regarder")]
    public Transform target;

    [Tooltip("Activer un lissage de rotation")]
    public bool smooth = false;

    [Tooltip("Vitesse du lissage (si activ�)")]
    public float smoothSpeed = 5f;

    void Update()
    {
        if (target == null) return;

        // Calcule la rotation voulue pour regarder vers la cible
        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position);

        if (smooth)
        {
            // Rotation interpol�e
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * smoothSpeed);
        }
        else
        {
            // Rotation instantan�e
            transform.rotation = desiredRotation;
        }
    }
}
