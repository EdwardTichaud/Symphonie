using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MusicalCodexManager))]
public class MusicalCodexManagerEditor : Editor
{
    private const string RegisteredMovesPropertyName = "registeredMoves";
    private const string MusicalMovePrefix = "MusicalMove_";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Populate Registered Moves"))
            PopulateRegisteredMoves();

        serializedObject.ApplyModifiedProperties();
    }

    private void PopulateRegisteredMoves()
    {
        var listProperty = serializedObject.FindProperty(RegisteredMovesPropertyName);
        if (listProperty == null)
        {
            Debug.LogWarning("[MusicalCodexManagerEditor] registeredMoves property not found.");
            return;
        }

        Undo.RecordObject(target, "Populate Registered Moves");

        var existing = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < listProperty.arraySize; i++)
        {
            var element = listProperty.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != null)
                existing.Add(element.objectReferenceValue);
        }

        string[] guids = AssetDatabase.FindAssets("t:MusicalMoveSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!assetName.StartsWith(MusicalMovePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var asset = AssetDatabase.LoadAssetAtPath<MusicalMoveSO>(path);
            if (asset == null || existing.Contains(asset))
                continue;

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            listProperty.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            existing.Add(asset);
        }

        EditorUtility.SetDirty(target);
    }
}
