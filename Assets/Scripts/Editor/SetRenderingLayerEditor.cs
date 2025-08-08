#if UNITY_EDITOR
using UnityEditor;
using UnityEngine; // Nécessaire pour l'utilisation de GUILayout dans l'inspecteur

/// <summary>
/// Inspecteur personnalisé pour <see cref="SetRenderingLayer"/>.
/// Son rôle est d'offrir un raccourci visuel afin d'appliquer
/// les <c>Rendering Layers</c> à l'ensemble des objets de la scène
/// (meshs et terrains inclus).
/// </summary>
[CustomEditor(typeof(SetRenderingLayer))]
public class SetRenderingLayerEditor : Editor
{
    /// <summary>
    /// Dessine l'interface de l'inspecteur.
    /// On affiche d'abord l'inspecteur par défaut puis on ajoute
    /// un bouton dédié à l'application globale des Rendering Layers.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Affiche l'inspecteur par défaut pour les champs publics
        DrawDefaultInspector();

        // Ajoute un peu d'espace pour séparer visuellement le bouton
        // des champs de données classiques
        EditorGUILayout.Space();

        // Récupère une référence typée vers l'objet cible
        var script = (SetRenderingLayer)target;

        // Bouton d'application globale. Il remplace l'ancien
        // comportement "ApplyToChildren" afin de cibler tous les
        // objets de la scène (qu'ils soient des meshs ou des terrains).
        if (GUILayout.Button("Appliquer à toute la scène"))
        {
            // Lance le traitement sur tous les objets de la scène
            // en s'appuyant sur la méthode publique "ApplyToAll".
            script.ApplyToAll();
        }
    }
}
#endif
