#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Inspecteur personnalisé pour <see cref="SetRenderingLayer"/>.
/// Ajoute un bouton permettant d'appliquer les Rendering Layers à
/// l'ensemble de la scène.
/// </summary>
[CustomEditor(typeof(SetRenderingLayer))]
public class SetRenderingLayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Affiche l'inspecteur par défaut pour les champs publics
        DrawDefaultInspector();

        EditorGUILayout.Space();

        // Récupère une référence typée vers l'objet cible
        var script = (SetRenderingLayer)target;

        // Bouton d'application globale
        if (GUILayout.Button("Appliquer à toute la scène"))
        {
            // Lance le traitement sur tous les objets de la scène
            script.ApplyToAll();
        }
    }
}
#endif
