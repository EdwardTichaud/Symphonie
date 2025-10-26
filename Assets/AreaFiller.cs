// AreaFiller.cs
// Remplit une zone en XY (plan) ou XYZ (volume) avec des instances de prefab en ÉDITEUR.
// Place ce fichier sous un dossier "Editor" pour activer la partie CustomEditor.

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

    [Header("Source")]
    public GameObject prefab;

    [Header("Zone (locale au GameObject)")]
    [Tooltip("Taille de la zone à remplir (locale).")]
    public Vector3 areaSize = new Vector3(10, 10, 10);
    [Tooltip("Décalage de l'origine (locale) à partir du centre de la zone.")]
    public Vector3 originOffset = Vector3.zero;
    [Tooltip("Dessiner les gizmos de la zone dans la scène.")]
    public bool drawGizmos = true;

    [Header("Placement")]
    public FillMode mode = FillMode.XY_Plane;
    [Tooltip("Si activé, la taille de cellule est basée sur la taille du Renderer du prefab (avec marge).")]
    public bool usePrefabBoundsForCell = true;
    [Tooltip("Taille de cellule (utilisée si 'usePrefabBoundsForCell' est désactivé).")]
    public Vector3 cellSize = new Vector3(1, 1, 1);
    [Tooltip("Marge ajoutée autour de la taille du prefab pour l'espacement.")]
    public Vector3 cellMargin = new Vector3(0.1f, 0.1f, 0.1f);

    [Header("Ajustements")]
    [Tooltip("Décalage vertical appliqué aux instances par rapport à la cellule.")]
    public float heightOffset = 0f;
    [Tooltip("Jitter (aléa) de position dans la cellule (0 = aucun).")]
    public Vector3 positionJitter = Vector3.zero;
    [Tooltip("Rotation aléatoire par axe (en degrés).")]
    public Vector3 randomRotationEuler = Vector3.zero;
    [Tooltip("Uniform Scale aléatoire (min, max). Laisse 1–1 pour désactiver.")]
    public Vector2 randomUniformScale = new Vector2(1f, 1f);

    [Header("Ciblage hiérarchie")]
    [Tooltip("Parenter les instances sous ce transform (par défaut: ce GameObject).")]
    public Transform instancesParent;

    // Marqueur pour reconnaître ce qui a été généré par cet outil
    private const string GENERATED_TAG = "GeneratedByAreaFiller";

#if UNITY_EDITOR
    // ====== Boutons d’action exposés par l’Editor ======

    public void FillNow()
    {
        if (prefab == null)
        {
            Debug.LogWarning("[AreaFiller] Aucun prefab assigné.");
            return;
        }

        if (instancesParent == null) instancesParent = transform;

        // Calcule la taille de cellule
        Vector3 finalCell = cellSize;
        if (usePrefabBoundsForCell)
        {
            if (!TryGetPrefabBounds(prefab, out var bSize))
            {
                Debug.LogWarning("[AreaFiller] Impossible d'estimer la taille du prefab. " +
                                 "Basculer sur 'cellSize' manuel.");
            }
            else
            {
                finalCell = new Vector3(
                    Mathf.Max(0.01f, bSize.x + cellMargin.x),
                    Mathf.Max(0.01f, bSize.y + cellMargin.y),
                    Mathf.Max(0.01f, bSize.z + cellMargin.z)
                );
            }
        }

        if (finalCell.x <= 0 || finalCell.y <= 0 || finalCell.z <= 0)
        {
            Debug.LogWarning("[AreaFiller] cellSize invalide.");
            return;
        }

        // Comptage selon mode
        Vector3 size = areaSize;
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Abs(size.y);
        size.z = Mathf.Abs(size.z);

        int nx = Mathf.Max(1, Mathf.FloorToInt(size.x / finalCell.x));
        int ny = Mathf.Max(1, Mathf.FloorToInt(size.y / finalCell.y));
        int nz = (mode == FillMode.XY_Plane) ? 1 : Mathf.Max(1, Mathf.FloorToInt(size.z / finalCell.z));

        // Point d'origine local : centre de la zone + offset -> coin min
        Vector3 half = size * 0.5f;
        Vector3 localOrigin = -half + originOffset;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // Boucles
        for (int ix = 0; ix < nx; ix++)
        for (int iy = 0; iy < ny; iy++)
        for (int iz = 0; iz < nz; iz++)
        {
            // Centre de la cellule en local
            Vector3 localCellCenter = new Vector3(
                localOrigin.x + (ix + 0.5f) * (size.x / nx),
                localOrigin.y + (iy + 0.5f) * (size.y / ny),
                localOrigin.z + (iz + 0.5f) * (size.z / nz)
            );

            // En mode XY, on “écrase” Z au centre + heightOffset appliqué sur Y
            if (mode == FillMode.XY_Plane)
                localCellCenter.z = 0f;

            Vector3 jitter = new Vector3(
                (positionJitter.x == 0 ? 0 : Random.Range(-positionJitter.x, positionJitter.x)),
                (positionJitter.y == 0 ? 0 : Random.Range(-positionJitter.y, positionJitter.y)),
                (positionJitter.z == 0 ? 0 : Random.Range(-positionJitter.z, positionJitter.z))
            );

            Vector3 localPos = localCellCenter + jitter;
            if (mode == FillMode.XY_Plane)
                localPos.y += heightOffset; // “vers le haut” = Y en Unity par défaut
            else
                localPos.y += heightOffset;

            // Conversion en monde puis on parentera : plus robuste si le parent est scalé
            Vector3 worldPos = transform.TransformPoint(localPos);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
                instance = Instantiate(prefab);

            Undo.RegisterCreatedObjectUndo(instance, "Area Fill Instance");

            instance.transform.SetPositionAndRotation(worldPos, transform.rotation);
            instance.transform.SetParent(instancesParent, true);

            // Rotation aléatoire locale additive
            Vector3 addEuler = new Vector3(
                (randomRotationEuler.x == 0 ? 0 : Random.Range(-randomRotationEuler.x * 0.5f, randomRotationEuler.x * 0.5f)),
                (randomRotationEuler.y == 0 ? 0 : Random.Range(-randomRotationEuler.y * 0.5f, randomRotationEuler.y * 0.5f)),
                (randomRotationEuler.z == 0 ? 0 : Random.Range(-randomRotationEuler.z * 0.5f, randomRotationEuler.z * 0.5f))
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

        // Ouvre une instance temporaire sûre pour lire les Renderers
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (temp == null) temp = GameObject.Instantiate(prefab);

        try
        {
            Bounds? b = null;
            var rends = temp.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (b == null) b = r.bounds;
                else b = Encapsulate(b.Value, r.bounds);
            }
            if (b.HasValue)
            {
                size = b.Value.size;
                return true;
            }
            // Si pas de Renderer, on tente les Colliders
            var cols = temp.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (b == null) b = c.bounds;
                else b = Encapsulate(b.Value, c.bounds);
            }
            if (b.HasValue)
            {
                size = b.Value.size;
                return true;
            }
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

    // Dessin de la zone
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

        // Indication du mode
        if (mode == FillMode.XY_Plane)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
            // trace un plan au centre Z
            Vector3 p = new Vector3(0, 0, 0) + originOffset;
            Gizmos.DrawLine(p + new Vector3(-size.x / 2, 0, 0), p + new Vector3(size.x / 2, 0, 0));
            Gizmos.DrawLine(p + new Vector3(0, -size.y / 2, 0), p + new Vector3(0, size.y / 2, 0));
        }
    }
}

// Petit composant marqueur pour retrouver ce qui a été généré par cet AreaFiller
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
            if (GUILayout.Button("Remplir"))
            {
                filler.FillNow();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Vider"))
            {
                filler.ClearGenerated();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = filler.prefab != null;
            if (GUILayout.Button("Déduire cellSize depuis le Prefab"))
            {
                if (!filler.TryComputeCellFromPrefab())
                    EditorUtility.DisplayDialog("Area Filler", "Impossible de déduire la taille : aucun Renderer/Collider détecté dans le prefab.", "OK");
                else
                    EditorUtility.SetDirty(filler);
            }
            GUI.enabled = true;
        }

        EditorGUILayout.HelpBox(
            "Astuce :\n- 'areaSize' définit la zone (locale) à remplir.\n" +
            "- En mode XY, la Z locale est ignorée (plan au Z=0 local).\n" +
            "- Active 'usePrefabBoundsForCell' pour un espacement auto basé sur la taille du prefab.\n" +
            "- 'Vider' ne supprime que les instances générées par CET AreaFiller (grâce au marqueur).",
            MessageType.Info
        );
    }
}
#endif
