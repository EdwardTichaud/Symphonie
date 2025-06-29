#if UNITY_EDITOR
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Camera/Editor Camera Follower")]
/// <summary>
/// Fait suivre une cible à la caméra en mode Éditeur uniquement, en reprenant sa position et sa rotation.
/// Lorsque le mode Play est activé, le script ne fonctionne pas.
/// </summary>
public class EditorCameraFollower : MonoBehaviour
{
    [Header("Cible à suivre")]
    [Tooltip("Transform que la caméra devra suivre en mode Éditeur")] 
    public Transform target;
    
    [Header("Offset")]
    [Tooltip("Décalage local de la caméra par rapport à la cible")] 
    public Vector3 localPositionOffset = new Vector3(0, 5, -10);

    [Tooltip("Décalage de rotation en Euler (ajouté à la rotation de la cible)")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    void Update()
    {
        // Ne rien faire si la scène est en mode Play
        if (Application.isPlaying)
            return;

        if (target == null)
            return;

        // Calcule la position : offset dans l'espace local de la cible
        Vector3 desiredPosition = target.TransformPoint(localPositionOffset);
        transform.position = desiredPosition;

        // Calcule la rotation : rotation de la cible + offset
        Quaternion desiredRotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
        transform.rotation = desiredRotation;
    }
}
#endif
