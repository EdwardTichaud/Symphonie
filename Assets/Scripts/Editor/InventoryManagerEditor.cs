using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InventoryManager))]
public class InventoryManagerEditor : Editor
{
    private const string RegisteredSealsPropertyName = "registeredSeals";
    private const string SealPrefix = "Sceau_";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Populate Registered Seals"))
            PopulateRegisteredSeals();

        serializedObject.ApplyModifiedProperties();
    }

    private void PopulateRegisteredSeals()
    {
        var listProperty = serializedObject.FindProperty(RegisteredSealsPropertyName);
        if (listProperty == null)
        {
            Debug.LogWarning("[InventoryManagerEditor] registeredSeals property not found.");
            return;
        }

        Undo.RecordObject(target, "Populate Registered Seals");

        var existing = new HashSet<UnityEngine.Object>();
        for (int i = 0; i < listProperty.arraySize; i++)
        {
            var element = listProperty.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != null)
                existing.Add(element.objectReferenceValue);
        }

        string[] guids = AssetDatabase.FindAssets("t:SceauSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!assetName.StartsWith(SealPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var asset = AssetDatabase.LoadAssetAtPath<SceauSO>(path);
            if (asset == null || existing.Contains(asset))
                continue;

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            listProperty.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            existing.Add(asset);
        }

        EditorUtility.SetDirty(target);
    }

    // Prefix check inline for clarity and to avoid extra allocations.
}
