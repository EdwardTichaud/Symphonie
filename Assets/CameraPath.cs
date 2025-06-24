using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

[ExecuteAlways]
public class CameraPath : MonoBehaviour
{
    [Header("Camera Path Settings")]
    [Tooltip("Tag de la caméra qui doit suivre ce chemin")]
    public string cameraTag = "MainCamera";

    [Tooltip("Points avec tangentes manuelles et LookAt optionnel par point")]
    public List<CameraPathPoint> points = new List<CameraPathPoint>();

    [Tooltip("Durées par segment")]
    public List<float> durations = new List<float>();

    [Range(0f, 1f)] public float previewPosition = 0f;
    public bool autoPreview = false;

    private float previewTime = 0f;

    public bool IsPlaying = false;
    public bool triggered;

    private void OnValidate() => UpdatePoints();
    private void Awake() => UpdatePoints();

    public void UpdatePoints()
    {
        int childCount = transform.childCount;
        while (points.Count < childCount)
            points.Add(new CameraPathPoint());

        while (points.Count > childCount)
            points.RemoveAt(points.Count - 1);

        for (int i = 0; i < childCount; i++)
            points[i].point = transform.GetChild(i);

        int segmentCount = Mathf.Max(0, points.Count - 1);
        while (durations.Count < segmentCount)
            durations.Add(1f);
        while (durations.Count > segmentCount)
            durations.RemoveAt(durations.Count - 1);
    }

    private void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying && points.Count >= 2)
        {
            if (autoPreview)
            {
                previewTime += Time.deltaTime;
                float total = GetTotalDuration();
                float t = previewTime % total;
                previewPosition = GetPathPositionFromTime(t);
            }

            Camera previewCam = GameObject.FindGameObjectWithTag(cameraTag)?.GetComponent<Camera>();
            if (previewCam != null)
            {
                Vector3 pos;
                Quaternion rot;
                EvaluatePath(previewPosition, out pos, out rot);
                previewCam.transform.position = pos;
                previewCam.transform.rotation = rot;
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogWarning($"[CameraPath] Aucun Camera trouvé avec le tag '{cameraTag}' pour le preview !");
            }
        }
        #endif
    }

    private void OnDrawGizmos()
    {
        if (points.Count < 2) return;
        Gizmos.color = Color.magenta;

        for (int i = 0; i < points.Count - 1; i++)
        {
            CameraPathPoint p0 = points[i];
            CameraPathPoint p1 = points[i + 1];

            Vector3 a = p0.point.position;
            Vector3 b = a + p0.point.rotation * p0.outTangent;
            Vector3 d = p1.point.position;
            Vector3 c = d + p1.point.rotation * p1.inTangent;

            Handles.DrawBezier(a, d, b, c, Color.cyan, null, 2f);
            Handles.DrawLine(a, b);
            Handles.DrawLine(d, c);
            Handles.SphereHandleCap(0, b, Quaternion.identity, 0.1f, EventType.Repaint);
            Handles.SphereHandleCap(0, c, Quaternion.identity, 0.1f, EventType.Repaint);

#if UNITY_EDITOR
            Vector3 mid = (a + d) * 0.5f;
            Handles.Label(mid, $"⏱ {durations[i]:0.00}s");
#endif
        }
    }

    public Vector3 EvaluatePosition(float t)
    {
        int numSegments = points.Count - 1;
        float scaledT = t * numSegments;
        int i = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, numSegments - 1);
        float localT = scaledT - i;

        CameraPathPoint p0 = points[i];
        CameraPathPoint p1 = points[i + 1];

        Vector3 a = p0.point.position;
        Vector3 b = a + p0.point.rotation * p0.outTangent;
        Vector3 d = p1.point.position;
        Vector3 c = d + p1.point.rotation * p1.inTangent;

        return Bezier(a, b, c, d, localT);
    }

    public Quaternion EvaluateRotation(float t)
    {
        int numSegments = points.Count - 1;
        float scaledT = t * numSegments;
        int i = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, numSegments - 1);
        float localT = scaledT - i;

        CameraPathPoint p0 = points[i];
        CameraPathPoint p1 = points[i + 1];

        Vector3 pos = EvaluatePosition(t);

        // --- 1️⃣ Interpoler la rotation "pure" du chemin
        Quaternion r1 = p0.point.rotation;
        Quaternion r2 = p1.point.rotation;
        Quaternion pathRotation = Quaternion.Slerp(r1, r2, localT);

        // --- 2️⃣ Si LookAt actif sur au moins un point, interpoler une version LookAt
        Quaternion lookAtRotation;

        bool p0Look = p0.useLookAt && p0.targetToLook != null;
        bool p1Look = p1.useLookAt && p1.targetToLook != null;

        if (p0Look && p1Look)
        {
            // Double LookAt → interpole entre les deux
            Quaternion look1 = Quaternion.LookRotation(p0.targetToLook.position - pos);
            Quaternion look2 = Quaternion.LookRotation(p1.targetToLook.position - pos);
            lookAtRotation = Quaternion.Slerp(look1, look2, localT);
        }
        else if (p0Look)
        {
            lookAtRotation = Quaternion.LookRotation(p0.targetToLook.position - pos);
        }
        else if (p1Look)
        {
            lookAtRotation = Quaternion.LookRotation(p1.targetToLook.position - pos);
        }
        else
        {
            // Aucun LookAt → utiliser pathRotation pur
            return pathRotation;
        }

        // --- 3️⃣ Fusionner en douceur entre PathRotation et LookAt
        // Lissage contrôlé par localT pour un blend progressif entre les zones
        float blendFactor = Mathf.SmoothStep(0f, 1f, localT);
        return Quaternion.Slerp(pathRotation, lookAtRotation, blendFactor);
    }

    public void EvaluatePath(float t, out Vector3 pos, out Quaternion rot)
    {
        pos = EvaluatePosition(t);
        rot = EvaluateRotation(t);
    }

    public float GetPathPositionFromTime(float elapsedTime)
    {
        float total = GetTotalDuration();
        if (total <= 0f) return 0f;

        float accumulated = 0f;
        for (int i = 0; i < durations.Count; i++)
        {
            float seg = durations[i];
            if (elapsedTime <= accumulated + seg)
            {
                float segmentT = (elapsedTime - accumulated) / seg;
                return (i + segmentT) / (points.Count - 1);
            }
            accumulated += seg;
        }
        return 1f;
    }

    public float GetTotalDuration()
    {
        float total = 0f;
        foreach (var d in durations)
            total += Mathf.Max(0f, d);
        return total;
    }

    public void PlaySequence(float startPosition = 0f)
    {
        if (triggered)
            return; // ✅ Si déjà déclenché, on ne relance pas

        UpdatePoints();

        if (points.Count < 2)
        {
            Debug.LogError("[CameraPath] Not enough points to play!");
            return;
        }

        Camera cam = GameObject.FindGameObjectWithTag(cameraTag)?.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError($"[CameraPath] No Camera with tag '{cameraTag}' !");
            return;
        }

        CameraController controller = cam.GetComponentInParent<CameraController>();
        if (controller == null)
        {
            Debug.LogError($"[CameraPath] Camera '{cam.name}' n'a pas de CameraController parent !");
            return;
        }

        IsPlaying = true; // ✅ Marque comme en cours de suivi
        triggered = true; // ✅ Marque comme déclenché
        controller.StartPathFollow(this, cameraTag, startPosition);
    }

    public void StopSequence()
    {
        IsPlaying = false;
    }

    private Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * p0
             + 3 * uu * t * p1
             + 3 * u * tt * p2
             + ttt * p3;
    }
}

[System.Serializable]
public class CameraPathPoint
{
    public Transform point;

    [Tooltip("Tangente de sortie (vers le point suivant)")]
    public Vector3 outTangent = Vector3.forward;

    [Tooltip("Tangente d'entrée (depuis le point précédent)")]
    public Vector3 inTangent = -Vector3.forward;

    [Header("LookAt par point")]
    public bool useLookAt = false;
    public Transform targetToLook;
}
