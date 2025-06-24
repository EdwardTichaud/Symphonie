using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ce composant dessine une sphère Gizmo dans la scène pour marquer une position.
/// </summary>
[ExecuteAlways]
public class GizmosSphere : MonoBehaviour
{
    [Header("Sphere Settings")]
    public Color color = Color.yellow;
    public float radius = 1f;

    [Tooltip("Dessine toujours le Gizmo (OnDrawGizmos) ou seulement quand sélectionné (OnDrawGizmosSelected).")]
    public bool drawAlways = true;

    private void OnDrawGizmos()
    {
        if (drawAlways)
            DrawSphereGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAlways)
            DrawSphereGizmo();
    }

    private void DrawSphereGizmo()
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
