#if UNITY_EDITOR
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Camera/Editor Camera Follower")]
/// <summary>
/// Fait suivre une cible � la cam�ra en mode �diteur uniquement, en reprenant sa position et sa rotation.
/// Lorsque le mode Play est activ�, le script ne fonctionne pas.
/// </summary>
public class EditorCameraFollower : MonoBehaviour
{
    [Header("Cible � suivre")]
    [Tooltip("Transform que la cam�ra devra suivre en mode �diteur")] 
    public Transform target;
    
    [Header("Offset")]
    [Tooltip("D�calage local de la cam�ra par rapport � la cible")] 
    public Vector3 localPositionOffset = new Vector3(0, 5, -10);

    [Tooltip("D�calage de rotation en Euler (ajout� � la rotation de la cible)")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

    void Update()
    {
        // Ne rien faire si la sc�ne est en mode Play
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
