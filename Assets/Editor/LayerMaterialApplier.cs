#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to apply materials by layer across the whole active scene (children included),
/// while preserving original renderer materials for undo/revert later.
/// Works in the Editor in Edit Mode and during Play Mode.
/// </summary>
public class LayerMaterialApplier : EditorWindow
{
    [Serializable]
    public struct LayerToMaterial
    {
        [Tooltip("Layer name or index to target")] public int layer;
        [Tooltip("Material to apply to every submesh of matching renderers")] public Material material;
    }

    [Serializable]
    public class Settings : ScriptableObject
    {
        public bool includeInactive = true;
        public bool keepMaterialArrayLength = true;
        public List<LayerToMaterial> mappings = new List<LayerToMaterial>();
    }

    private Settings _settings;

    private const string kSettingsAssetName = "LayerMaterialApplierSettings";

    [MenuItem("Tools/Rendering/Layer Material Applier")] 
    public static void ShowWindow()
    {
        var wnd = GetWindow<LayerMaterialApplier>(true, "Layer Material Applier");
        wnd.minSize = new Vector2(420, 260);
        wnd.Initialize();
    }

    private void Initialize()
    {
        if (_settings == null)
        {
            _settings = LoadOrCreateSettingsAsset();
        }
    }

    private void OnEnable()
    {
        Initialize();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Keep window alive and functional across mode changes
        Repaint();
    }

    private void OnGUI()
    {
        if (_settings == null) Initialize();
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);
            _settings.includeInactive = EditorGUILayout.Toggle(new GUIContent("Include Inactive"), _settings.includeInactive);
            _settings.keepMaterialArrayLength = EditorGUILayout.Toggle(new GUIContent("Keep Materials Array Length"), _settings.keepMaterialArrayLength);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer → Material Mappings", EditorStyles.boldLabel);
            DrawMappingsList();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Apply to Active Scene", "Apply materials based on layer to every Renderer in the active scene"), GUILayout.Height(30)))
                {
                    ApplyToScene();
                }
                if (GUILayout.Button(new GUIContent("Revert All", "Revert every Renderer changed by this tool back to its original materials"), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Revert All?", "This will restore all renderers recorded by this tool in the active scene.", "Revert", "Cancel"))
                    {
                        RevertAll();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Applies the chosen material to all submeshes of renderers whose GameObject layer matches. Original materials are preserved in a per-scene hidden state object so you can revert later, even across Play/Edit mode.", MessageType.Info);
        }
    }

    private void DrawMappingsList()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            int removeIndex = -1;
            for (int i = 0; i < _settings.mappings.Count; i++)
            {
                var m = _settings.mappings[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    m.layer = LayerFieldWidth(new GUIContent("Layer", "Target layer"), m.layer, 120f);
                    m.material = (Material)EditorGUILayout.ObjectField(new GUIContent("Material"), m.material, typeof(Material), false);

                    GUI.enabled = _settings.mappings.Count > 1;
                    if (GUILayout.Button("-", GUILayout.Width(24))) removeIndex = i;
                    GUI.enabled = true;
                }
                _settings.mappings[i] = m;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Mapping"))
                {
                    _settings.mappings.Add(new LayerToMaterial{ layer = 0, material = null });
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Sort by Layer", GUILayout.Width(110)))
                {
                    _settings.mappings = _settings.mappings.OrderBy(mm => mm.layer).ToList();
                }
            }
            if (removeIndex >= 0)
            {
                _settings.mappings.RemoveAt(removeIndex);
            }
        }
    }

    private static int LayerFieldWidth(GUIContent label, int layer, float labelWidth)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var prev = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            int newLayer = EditorGUILayout.LayerField(label, layer);
            EditorGUIUtility.labelWidth = prev;
            return newLayer;
        }
    }

    private Settings LoadOrCreateSettingsAsset()
    {
        string[] assets = AssetDatabase.FindAssets($"t:{nameof(Settings)}");
        Settings s = null;
        if (assets != null && assets.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(assets[0]);
            s = AssetDatabase.LoadAssetAtPath<Settings>(path);
        }
        if (s == null)
        {
            s = CreateInstance<Settings>();
            s.mappings = new List<LayerToMaterial>() { new LayerToMaterial { layer = 0, material = null } };
            string dir = "Assets/Editor";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Editor");
            AssetDatabase.CreateAsset(s, $"{dir}/{kSettingsAssetName}.asset");
            AssetDatabase.SaveAssets();
        }
        return s;
    }

    #region Core Logic

    private static RenderersState GetOrCreateStateForActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            throw new InvalidOperationException("Active scene is not valid.");
        }

        // Try to find existing state holder in scene root
        foreach (var root in scene.GetRootGameObjects())
        {
            var state = root.GetComponent<RenderersState>();
            if (state != null) return state;
        }

        // Create a hidden holder object so state is serialized with the scene
        var go = new GameObject("__LayerMaterialApplier_State");
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
        SceneManager.MoveGameObjectToScene(go, scene);
        var holder = go.AddComponent<RenderersState>();
        holder.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInBuild;
        EditorSceneManager.MarkSceneDirty(scene);
        return holder;
    }

    private void ApplyToScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        var state = GetOrCreateStateForActiveScene();
        var renderers = EnumerateAllRenderersInScene(scene, _settings.includeInactive).ToArray();

        int changed = 0;
        try
        {
            Undo.RegisterCompleteObjectUndo(state, "Apply Layer Materials (State)");

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var go = r.gameObject;
                if (!go.scene.IsValid()) continue;
                var mapping = FindMappingForLayer(go.layer);
                if (mapping == null || mapping.Value.material == null) continue;

                // Save originals only once per renderer (first time it's affected)
                if (!state.HasOriginal(r))
                {
                    state.SaveOriginal(r);
                }

                ApplyMaterialToRenderer(r, mapping.Value.material, _settings.keepMaterialArrayLength);
                changed++;
            }
        }
        finally
        {
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        ShowNotification(new GUIContent($"Applied to {changed} renderer(s)."));
    }

    private void RevertAll()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        var state = GetOrCreateStateForActiveScene();
        int reverted = 0;
        try
        {
            // We may lose references if renderers got destroyed; clean will happen after revert loop
            foreach (var entry in state.Entries.ToArray())
            {
                if (entry.renderer == null) continue;
                Undo.RecordObject(entry.renderer, "Revert Layer Materials");
                entry.renderer.sharedMaterials = entry.originals;
                EditorUtility.SetDirty(entry.renderer);
                reverted++;
            }
        }
        finally
        {
            state.Clear();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        ShowNotification(new GUIContent($"Reverted {reverted} renderer(s)."));
    }

    private LayerToMaterial? FindMappingForLayer(int layer)
    {
        foreach (var m in _settings.mappings)
        {
            if (m.layer == layer) return m;
        }
        return null;
    }

    private static IEnumerable<Renderer> EnumerateAllRenderersInScene(Scene scene, bool includeInactive)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(includeInactive))
            {
                // Limit to MeshRenderer and SkinnedMeshRenderer by default (others still derive from Renderer; we keep them to be generic)
                yield return r;
            }
        }
    }

    private static void ApplyMaterialToRenderer(Renderer r, Material mat, bool keepArrayLength)
    {
        if (r == null || mat == null) return;
        Undo.RecordObject(r, "Apply Layer Material");
        var current = r.sharedMaterials;
        if (current == null || current.Length == 0)
        {
            r.sharedMaterial = mat;
        }
        else
        {
            if (keepArrayLength)
            {
                for (int i = 0; i < current.Length; i++) current[i] = mat;
                r.sharedMaterials = current;
            }
            else
            {
                r.sharedMaterials = new[] { mat };
            }
        }
        EditorUtility.SetDirty(r);
    }

    #endregion

    #region State Holder (serialized in the scene)

    /// <summary>
    /// Holds original materials per Renderer so we can revert later, serialized with the scene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RenderersState : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Renderer renderer;
            public Material[] originals;
        }

        [SerializeField]
        private List<Entry> _entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => _entries;

        public bool HasOriginal(Renderer r)
        {
            if (r == null) return false;
            return _entries.Any(e => e.renderer == r);
        }

        public void SaveOriginal(Renderer r)
        {
            if (r == null) return;
            if (HasOriginal(r)) return;
            var entry = new Entry
            {
                renderer = r,
                originals = r.sharedMaterials.ToArray()
            };
            _entries.Add(entry);
            EditorUtility.SetDirty(this);
        }

        public void Clear()
        {
            _entries.Clear();
            EditorUtility.SetDirty(this);
        }

        private void Awake()
        {
            // keep this holder hidden from hierarchy to avoid confusion
            if (!Application.isPlaying)
            {
                gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
            }
        }
    }

    #endregion
}
#endif
