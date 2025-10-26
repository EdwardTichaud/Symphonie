// AreaFiller.cs (version avec Hex Packing + Overlap)
// Place sous Assets/Editor/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[AddComponentMenu("Tools/Area Filler")]
[SelectionBase]
public class AreaFiller : MonoBehaviour
{
    public enum FillMode { XY_Plane, XYZ_Volume }
    public enum XYLayout { Grid, HexagonalTight } // NEW

    [Header("Source")]
    public GameObject prefab;

    [Header("Zone (locale au GameObject)")]
    public Vector3 areaSize = new Vector3(10, 10, 10);
    public Vector3 originOffset = Vector3.zero;
    public bool drawGizmos = true;

    [Header("Placement (commun)")]
    public FillMode mode = FillMode.XY_Plane;
    public bool usePrefabBoundsForCell = true;
    public Vector3 cellSize = new Vector3(1, 1, 1);
    public Vector3 cellMargin = new Vector3(0.1f, 0.1f, 0.1f);

    [Header("XY (anti-trous pour prefabs ronds)")]
    public XYLayout xyLayout = XYLayout.Grid;          // NEW
    [Tooltip("Autorise un léger chevauchement pour compacter (utile pour formes rondes).")]
    public bool allowOverlap = false;                   // NEW (pour XY et XYZ)
    [Tooltip("Facteur d'overlap (1 = pas de chevauchement, <1 = on compacte). Ex: 0.9")]
    [Range(0.5f, 1.0f)] public float overlapFactor = 1f; // NEW
    [Tooltip("Déduire automatiquement le 'rayon' du prefab à partir de son plus grand côté XY.")]
    public bool autoDiscRadius = true;                 // NEW
    [Tooltip("Rayon (si autoDiscRadius est OFF).")]
    public float discRadius = 0.5f;                    // NEW

    [Header("Ajustements")]
    public float heightOffset = 0f;
    public Vector3 positionJitter = Vector3.zero;
    public Vector3 randomRotationEuler = Vector3.zero;
    public Vector2 randomUniformScale = new Vector2(1f, 1f);

    [Header("Ciblage hiérarchie")]
    public Transform instancesParent;

    private const string GENERATED_TAG = "GeneratedByAreaFiller";

#if UNITY_EDITOR
    public void FillNow()
    {
        if (prefab == null)
        {
            Debug.LogWarning("[AreaFiller] Aucun prefab assigné.");
            return;
        }
        if (instancesParent == null) instancesParent = transform;

        // Calcule taille de cellule (grille) et/ou rayon (hex)
        Vector3 finalCell = cellSize;
        Vector3 prefabBounds;
        bool hasBounds = TryGetPrefabBounds(prefab, out prefabBounds);

        if (usePrefabBoundsForCell)
        {
            if (!hasBounds)
            {
                Debug.LogWarning("[AreaFiller] Impossible d'estimer la taille du prefab. Bascule sur cellSize.");
            }
            else
            {
                finalCell = new Vector3(
                    Mathf.Max(0.01f, prefabBounds.x + cellMargin.x),
                    Mathf.Max(0.01f, prefabBounds.y + cellMargin.y),
                    Mathf.Max(0.01f, prefabBounds.z + cellMargin.z)
                );
            }
        }

        if (finalCell.x <= 0 || finalCell.y <= 0 || finalCell.z <= 0)
        {
            Debug.LogWarning("[AreaFiller] cellSize invalide.");
            return;
        }

        Vector3 size = new Vector3(Mathf.Abs(areaSize.x), Mathf.Abs(areaSize.y), Mathf.Abs(areaSize.z));
        Vector3 half = size * 0.5f;
        Vector3 localOrigin = -half + originOffset;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        if (mode == FillMode.XY_Plane && xyLayout == XYLayout.HexagonalTight)
        {
            // ---------- HEX PACKING 2D (anti-trous pour prefabs ronds) ----------
            // Rayon: on prend le max XY pour couvrir le disque
            float r = autoDiscRadius && hasBounds
                ? 0.5f * Mathf.Max(prefabBounds.x, prefabBounds.y)
                : Mathf.Max(0.0001f, discRadius);

            // Espacements hex : horizontal = 2r, vertical = sqrt(3) * r
            // Overlap: on compacte en multipliant par overlapFactor (<=1)
            float spacingX = (2f * r) * Mathf.Clamp(overlapFactor, 0.5f, 1f);
            float spacingY = (Mathf.Sqrt(3f) * r) * Mathf.Clamp(overlapFactor, 0.5f, 1f);

            // Nombre de rangées
            int rows = Mathf.Max(1, Mathf.FloorToInt(size.y / spacingY));
            // On parcourt rangées; les colonnes dépendent du décalage de la rangée
            for (int iy = 0; iy < rows; iy++)
            {
                bool odd = (iy % 2) == 1;
                float rowOffsetX = odd ? spacingX * 0.5f : 0f;

                // Largeur disponible pour cette rangée
                float usableWidth = size.x - rowOffsetX;
                int cols = Mathf.Max(1, Mathf.FloorToInt(usableWidth / spacingX));

                for (int ix = 0; ix < cols; ix++)
                {
                    // Centre de cellule (hex) en coords locales (Z=0 car XY)
                    float x = localOrigin.x + rowOffsetX + (ix + 0.5f) * spacingX;
                    float y = localOrigin.y + (iy + 0.5f) * spacingY;
                    float z = 0f;

                    Vector3 jitter = new Vector3(
                        positionJitter.x == 0 ? 0 : Random.Range(-positionJitter.x, positionJitter.x),
                        positionJitter.y == 0 ? 0 : Random.Range(-positionJitter.y, positionJitter.y),
                        0f
                    );

                    Vector3 localPos = new Vector3(x, y + heightOffset, z) + jitter;
                    Vector3 worldPos = transform.TransformPoint(localPos);

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (instance == null) instance = Instantiate(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Area Fill Instance");

                    instance.transform.SetPositionAndRotation(worldPos, transform.rotation);
                    instance.transform.SetParent(instancesParent, true);

                    // Rotation aléatoire (ajoutée)
                    Vector3 addEuler = new Vector3(
                        randomRotationEuler.x == 0 ? 0 : Random.Range(-randomRotationEuler.x * 0.5f, randomRotationEuler.x * 0.5f),
                        randomRotationEuler.y == 0 ? 0 : Random.Range(-randomRotationEuler.y * 0.5f, randomRotationEuler.y * 0.5f),
                        randomRotationEuler.z == 0 ? 0 : Random.Range(-randomRotationEuler.z * 0.5f, randomRotationEuler.z * 0.5f)
                    );
                    instance.transform.rotation *= Quaternion.Euler(addEuler);

                    // Échelle aléatoire uniforme
                    float sMin = Mathf.Min(randomUniformScale.x, randomUniformScale.y);
                    float sMax = Mathf.Max(randomUniformScale.x, randomUniformScale.y);
                    float s = Mathf.Clamp(Random.Range(sMin, sMax), 0.0001f, 1000f);
                    if (Mathf.Abs(s - 1f) > 1e-4f)
                        instance.transform.localScale = instance.transform.localScale * s;

                    // Marqueur
                    var marker = instance.GetComponent<AreaFillerMarker>();
                    if (marker == null) marker = instance.AddComponent<AreaFillerMarker>();
                    marker.marker = GENERATED_TAG;
                    marker.source = this;
                }
            }
        }
        else
        {
            // ---------- GRILLE CLASSIQUE (XY ou XYZ) ----------
            // Option overlap: on réduit artificiellement la "cellule" pour compacter
            Vector3 step = finalCell;
            if (allowOverlap)
            {
                float f = Mathf.Clamp(overlapFactor, 0.5f, 1f);
                step = new Vector3(step.x * f, step.y * f, step.z * f);
            }

            int nx = Mathf.Max(1, Mathf.FloorToInt(size.x / (mode == FillMode.XY_Plane ? step.x : step.x)));
            int ny = Mathf.Max(1, Mathf.FloorToInt(size.y / step.y));
            int nz = (mode == FillMode.XY_Plane) ? 1 : Mathf.Max(1, Mathf.FloorToInt(size.z / step.z));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            for (int iz = 0; iz < nz; iz++)
            {
                // Centre de la cellule (grille)
                float x = localOrigin.x + (ix + 0.5f) * (size.x / nx);
                float y = localOrigin.y + (iy + 0.5f) * (size.y / ny);
                float z = (mode == FillMode.XY_Plane) ? 0f : localOrigin.z + (iz + 0.5f) * (size.z / nz);

                Vector3 jitter = new Vector3(
                    positionJitter.x == 0 ? 0 : Random.Range(-positionJitter.x, positionJitter.x),
                    positionJitter.y == 0 ? 0 : Random.Range(-positionJitter.y, positionJitter.y),
                    positionJitter.z == 0 ? 0 : Random.Range(-positionJitter.z, positionJitter.z)
                );

                Vector3 localPos = new Vector3(x, y + heightOffset, z) + jitter;
                Vector3 worldPos = transform.TransformPoint(localPos);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null) instance = Instantiate(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Area Fill Instance");

                instance.transform.SetPositionAndRotation(worldPos, transform.rotation);
                instance.transform.SetParent(instancesParent, true);

                // Rotation aléatoire (ajoutée)
                Vector3 addEuler = new Vector3(
                    randomRotationEuler.x == 0 ? 0 : Random.Range(-randomRotationEuler.x * 0.5f, randomRotationEuler.x * 0.5f),
                    randomRotationEuler.y == 0 ? 0 : Random.Range(-randomRotationEuler.y * 0.5f, randomRotationEuler.y * 0.5f),
                    randomRotationEuler.z == 0 ? 0 : Random.Range(-randomRotationEuler.z * 0.5f, randomRotationEuler.z * 0.5f)
                );
                instance.transform.rotation *= Quaternion.Euler(addEuler);

                // Échelle aléatoire uniforme
                float sMin = Mathf.Min(randomUniformScale.x, randomUniformScale.y);
                float sMax = Mathf.Max(randomUniformScale.x, randomUniformScale.y);
                float s = Mathf.Clamp(Random.Range(sMin, sMax), 0.0001f, 1000f);
                if (Mathf.Abs(s - 1f) > 1e-4f)
                    instance.transform.localScale = instance.transform.localScale * s;

                // Marqueur
                var marker = instance.GetComponent<AreaFillerMarker>();
                if (marker == null) marker = instance.AddComponent<AreaFillerMarker>();
                marker.marker = GENERATED_TAG;
                marker.source = this;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkSceneDirty();
    }

    public void ClearGenerated()
    {
        if (instancesParent == null) instancesParent = transform;

        var markers = instancesParent.GetComponentsInChildren<AreaFillerMarker>(true);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var m in markers)
        {
            if (m != null && m.source == this && m.marker == GENERATED_TAG)
            {
                if (m.gameObject != null)
                    Undo.DestroyObjectImmediate(m.gameObject);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkSceneDirty();
    }

    public bool TryComputeCellFromPrefab()
    {
        if (prefab == null) return false;
        if (TryGetPrefabBounds(prefab, out var bSize))
        {
            cellSize = bSize + cellMargin;
            return true;
        }
        return false;
    }

    // ---- Utils ----
    private static void MarkSceneDirty()
    {
        if (!Application.isPlaying)
            EditorSceneManager.MarkAllScenesDirty();
    }

    private static bool TryGetPrefabBounds(GameObject prefab, out Vector3 size)
    {
        size = Vector3.zero;
        if (prefab == null) return false;

        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (temp == null) temp = GameObject.Instantiate(prefab);

        try
        {
            Bounds? b = null;
            var rends = temp.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (b == null) b = r.bounds; else b = Encapsulate(b.Value, r.bounds);
            }
            if (!b.HasValue)
            {
                var cols = temp.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols)
                {
                    if (b == null) b = c.bounds; else b = Encapsulate(b.Value, c.bounds);
                }
            }
            if (b.HasValue) { size = b.Value.size; return true; }
            return false;
        }
        finally
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(temp);
#else
            GameObject.DestroyImmediate(temp);
#endif
        }

        static Bounds Encapsulate(Bounds a, Bounds bnds)
        {
            a.Encapsulate(bnds.min);
            a.Encapsulate(bnds.max);
            return a;
        }
    }
#endif // UNITY_EDITOR

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        var size = new Vector3(Mathf.Abs(areaSize.x), Mathf.Abs(areaSize.y), Mathf.Abs(areaSize.z));
        var center = originOffset;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.15f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, size);

        if (mode == FillMode.XY_Plane && xyLayout == XYLayout.HexagonalTight)
        {
            // Petite croix au centre Z=0 pour rappeler le plan
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
            Vector3 p = new Vector3(0, 0, 0) + originOffset;
            Gizmos.DrawLine(p + new Vector3(-size.x / 2, 0, 0), p + new Vector3(size.x / 2, 0, 0));
            Gizmos.DrawLine(p + new Vector3(0, -size.y / 2, 0), p + new Vector3(0, size.y / 2, 0));
        }
    }
}

public class AreaFillerMarker : MonoBehaviour
{
    public string marker;
    public AreaFiller source;
}

#if UNITY_EDITOR
[CustomEditor(typeof(AreaFiller))]
public class AreaFillerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var filler = (AreaFiller)target;

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = filler.prefab != null;
            if (GUILayout.Button("Remplir")) { filler.FillNow(); }
            GUI.enabled = true;

            if (GUILayout.Button("Vider")) { filler.ClearGenerated(); }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = filler.prefab != null;
            if (GUILayout.Button("Déduire cellSize depuis le Prefab"))
            {
                if (!filler.TryComputeCellFromPrefab())
                    EditorUtility.DisplayDialog("Area Filler", "Impossible de déduire la taille : aucun Renderer/Collider détecté.", "OK");
                else
                    EditorUtility.SetDirty(filler);
            }
            GUI.enabled = true;
        }

        EditorGUILayout.HelpBox(
            "Conseils :\n" +
            "- Pour des prefabs ronds en XY, mets 'xyLayout = HexagonalTight'.\n" +
            "- 'allowOverlap' + 'overlapFactor' (<1) compacte encore plus (léger chevauchement autorisé).\n" +
            "- En XYZ, on reste sur une grille (le facteur d'overlap resserre l'espacement, pas de close-packing HCP/FCC).\n" +
            "- Garde 'usePrefabBoundsForCell' activé si ton prefab a des Renderers/Colliders représentatifs.",
            MessageType.Info
        );
    }
}
#endif
