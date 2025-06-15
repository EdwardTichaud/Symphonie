#if UNITY_EDITOR
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Camera/Editor Camera Follower")]
/// <summary>
/// Fait suivre une cible à la caméra en mode Éditeur uniquement. Lorsque le mode Play est activé, le script ne fonctionne pas.
/// </summary>
public class EditorCameraFollower : MonoBehaviour
{
    [Header("Cible à suivre")]
    [Tooltip("Transform que la caméra devra suivre en mode Éditeur")] 
    public Transform target;
    
    [Header("Offset")]
    [Tooltip("Décalage local de la caméra par rapport à la cible")] 
    public Vector3 localOffset = new Vector3(0, 5, -10);

    void Update()
    {
        // Ne rien faire si la scène est en mode Play
        if (Application.isPlaying)
            return;

        if (target == null)
            return;
        
        // Calcul de la position désirée
        Vector3 worldOffset = transform.parent != null
            ? transform.parent.TransformDirection(localOffset)
            : localOffset;
        
        transform.position = target.position + worldOffset;
        transform.LookAt(target);
    }
}
#endif
