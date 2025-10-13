using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class AnimationClipLayerGenerator : EditorWindow
{
    LayerFilterConfig config;
    DefaultAsset sourceFolder;
    DefaultAsset destinationFolder;
    bool recurseSubFolders = true;

    [MenuItem("Tools/Animator Tools/Generate Layer Variants (CC)")]
    static void Open() => GetWindow<AnimationClipLayerGenerator>("Layer Variants (CC)");

    void OnGUI()
    {
        GUILayout.Label("Générateur de variantes par Layer", EditorStyles.boldLabel);
        config = (LayerFilterConfig)EditorGUILayout.ObjectField("Config (ScriptableObject)", config, typeof(LayerFilterConfig), false);
        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Dossier Source", sourceFolder, typeof(DefaultAsset), false);
        destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField("Dossier Cible", destinationFolder, typeof(DefaultAsset), false);
        recurseSubFolders = EditorGUILayout.ToggleLeft("Inclure sous-dossiers", recurseSubFolders);

        EditorGUILayout.Space();
        if (GUILayout.Button("Générer les variantes"))
        {
            if (config == null) { Debug.LogError("Config manquante."); return; }
            if (sourceFolder == null) { Debug.LogError("Dossier source manquant."); return; }
            if (destinationFolder == null) { Debug.LogError("Dossier cible manquant."); return; }

            string srcPath = AssetDatabase.GetAssetPath(sourceFolder);
            string dstPath = AssetDatabase.GetAssetPath(destinationFolder);
            if (!AssetDatabase.IsValidFolder(srcPath) || !AssetDatabase.IsValidFolder(dstPath))
            {
                Debug.LogError("Source ou Cible n'est pas un dossier valide dans le projet.");
                return;
            }

            GenerateAll(config, srcPath, dstPath, recurseSubFolders);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Si un clip est importé en Humanoid (courbes muscle), le filtrage par chemin est impossible. " +
            "Le générateur dupliquera le clip mais laissera les courbes intactes (utilise alors des Avatar Masks au runtime).",
            MessageType.Info);
    }

    // ---- CORE ----

    static void GenerateAll(LayerFilterConfig cfg, string srcPath, string dstPath, bool recurse)
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { srcPath });
        int created = 0, scanned = 0;

        // Prépare des helpers de matching (case-insensitive)
        foreach (var guid in guids)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(guid);
            if (Directory.Exists(clipPath)) continue; // safety

            if (!recurse && Path.GetDirectoryName(clipPath).Replace('\\', '/') != srcPath.Replace('\\', '/'))
                continue;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) continue;
            scanned++;

            string baseName = Path.GetFileNameWithoutExtension(clipPath);

            // Inspect binding type pour prévenir en cas de muscles (Humanoid)
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            var pptrBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            bool hasTransformPaths = floatBindings.Any(b => !string.IsNullOrEmpty(b.path))
                                   || pptrBindings.Any(b => !string.IsNullOrEmpty(b.path));
            if (!hasTransformPaths)
            {
                Debug.LogWarning($"[LayerGen] '{baseName}' semble être Humanoid (muscles). " +
                                 $"Les filtres par path ne s'appliqueront pas (duplication brute).");
            }

            foreach (var rule in cfg.layers)
            {
                string newName = $"{rule.prefix}{baseName}.anim";
                string outPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dstPath, newName));

                var newClip = new AnimationClip();
                if (rule.copyFrameRate) newClip.frameRate = clip.frameRate;

                // Copie grossière (courbes & settings) puis on nettoie
                EditorUtility.CopySerialized(clip, newClip);

                // FILTRAGE
                FilterClip(newClip, rule);

                AssetDatabase.CreateAsset(newClip, outPath);

                // Évents
                if (!rule.copyAnimationEvents)
                    AnimationUtility.SetAnimationEvents(newClip, new AnimationEvent[0]);

                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"✅ Génération terminée. Clips scannés: {scanned} — Variantes créées: {created} (destination: {dstPath})");
    }

    static void FilterClip(AnimationClip clip, LayerFilterConfig.LayerRule rule)
    {
        // Float curves (inclut transforms, blendshapes, constraints, etc.)
        var floatBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var b in floatBindings)
        {
            bool isBlendshape = b.propertyName != null && b.propertyName.StartsWith("blendShape.");
            string path = (b.path ?? string.Empty).ToLowerInvariant();

            // Blendshape rules
            if (rule.keepOnlyBlendshapes && !isBlendshape)
            {
                AnimationUtility.SetEditorCurve(clip, b, null);
                continue;
            }
            if (rule.dropAllBlendshapes && isBlendshape)
            {
                AnimationUtility.SetEditorCurve(clip, b, null);
                continue;
            }
            if (isBlendshape && rule.allowedBlendshapeMeshPaths != null && rule.allowedBlendshapeMeshPaths.Count > 0)
            {
                if (!rule.allowedBlendshapeMeshPaths.Any(f => path.Contains(f.ToLowerInvariant())))
                {
                    AnimationUtility.SetEditorCurve(clip, b, null);
                    continue;
                }
            }

            // Path filters
            if (!PassesPathFilters(path, rule))
            {
                AnimationUtility.SetEditorCurve(clip, b, null);
            }
        }

        // PPtr curves (sprites, materials, etc.) — très rare ici mais on aligne le comportement
        var pptrBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var bb in pptrBindings)
        {
            string path = (bb.path ?? string.Empty).ToLowerInvariant();
            if (!PassesPathFilters(path, rule))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, bb, null);
            }
        }
    }

    static bool PassesPathFilters(string lowerPath, LayerFilterConfig.LayerRule rule)
    {
        // Include
        bool includeOk;
        if (rule.includePathFragments == null || rule.includePathFragments.Count == 0)
            includeOk = rule.includeAllIfEmpty;
        else
            includeOk = rule.includePathFragments.Any(f => lowerPath.Contains((f ?? "").ToLowerInvariant()));

        if (!includeOk) return false;

        // Exclude
        if (rule.excludePathFragments != null && rule.excludePathFragments.Count > 0)
        {
            if (rule.excludePathFragments.Any(f => lowerPath.Contains((f ?? "").ToLowerInvariant())))
                return false;
        }
        return true;
    }
}
