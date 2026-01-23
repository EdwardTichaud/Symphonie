using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleSceneBindings))]
public class BattleSceneBindingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Auto Bind From Scene"))
            InvokeEditorMethod(target, "AutoBindFromScene");
    }

    private static void InvokeEditorMethod(Object targetObject, string methodName)
    {
        if (targetObject == null)
            return;

        MethodInfo method = targetObject.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            Debug.LogWarning($"[{targetObject.GetType().Name}] Missing method {methodName}.");
            return;
        }

        method.Invoke(targetObject, null);
    }
}
