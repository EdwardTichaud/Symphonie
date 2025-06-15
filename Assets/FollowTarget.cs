using UnityEngine;

[ExecuteAlways]
public class FollowTarget : MonoBehaviour
{
    [Tooltip("Transform � suivre")]
    public Transform target;

    [Tooltip("Offset relatif � appliquer")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Activer le suivi liss�")]
    public bool smoothFollow = true;

    [Tooltip("Vitesse du lissage")]
    public float smoothSpeed = 5f;

    void Update()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
        }
        else
        {
            transform.position = desiredPosition;
        }
    }
}
