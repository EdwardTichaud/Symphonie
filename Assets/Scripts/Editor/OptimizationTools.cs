using UnityEditor;
using UnityEngine;

// Outil d'optimisation pour activer le GPU Instancing et marquer les objets statiques.
// Ces fonctions permettent de r\u00e9duire le nombre de draw calls et d'am\u00e9liorer les performances.
public static class OptimizationTools
{
    // Active le GPU Instancing pour tous les mat\u00e9riaux du projet.
    // Cela permet de partager les m\u00eames shaders entre plusieurs objets
    // et d'\u00e9viter des draw calls inutiles.
    [MenuItem("Symphonie/Optimisation/Activer GPU Instancing sur tous les mat\u00e9riaux")]
    private static void EnableGPUInstancing()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !mat.enableInstancing)
            {
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        Debug.Log($"GPU Instancing activ\u00e9 sur {count} mat\u00e9riaux.");
    }

    // Marque comme statiques les objets de la sc\u00e8ne ne poss\u00e9dant pas de Rigidbody.
    // Le Static Batching sera ainsi appliqu\u00e9 automatiquement par Unity.
    [MenuItem("Symphonie/Optimisation/Marquer les objets statiques de la sc\u00e8ne")]
    private static void MarkSceneObjectsStatic()
    {
        // Utilise la nouvelle API recommandée par Unity pour récupérer les objets de la scène
        // sans coût supplémentaire de tri lorsque ce n'est pas nécessaire.
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;
        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<MeshRenderer>() != null && go.GetComponent<Rigidbody>() == null)
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                count++;
            }
        }
        Debug.Log($"{count} objets marqu\u00e9s comme statiques.");
    }
}
