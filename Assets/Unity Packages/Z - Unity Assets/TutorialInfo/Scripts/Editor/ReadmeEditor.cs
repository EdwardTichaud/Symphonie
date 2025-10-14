using System.IO;
using UnityEditor;
using UnityEngine;

namespace TutorialInfo
{
    /// <summary>
    /// Remplace l'éditeur automatique de readme des assets Unity pour éviter les erreurs au démarrage.
    /// Cette version vérifie l'existence du fichier avant de tenter de le charger, afin de supprimer le spam dans la console.
    /// </summary>
    [InitializeOnLoad]
    public static class ReadmeEditor
    {
        private const string ReadmeAssetName = "Readme";
        private const string ReadmeAssetExtension = ".asset";

        static ReadmeEditor()
        {
            // S'assure que la sélection automatique ne déclenche plus d'exception lorsqu'un readme manque.
            EditorApplication.delayCall += SelectReadmeAutomatically;
        }

        private static void SelectReadmeAutomatically()
        {
            if (Application.isBatchMode)
            {
                // Pas besoin de forcer la sélection en mode batch : cela évite du bruit inutile.
                return;
            }

            string assetPath = FindReadmeAssetPath();
            if (string.IsNullOrEmpty(assetPath))
            {
                // Journalise une information discrète pour prévenir qu'un readme est absent, sans générer d'erreur bloquante.
                Debug.Log("Readme introuvable. Le sélecteur automatique a ignoré la requête.");
                return;
            }

            var readme = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (readme != null)
            {
                Selection.activeObject = readme;
            }
        }

        private static string FindReadmeAssetPath()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject " + ReadmeAssetName);
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(candidatePath) &&
                    candidatePath.EndsWith(ReadmeAssetExtension) &&
                    Path.GetFileNameWithoutExtension(candidatePath) == ReadmeAssetName)
                {
                    return candidatePath;
                }
            }

            return null;
        }
    }
}
