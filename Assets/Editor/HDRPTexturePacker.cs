// Assets/Editor/HDRPTexturePacker.cs
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if USING_HDRP || UNITY_6000_0_OR_NEWER
using UnityEditor.Rendering.HighDefinition;
using UnityEngine.Rendering.HighDefinition;
#endif

public class HDRPTexturePacker : Editor
{
    // --- Mots-clés (tout en minuscules) ---
    static readonly string[] KW_BASE = { "base color", "basecolor", "albedo", "diffuse", "color", "base" };
    static readonly string[] KW_METAL = { "metallic", "metalness", "metal" };
    static readonly string[] KW_ROUGH = { "roughness", "rough" };
    static readonly string[] KW_NORMAL = { "normal directx", "normal_dx", "normaldx", "normal" };
    static readonly string[] KW_AO = { "ambient occlusion", "ambientocclusion", "ao", "occlusion" };
    static readonly string[] KW_DETAIL = { "detail mask", "detailmask", "detail" };
    static readonly string[] KW_EMISS = { "emissive", "emission", "emit" };
    static readonly string[] KW_HEIGHT = { "height", "displacement", "disp" };
    static readonly string[] KW_MASKMAP = { "mask map", "maskmap", "mask" };

    [MenuItem("Tools/HDRP/Convert Selected to HDRP/Lit")]
    public static void ConvertSelected()
    {
        var sel = Selection.objects;
        if (sel == null || sel.Length == 0)
        {
            Debug.LogWarning("[HDRP] Sélectionne au moins un dossier dans l’onglet Project.");
            return;
        }

        int folders = 0, groups = 0, mats = 0, maskMaps = 0;

        foreach (var o in sel)
        {
            var rootPath = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                Debug.Log($"[HDRP] Ignoré (pas un dossier) : {rootPath}");
                continue;
            }
            folders++;

            // Récupère toutes les textures du dossier
            var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootPath });
            if (texGuids.Length == 0)
            {
                Debug.Log($"[HDRP] Aucun Texture2D dans : {rootPath}");
                continue;
            }

            // Regroupe par "root" (partie avant le premier mot-clé reconnu)
            var perRoot = new Dictionary<string, List<string>>();
            foreach (var g in texGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var name = Path.GetFileNameWithoutExtension(p);
                var root = GetRootName(name);
                if (!perRoot.TryGetValue(root, out var list))
                    perRoot[root] = list = new List<string>();
                list.Add(p);
            }

            foreach (var kv in perRoot)
            {
                groups++;
                maskMaps += BuildMaterialForGroup(kv.Key, kv.Value, ref mats);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HDRP] Terminé. Dossiers: {folders}, Groupes: {groups}, Matériaux créés/MAJ: {mats}, MaskMaps créés: {maskMaps}");
    }

    // ---- Matériau HDRP/Lit pour un groupe (root) ----
    static int BuildMaterialForGroup(string root, List<string> paths, ref int matCounter)
    {
        if (paths == null || paths.Count == 0) return 0;
        string dir = Path.GetDirectoryName(paths[0]).Replace("\\", "/");

        // Cherche la meilleure texture par type via mots-clés
        var baseCol = FindTex(dir, root, KW_BASE);
        var normal = FindTex(dir, root, KW_NORMAL);
        var metal = FindTex(dir, root, KW_METAL);
        var rough = FindTex(dir, root, KW_ROUGH);
        var ao = FindTex(dir, root, KW_AO);
        var detail = FindTex(dir, root, KW_DETAIL);
        var emiss = FindTex(dir, root, KW_EMISS);
        var height = FindTex(dir, root, KW_HEIGHT);
        var mask = FindTex(dir, root, KW_MASKMAP);

        int createdMask = 0;
        if (mask == null)
        {
            var mm = PackMaskMap(dir, root, metal, ao, detail, rough);
            if (!string.IsNullOrEmpty(mm))
            {
                mask = AssetDatabase.LoadAssetAtPath<Texture2D>(mm);
                createdMask = 1;
                Debug.Log($"[HDRP] MaskMap créé: {mm}");
            }
        }

        if (baseCol == null && normal == null && mask == null)
        {
            Debug.Log($"[HDRP] Groupe '{root}': aucune map utile trouvée → ignoré.");
            return createdMask;
        }

        // Crée / met à jour le material HDRP/Lit
        var matPath = $"{dir}/{root}_HDRP_Lit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
            matCounter++;
            Debug.Log($"[HDRP] Mat créé: {matPath}");
        }
        else
        {
            Debug.Log($"[HDRP] Mat mis à jour: {matPath}");
        }

        // Assignations + import settings
        if (baseCol) { mat.SetTexture("_BaseColorMap", baseCol); SetSRGB(baseCol, true); }
        if (normal)
        {
            mat.SetTexture("_NormalMap", normal);
            MarkNormal(normal);
            if (ContainsAny(normal.name, new[] { "normal directx", "normal_dx", "normaldx" }))
                Debug.Log($"[HDRP] '{normal.name}': si le relief paraît inversé, coche 'Flip Y/Green' dans l’import.");
        }
        if (mask) { mat.SetTexture("_MaskMap", mask); SetSRGB(mask, false); }
        if (height) { mat.SetTexture("_HeightMap", height); SetSRGB(height, false); }
        if (emiss)
        {
            mat.SetTexture("_EmissiveColorMap", emiss);
            SetSRGB(emiss, true);
            mat.SetColor("_EmissiveColor", Color.white);
            mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
            mat.SetFloat("_EmissiveIntensity", 1f);
        }

#if USING_HDRP || UNITY_6000_0_OR_NEWER
        HDMaterial.ValidateMaterial(mat);
#endif

        EditorUtility.SetDirty(mat);
        return createdMask;
    }

    // ---- Recherche par mots-clés, avec scoring ----
    static Texture2D FindTex(string dir, string root, string[] keys)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
        int bestScore = 0; string bestPath = null;
        var rootN = N(root);

        foreach (var guid in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(p);
            var nameN = N(name);

            int score = 0;
            if (!string.IsNullOrEmpty(rootN) && nameN.StartsWith(rootN)) score++; // petite priorité si commence par root
            foreach (var k in keys) if (ContainsAny(name, new[] { k })) score++;  // +1 par mot-clé présent

            if (score > bestScore)
            {
                bestScore = score;
                bestPath = p;
            }
        }
        return bestPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(bestPath) : null;
    }

    // ---- Création MaskMap (R=Metallic, G=AO(ou1), B=Detail(ou1), A=Smoothness=1−Roughness) ----
    static string PackMaskMap(string dir, string root, Texture2D metal, Texture2D ao, Texture2D detail, Texture2D rough)
    {
        var refTex = metal ?? ao ?? rough ?? detail;
        if (refTex == null) return null;

        int w = refTex.width, h = refTex.height;

        MakeReadable(metal);
        MakeReadable(ao);
        MakeReadable(detail);
        MakeReadable(rough);

        var r = GetGray(metal, w, h, 0f); // Metallic
        var g = GetGray(ao, w, h, 1f); // AO (1 si manquant)
        var b = GetGray(detail, w, h, 1f); // Detail (1 si manquant)
        var a = GetGray(rough, w, h, 0f); // Roughness (0 si manquant) → sera inversée

        for (int i = 0; i < a.Length; i++) a[i] = 1f - a[i]; // Smoothness

        var outPixels = new Color32[w * h];
        for (int i = 0; i < outPixels.Length; i++)
            outPixels[i] = new Color32(F(r[i]), F(g[i]), F(b[i]), F(a[i]));

        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        outTex.SetPixels32(outPixels);
        outTex.Apply(false, false);

        var outPath = $"{dir}/{root}_MaskMap.png";
        File.WriteAllBytes(outPath, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);

        AssetDatabase.ImportAsset(outPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
        if (ti != null)
        {
            ti.sRGBTexture = false; // données, pas couleur
            ti.SaveAndReimport();
        }
        return outPath;
    }

    static float[] GetGray(Texture2D tex, int w, int h, float fallback)
    {
        var arr = new float[w * h];
        if (tex == null) { for (int i = 0; i < arr.Length; i++) arr[i] = fallback; return arr; }

        var path = AssetDatabase.GetAssetPath(tex);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti != null && !ti.isReadable) { ti.isReadable = true; ti.SaveAndReimport(); }

        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var px = t.GetPixels();
        for (int i = 0; i < px.Length; i++) arr[i] = px[i].grayscale;
        return arr;
    }

    static byte F(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

    // ---- Import settings helpers ----
    static bool MakeReadable(Texture2D tex)
    {
        if (tex == null) return false;
        var path = AssetDatabase.GetAssetPath(tex);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return false;

        bool changed = false;
        // sRGB ON pour albedo/emissive ; OFF sinon
        bool shouldSRGB = IsColorLike(path);
        if (ti.sRGBTexture != shouldSRGB) { ti.sRGBTexture = shouldSRGB; changed = true; }
        if (!ti.isReadable) { ti.isReadable = true; changed = true; }

        if (changed) ti.SaveAndReimport();
        return true;
    }

    static void SetSRGB(Texture2D t, bool sRGB)
    {
        if (t == null) return;
        var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
        if (ti != null && ti.sRGBTexture != sRGB) { ti.sRGBTexture = sRGB; ti.SaveAndReimport(); }
    }

    static void MarkNormal(Texture2D t)
    {
        if (t == null) return;
        var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
    }

    static bool IsColorLike(string path)
    {
        var n = N(Path.GetFileNameWithoutExtension(path));
        return KW_BASE.Any(k => n.Contains(N(k))) || KW_EMISS.Any(k => n.Contains(N(k)));
    }

    // ---- Normalisation nom + matching ----
    static string N(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        while (t.Contains("  ")) t = t.Replace("  ", " ");
        return t.Trim();
    }

    static bool ContainsAny(string name, string[] keys)
    {
        var n = N(name);
        foreach (var k in keys) if (n.Contains(N(k))) return true;
        return false;
    }

    // Coupe le nom avant le premier mot-clé rencontré (sinon renvoie le nom complet)
    static string GetRootName(string name)
    {
        var n = N(name);
        var all = KW_BASE.Concat(KW_METAL).Concat(KW_ROUGH).Concat(KW_NORMAL)
                         .Concat(KW_AO).Concat(KW_DETAIL).Concat(KW_EMISS)
                         .Concat(KW_HEIGHT).Concat(KW_MASKMAP).Select(N).ToArray();
        int cut = -1;
        foreach (var k in all)
        {
            int idx = n.IndexOf(k);
            if (idx >= 0) cut = (cut < 0) ? idx : Mathf.Min(cut, idx);
        }
        if (cut > 0) return name.Substring(0, cut).TrimEnd('_', '-', ' ');
        return name;
    }
}
