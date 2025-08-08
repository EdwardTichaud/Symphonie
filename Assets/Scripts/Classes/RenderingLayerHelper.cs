// Ce fichier fournit des utilitaires pour récupérer les noms des Rendering Layers
// dans l'Éditeur Unity. L'API GraphicsSettings.renderingLayerNames n'est pas disponible
// dans toutes les versions de Unity, ce qui provoquait une erreur de compilation.
// On utilise donc l'API d'éditeur lorsque disponible et un tableau vide sinon.

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RenderingLayerHelper
{
    /// <summary>
    /// Retourne les noms des Rendering Layers configurés dans le projet.
    /// Dans l'éditeur, on s'appuie sur EditorGraphicsSettings. En build, où cette
    /// information n'est pas accessible, on renvoie simplement un tableau vide.
    /// </summary>
    /// <returns>Tableau des noms des Rendering Layers ou tableau vide.</returns>
    public static string[] GetRenderingLayerNames()
    {
#if UNITY_EDITOR
        // Utilisation de l'API EditorGraphicsSettings pour obtenir les noms des layers.
        return UnityEditor.Rendering.EditorGraphicsSettings.renderingLayerNames;
#else
        // En dehors de l'éditeur (exécution ou build), les noms ne sont pas disponibles.
        return System.Array.Empty<string>();
#endif
    }
}
