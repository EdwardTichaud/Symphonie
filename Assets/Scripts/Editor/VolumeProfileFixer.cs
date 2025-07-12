using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class VolumeProfileFixer
{
    [MenuItem("Tools/Clean Null Volume Components")]
    public static void CleanNullComponents()
    {
        string[] guids = AssetDatabase.FindAssets("t:VolumeProfile");
        int cleanedProfiles = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
                continue;

            int before = profile.components.Count;
            profile.components.RemoveAll(c => c == null);
            if (profile.components.Count != before)
            {
                cleanedProfiles++;
                EditorUtility.SetDirty(profile);
                Debug.Log($"Nettoyage des composants nuls dans {path}");
            }
        }

        if (cleanedProfiles > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"{cleanedProfiles} profils nettoyés.");
        }
        else
        {
            Debug.Log("Aucun composant nul trouvé dans les profils de volume.");
        }
    }
}
