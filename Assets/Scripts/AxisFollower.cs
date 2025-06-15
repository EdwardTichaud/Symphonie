using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AxisFollower : MonoBehaviour
{
    [Header("Définition de l'axe")]
    public Transform axisOrigin;            // Point d'origine de l'axe
    public Transform axisEnd;               // Point de fin de l'axe
    [Tooltip("Liste d'étapes (points intermédiaires) entre l'origine et la fin de l'axe")]
    public List<Transform> intermediatePoints = new List<Transform>();

    [Header("Cible à suivre")]
    public Transform target;                // La cible (ex : joueur)

    [Header("Distance")]
    [Tooltip("Distance actuelle entre la target et le movingPoint")]
    public float currentDistance;
    [Tooltip("Distance max à laquelle on active le suivi")]
    public float maxViewDistance = 10f;

    [Header("Point mobile")]
    public Transform movingPoint;           // Le point qui doit suivre sur l'axe

    void Update()
    {
        if (axisOrigin == null || axisEnd == null || target == null || movingPoint == null)
            return;

        // Construire la liste de positions de l'axe
        List<Vector3> nodes = new List<Vector3> { axisOrigin.position };
        foreach (var pt in intermediatePoints)
            if (pt != null) nodes.Add(pt.position);
        nodes.Add(axisEnd.position);

        // Trouver la projection la plus proche sur chaque segment
        Vector3 bestProj = nodes[0];
        float bestDist = float.MaxValue;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Vector3 A = nodes[i];
            Vector3 B = nodes[i + 1];
            Vector3 AB = B - A;
            float segLen = AB.magnitude;
            Vector3 dir = AB / segLen;

            // Projection scalaire
            float t = Vector3.Dot(target.position - A, dir);
            float tClamped = Mathf.Clamp(t, 0f, segLen);
            Vector3 proj = A + dir * tClamped;

            float dist = Vector3.Distance(target.position, proj);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestProj = proj;
            }
        }

        // Mise à jour de la distance
        currentDistance = bestDist;

        // Si au-delà de la portée, clamp à la sphère de portée sur la ligne la plus proche
        Vector3 finalPos = bestProj;
        if (currentDistance > maxViewDistance)
        {
            Vector3 dirToCam = (bestProj - target.position).normalized;
            finalPos = target.position + dirToCam * maxViewDistance;
            // Reprojeter finalPos sur la polyline
            bestDist = float.MaxValue;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                Vector3 A = nodes[i];
                Vector3 B = nodes[i + 1];
                Vector3 AB = B - A;
                float segLen = AB.magnitude;
                Vector3 dir = AB / segLen;
                float t = Vector3.Dot(finalPos - A, dir);
                float tClamped = Mathf.Clamp(t, 0f, segLen);
                Vector3 proj = A + dir * tClamped;
                float dist = Vector3.Distance(finalPos, proj);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    finalPos = proj;
                }
            }
            currentDistance = Vector3.Distance(target.position, finalPos);
        }

        // Appliquer la position
        movingPoint.position = finalPos;
    }

    void OnDrawGizmos()
    {
        if (axisOrigin == null || axisEnd == null)
            return;

        // Construire les nodes pour le dessin
        List<Vector3> nodes = new List<Vector3> { axisOrigin.position };
        foreach (var pt in intermediatePoints)
            if (pt != null) nodes.Add(pt.position);
        nodes.Add(axisEnd.position);

        // Dessiner chaque segment
        Gizmos.color = Color.cyan;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Gizmos.DrawLine(nodes[i], nodes[i + 1]);
        }

        // Dessiner les points de nodes
        Gizmos.color = Color.gray;
        foreach (var pos in nodes)
            Gizmos.DrawSphere(pos, 0.05f);

        // Distance actuelle et portée
        if (target != null && movingPoint != null)
        {
            // Ligne distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position, movingPoint.position);
            Gizmos.DrawSphere(target.position, 0.05f);
            Gizmos.DrawSphere(movingPoint.position, 0.05f);

#if UNITY_EDITOR
            // Label distance
            Vector3 mid = (target.position + movingPoint.position) * 0.5f;
            Handles.Label(mid, currentDistance.ToString("F2") + " m");
#endif

            // Portée max
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, maxViewDistance);
        }
    }
}
