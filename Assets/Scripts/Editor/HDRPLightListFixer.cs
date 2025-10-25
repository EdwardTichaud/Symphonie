// Ce script d'éditeur garantit que le pipeline HDRP dispose bien des buffers
// de listes de lumières requis (comme g_vLightListCluster) afin d'éviter les
// avertissements "buffer non fourni" observés avec Direct3D 12.
// Il force l'activation de l'option Tile & Cluster sur l'asset HDRP courant.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Symphonie.EditorTools
{
    /// <summary>
    /// Utilitaire d'auto-réparation pour le pipeline HDRP : il s'assure que
    /// l'asset HDRP actif est configuré pour fournir les buffers de lumière
    /// attendus par le shader "HDRP/Lit" (dont g_vLightListCluster).
    /// </summary>
    [InitializeOnLoad]
    public static class HDRPLightListFixer
    {
        /// <summary>
        /// Le constructeur statique est appelé dès le chargement de l'éditeur et
        /// applique immédiatement la correction afin que toutes les scènes
        /// ouvertes bénéficient de la configuration attendue.
        /// </summary>
        static HDRPLightListFixer()
        {
            TryForceTileAndCluster(enableLogs: false);
        }

        /// <summary>
        /// Expose également la correction via un menu afin de pouvoir la relancer
        /// manuellement si besoin (par exemple après la création d'un nouvel asset
        /// HDRP).
        /// </summary>
        [MenuItem("Symphonie/Outils HDRP/Forcer Tile & Cluster", priority = 100)]
        private static void ForceTileAndClusterMenu()
        {
            TryForceTileAndCluster(enableLogs: true);
        }

        /// <summary>
        /// Applique la correction en modifiant la propriété sérialisée
        /// "m_RenderPipelineSettings.lightLoopSettings.enableTileAndCluster" de
        /// l'asset HDRP courant.
        /// </summary>
        /// <param name="enableLogs">Affiche un log informatif si vrai.</param>
        private static void TryForceTileAndCluster(bool enableLogs)
        {
            // On récupère l'asset HDRP actuellement assigné dans les Graphics Settings.
            // On évite l'utilisation des nouveaux patterns C# pour maximiser la
            // compatibilité avec la version du langage supportée par Unity.
            var renderPipeline = GraphicsSettings.currentRenderPipeline;
            if (!(renderPipeline is HDRenderPipelineAsset hdAsset))
            {
                if (enableLogs)
                {
                    Debug.LogWarning(
                        "Aucun asset HDRP actif n'a été trouvé dans les Graphics Settings.");
                }
                return;
            }

            // On édite l'asset via SerializedObject pour être compatible avec les
            // versions d'Unity où la propriété n'est pas exposée directement en C#.
            var serializedAsset = new SerializedObject(hdAsset);
            SerializedProperty enableTileAndCluster = serializedAsset.FindProperty(
                "m_RenderPipelineSettings.lightLoopSettings.enableTileAndCluster");

            if (enableTileAndCluster == null)
            {
                if (enableLogs)
                {
                    Debug.LogWarning(
                        "Impossible de localiser la propriété enableTileAndCluster dans l'asset HDRP.");
                }
                return;
            }

            if (!enableTileAndCluster.boolValue)
            {
                enableTileAndCluster.boolValue = true;
                serializedAsset.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hdAsset);
                AssetDatabase.SaveAssets();

                if (enableLogs)
                {
                    Debug.Log(
                        "L'option Tile & Cluster a été activée pour l'asset HDRP afin d'exposer g_vLightListCluster.");
                }
            }
            else if (enableLogs)
            {
                Debug.Log(
                    "L'asset HDRP disposait déjà de l'option Tile & Cluster activée.");
            }
        }
    }
}
