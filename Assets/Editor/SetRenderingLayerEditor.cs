using UnityEditor;
using UnityEngine;
using UnityEditorInternal; // Pour accéder aux noms de couches Unity

[CustomEditor(typeof(SetRenderingLayer))]
public class SetRenderingLayerEditor : Editor
{
    private string[] layerNames; // Liste des Rendering Layers disponibles
    private int selectedIndex; // Index du Rendering Layer sélectionné

    private void OnEnable()
    {
        layerNames = UnityEngine.Rendering.GraphicsSettings.renderingLayerNames;
        var script = (SetRenderingLayer)target;

        // Trouver l'index actuel
        selectedIndex = Mathf.Clamp(script.renderingLayerIndex, 0, layerNames.Length - 1);
    }

    public override void OnInspectorGUI()
    {
        var script = (SetRenderingLayer)target;

        // Sélection du Rendering Layer
        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Rendering Layer", selectedIndex, layerNames);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script, "Change Rendering Layer Index");
            script.renderingLayerIndex = selectedIndex;
            EditorUtility.SetDirty(script);
        }

        // Sélection des LayerMask à affecter
        EditorGUI.BeginChangeCheck();
        int mask = EditorGUILayout.MaskField("Layer(s) concerné(s)", script.targetLayers, InternalEditorUtility.layers);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script, "Change Target Layers");
            script.targetLayers = mask;
            EditorUtility.SetDirty(script);
        }

        if (GUILayout.Button("Appliquer aux enfants"))
        {
            script.ApplyToChildren();
        }
    }
}
