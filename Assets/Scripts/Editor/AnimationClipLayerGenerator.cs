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

            // On identifie les clips Humanoid : dans ce mode Unity remplace les paths par des courbes de muscles
            // liées à l'Animator. Les règles de filtrage basées sur les chemins d'os seraient donc inefficaces et
            // risqueraient de supprimer tout le contenu utile.
            bool isHumanoidClip = IsLikelyHumanoidClip(floatBindings, pptrBindings);
            if (isHumanoidClip)
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

                // FILTRAGE : si le clip est Humanoid on le laisse intact et on délègue le masquage au runtime
                // via un AvatarMask. Sinon on applique le filtrage détaillé par chemins d'os/blendshapes.
                if (!isHumanoidClip)
                {
                    FilterClip(newClip, rule);
                }

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

    // Catégories anatomiques supportées par les heuristiques de filtrage.
    enum BodyRegion
    {
        Unknown,
        UpperBody,
        Face,
        Torso,
        HeadAndNeck,
    }

    // Fragments utilisés pour identifier chaque catégorie (tous en minuscules pour éviter les ToLower multiples).
    static readonly string[] UpperBodyInclude =
    {
        "spine_02", "spine_03", "cc_base_l_ribstwist", "cc_base_r_ribstwist", "clavicle", "shoulder",
        "upperarm", "lowerarm", "forearm", "hand", "arm", "weapon", "elbow", "wrist"
    };
    static readonly string[] UpperBodyExclude =
    {
        "pelvis", "thigh", "calf", "foot", "ball", "toe", "finger", "thumb", "head", "neck",
        "cc_base_facialbone", "jaw", "eye", "teeth", "tongue"
    };
    static readonly string[] FaceInclude =
    {
        "cc_base_facialbone", "facial", "face", "jaw", "lip", "mouth", "eye", "eyebrow", "brow",
        "nose", "tongue", "teeth", "cheek"
    };
    static readonly string[] TorsoInclude =
    {
        "spine_01", "spine_02", "spine_03", "chest", "spine", "ribcage", "torso"
    };
    static readonly string[] TorsoExclude =
    {
        "clavicle", "upperarm", "lowerarm", "hand", "arm", "weapon", "head", "neck", "facial",
        "cc_base_facialbone", "jaw", "tongue", "eye", "teeth"
    };
    static readonly string[] HeadNeckInclude =
    {
        "neck", "head", "c_neck", "c_head", "neck_01", "neck_02", "neck_03", "headtop", "skull"
    };
    static readonly string[] HeadNeckExclude =
    {
        "cc_base_facialbone", "jaw", "tongue", "eye", "teeth", "brow", "lip"
    };

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

    /// <summary>
    /// Détecte les clips importés en mode Humanoid. Dans ce cas Unity convertit les courbes en muscles et
    /// supprime les paths. On vérifie donc l'absence totale de chemins combinée à la présence de bindings
    /// orientés Animator (signature typique des clips Humanoid).
    /// </summary>
    static bool IsLikelyHumanoidClip(EditorCurveBinding[] floatBindings, EditorCurveBinding[] pptrBindings)
    {
        if (floatBindings == null) floatBindings = System.Array.Empty<EditorCurveBinding>();
        if (pptrBindings == null) pptrBindings = System.Array.Empty<EditorCurveBinding>();

        bool HasAnyPath(EditorCurveBinding[] bindings) => bindings.Any(b => !string.IsNullOrEmpty(b.path));

        // Les clips Humanoid n'exposent aucun path : toutes les courbes sont au niveau Animator.
        if (HasAnyPath(floatBindings) || HasAnyPath(pptrBindings))
            return false;

        // Dans le doute on vérifie la présence d'au moins une courbe pilotant l'Animator (muscles/quaternions).
        bool hasAnimatorDrivenCurve = floatBindings.Any(b => b.type == typeof(Animator));

        return hasAnimatorDrivenCurve;
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

        // Heuristiques anatomiques : si la règle correspond à une zone connue et que le chemin ne colle pas à
        // cette zone, on ignore la courbe. Cela sécurise les variantes même lorsque le squelette change légèrement
        // (nouvelles souches de noms d'os par exemple).
        var region = DetectBodyRegion(rule);
        if (region != BodyRegion.Unknown && !MatchesBodyRegion(lowerPath, region))
            return false;

        return true;
    }

    /// <summary>
    /// Détermine automatiquement la zone du corps visée par la règle.
    /// L'objectif est de fiabiliser la génération lorsque les listes include/exclude
    /// n'anticipent pas toutes les variantes de noms de bones.
    /// </summary>
    static BodyRegion DetectBodyRegion(LayerFilterConfig.LayerRule rule)
    {
        if (rule == null) return BodyRegion.Unknown;

        // On normalise les libellés une seule fois pour éviter des ToLower répétitifs.
        string name = (rule.layerName ?? string.Empty).ToLowerInvariant();
        string prefix = (rule.prefix ?? string.Empty).ToLowerInvariant();

        // On vérifie aussi le contenu textuel des fragments pour enrichir la détection.
        bool HasFragment(IEnumerable<string> fragments, params string[] keys)
            => fragments != null && fragments.Any(f =>
                {
                    string lower = (f ?? string.Empty).ToLowerInvariant();
                    return keys.Any(k => lower.Contains(k));
                });

        if (name.Contains("upperbody") || prefix.Contains("upperbody")
            || HasFragment(rule.includePathFragments, "upperarm", "lowerarm", "clavicle", "spine_03"))
            return BodyRegion.UpperBody;

        if (name.Contains("face") || prefix.Contains("face") || rule.keepOnlyBlendshapes
            || HasFragment(rule.includePathFragments, "facial", "jaw", "eye", "brow"))
            return BodyRegion.Face;

        if (name.Contains("torso") || name.Contains("chest") || prefix.Contains("torso")
            || HasFragment(rule.includePathFragments, "spine_01", "spine_02", "chest"))
            return BodyRegion.Torso;

        if (name.Contains("headneck") || name.Contains("head") || name.Contains("neck")
            || prefix.Contains("headneck") || HasFragment(rule.includePathFragments, "neck", "head"))
            return BodyRegion.HeadAndNeck;

        return BodyRegion.Unknown;
    }

    /// <summary>
    /// Vérifie si le chemin appartient bien à la zone détectée. Les heuristiques sont volontairement
    /// permissives : on accepte la courbe si au moins un fragment attendu est trouvé et qu'aucun fragment
    /// d'exclusion majeur n'est détecté.
    /// </summary>
    static bool MatchesBodyRegion(string lowerPath, BodyRegion region)
    {
        if (string.IsNullOrEmpty(lowerPath))
            return region == BodyRegion.Face; // Les blendshapes faciaux n'ont pas forcément de path explicite.

        bool ContainsAny(string[] fragments) => fragments.Any(lowerPath.Contains);

        switch (region)
        {
            case BodyRegion.UpperBody:
                if (!ContainsAny(UpperBodyInclude)) return false;
                if (ContainsAny(UpperBodyExclude)) return false;
                return true;
            case BodyRegion.Face:
                // Si la courbe concerne la tête mais pas explicitement un os facial, on reste permissif pour éviter
                // de filtrer des blendshapes/curves utiles (yeux, mâchoire, etc.).
                return ContainsAny(FaceInclude) || lowerPath.Contains("head") || lowerPath.Contains("cc_base_head");
            case BodyRegion.Torso:
                if (!ContainsAny(TorsoInclude)) return false;
                if (ContainsAny(TorsoExclude)) return false;
                return true;
            case BodyRegion.HeadAndNeck:
                if (!ContainsAny(HeadNeckInclude)) return false;
                if (ContainsAny(HeadNeckExclude)) return false;
                return true;
            default:
                return true;
        }
    }
}
