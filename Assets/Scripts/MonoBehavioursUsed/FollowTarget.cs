using UnityEngine;

[ExecuteAlways]
public class FollowTarget : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform à suivre")]
    public Transform target;

    [Header("Position")]
    [Tooltip("Activer le suivi de la position de la target")]
    public bool followPosition = true;

    [Tooltip("Offset relatif à appliquer")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Activer le suivi lissé de la position")]
    public bool smoothPosition = true;

    [Tooltip("Vitesse du lissage de la position")]
    public float smoothPositionSpeed = 5f;

    [Header("Rotation")]
    [Tooltip("Activer le suivi de la rotation de la target")]
    public bool followRotation = true;

    [Tooltip("Offset de rotation à ajouter (en Euler)")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    [Tooltip("Activer le suivi lissé de la rotation")]
    public bool smoothRotation = true;

    [Tooltip("Vitesse du lissage de la rotation")]
    public float smoothRotationSpeed = 5f;

    void Update()
    {
        if (target == null) return;

        // --- POSITION ---
        if (followPosition)
        {
            Vector3 desiredPosition = target.position + target.rotation * positionOffset;

            if (smoothPosition)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothPositionSpeed);
            }
            else
            {
                transform.position = desiredPosition;
            }
        }

        // --- ROTATION ---
        if (followRotation)
        {
            Quaternion desiredRotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);

            if (smoothRotation)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * smoothRotationSpeed);
            }
            else
            {
                transform.rotation = desiredRotation;
            }
        }
    }
}
