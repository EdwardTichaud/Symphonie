using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SetRenderingLayer))]
public class SetRenderingLayerEditor : Editor
{
    private string[] layerNames;
    private int selectedIndex;

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

        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUILayout.Popup("Rendering Layer", selectedIndex, layerNames);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(script, "Change Rendering Layer Index");
            script.renderingLayerIndex = selectedIndex;
            EditorUtility.SetDirty(script);
        }

        if (GUILayout.Button("Appliquer aux enfants"))
        {
            script.ApplyToChildren();
        }
    }
}
