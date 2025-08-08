// Ce fichier fournit des utilitaires pour récupérer les noms des Rendering Layers
// dans l'Éditeur Unity. Entre les différentes versions de Unity, l'API permettant
// d'accéder à ces noms a changé de localisation et de dénomination. L'objectif de
// cette classe est d'offrir un point d'accès unique, tout en évitant les erreurs de
// compilation lorsque certaines propriétés ne sont pas présentes.

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RenderingLayerHelper
{
    /// <summary>
    /// Retourne les noms des Rendering Layers configurés dans le projet.
    /// Dans l'éditeur, on tente d'abord d'accéder à la propriété
    /// <c>GraphicsSettings.renderingLayerNames</c>. Si celle-ci n'est pas disponible
    /// (certaines versions plus anciennes ou plus récentes de Unity la déplacent ou
    /// la renomment), on tente ensuite d'accéder à
    /// <c>EditorGraphicsSettings.defaultRenderingLayerMaskNames</c>. En build, où ces
    /// informations ne sont pas accessibles, on renvoie simplement un tableau vide.
    /// </summary>
    /// <returns>Tableau des noms des Rendering Layers ou tableau vide.</returns>
    public static string[] GetRenderingLayerNames()
    {
#if UNITY_EDITOR
        // --- Tentative 1 : utiliser GraphicsSettings.renderingLayerNames --------------------
        // Cette propriété est disponible sur les versions récentes de Unity.
        var graphicsType = typeof(UnityEngine.Rendering.GraphicsSettings);
        var renderingLayerProp = graphicsType.GetProperty(
            "renderingLayerNames",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (renderingLayerProp != null)
        {
            // Si la propriété existe, on renvoie sa valeur immédiatement.
            return renderingLayerProp.GetValue(null, null) as string[] ?? System.Array.Empty<string>();
        }

        // --- Tentative 2 : utiliser EditorGraphicsSettings.defaultRenderingLayerMaskNames ---
        // Certaines versions stockent les noms par défaut dans l'EditorGraphicsSettings.
        var editorType = typeof(UnityEditor.Rendering.EditorGraphicsSettings);
        renderingLayerProp = editorType.GetProperty(
            "defaultRenderingLayerMaskNames",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (renderingLayerProp != null)
        {
            return renderingLayerProp.GetValue(null, null) as string[] ?? System.Array.Empty<string>();
        }

        // Si aucune API n'est disponible, on renvoie un tableau vide pour éviter les plantages.
        return System.Array.Empty<string>();
#else
        // En dehors de l'éditeur (exécution ou build), les noms ne sont pas disponibles.
        return System.Array.Empty<string>();
#endif
    }
}
