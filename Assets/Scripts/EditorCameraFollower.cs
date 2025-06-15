#if UNITY_EDITOR
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Camera/Editor Camera Follower")]
/// <summary>
/// Fait suivre une cible � la cam�ra en mode �diteur uniquement. Lorsque le mode Play est activ�, le script ne fonctionne pas.
/// </summary>
public class EditorCameraFollower : MonoBehaviour
{
    [Header("Cible � suivre")]
    [Tooltip("Transform que la cam�ra devra suivre en mode �diteur")] 
    public Transform target;
    
    [Header("Offset")]
    [Tooltip("D�calage local de la cam�ra par rapport � la cible")] 
    public Vector3 localOffset = new Vector3(0, 5, -10);

    void Update()
    {
        // Ne rien faire si la sc�ne est en mode Play
        if (Application.isPlaying)
            return;

        if (target == null)
            return;
        
        // Calcul de la position d�sir�e
        Vector3 worldOffset = transform.parent != null
            ? transform.parent.TransformDirection(localOffset)
            : localOffset;
        
        transform.position = target.position + worldOffset;
        transform.LookAt(target);
    }
}
#endif
