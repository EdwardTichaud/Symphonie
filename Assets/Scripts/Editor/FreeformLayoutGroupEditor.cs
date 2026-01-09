#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(FreeformLayoutGroup))]
[CanEditMultipleObjects]
public class FreeformLayoutGroupEditor : Editor
{
    private ReorderableList entryList;
    private SerializedProperty editModeProp;
    private SerializedProperty useScaleForSizeProp;
    private SerializedProperty entriesProp;
    private SerializedProperty gridSpacingProp;
    private SerializedProperty gridColumnsProp;
    private SerializedObject primarySerializedObject;
    private SerializedProperty primaryEntriesProp;
    private SerializedProperty primarySlotPositionsProp;

    private void OnEnable()
    {
        editModeProp = serializedObject.FindProperty("editMode");
        useScaleForSizeProp = serializedObject.FindProperty("useScaleForSize");
        entriesProp = serializedObject.FindProperty("entries");
        gridSpacingProp = serializedObject.FindProperty("gridSpacing");
        gridColumnsProp = serializedObject.FindProperty("gridColumns");

        primarySerializedObject = new SerializedObject(targets[0]);
        primaryEntriesProp = primarySerializedObject.FindProperty("entries");
        primarySlotPositionsProp = primarySerializedObject.FindProperty("slotPositions");

        entryList = new ReorderableList(primarySerializedObject, primaryEntriesProp, true, true, false, true);
        entryList.elementHeight = (EditorGUIUtility.singleLineHeight * 2f) + 8f;
        entryList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Layout Entries");
        };
        entryList.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = primaryEntriesProp.GetArrayElementAtIndex(index);
            SerializedProperty rectProp = element.FindPropertyRelative("rect");
            SerializedProperty posProp = element.FindPropertyRelative("anchoredPosition");
            SerializedProperty sizeProp = element.FindPropertyRelative("sizeDelta");
            SerializedProperty sizeInitProp = element.FindPropertyRelative("sizeInitialized");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float padding = 2f;
            Rect line1 = new Rect(rect.x, rect.y + padding, rect.width, lineHeight);
            Rect line2 = new Rect(rect.x, rect.y + padding + lineHeight + 4f, rect.width, lineHeight);
            Rect rectField = new Rect(line1.x, line1.y, line1.width * 0.55f, lineHeight);
            Rect posField = new Rect(line1.x + line1.width * 0.55f + 6f, line1.y, line1.width * 0.45f - 6f, lineHeight);

            EditorGUI.PropertyField(rectField, rectProp, GUIContent.none);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(posField, posProp, GUIContent.none);
            if (EditorGUI.EndChangeCheck() && primarySlotPositionsProp != null && primarySlotPositionsProp.arraySize > index)
                primarySlotPositionsProp.GetArrayElementAtIndex(index).vector2Value = posProp.vector2Value;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(line2, sizeProp, new GUIContent("Size"));
            if (EditorGUI.EndChangeCheck() && sizeInitProp != null)
            {
                sizeInitProp.boolValue = true;
                if (rectProp.objectReferenceValue is RectTransform rectTransform)
                {
                    Undo.RecordObject(rectTransform, "Change Layout Entry Size");
                    var group = primarySerializedObject.targetObject as FreeformLayoutGroup;
                    if (group == null || !group.UseScaleForSize)
                        rectTransform.sizeDelta = sizeProp.vector2Value;
                    EditorUtility.SetDirty(rectTransform);
                }
                if (primarySerializedObject.targetObject is FreeformLayoutGroup layoutGroup)
                    layoutGroup.ApplySizes();
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox(
                "Multiple selection detected. Entry list edits apply only to the first selected object.",
                MessageType.Info);
        }

        EditorGUILayout.PropertyField(editModeProp);
        if (editModeProp.boolValue)
        {
            EditorGUILayout.HelpBox(
                "Edit mode enabled. Move RectTransforms in the scene view, then capture the layout.",
                MessageType.Info);
        }

        bool useScaleToggled = false;
        bool useScaleEnabled = false;
        if (useScaleForSizeProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(useScaleForSizeProp, new GUIContent("Use Scale For Size"));
            if (EditorGUI.EndChangeCheck())
            {
                useScaleToggled = true;
                useScaleEnabled = useScaleForSizeProp.boolValue;
            }
        }

        primarySerializedObject.Update();
        entryList.DoLayoutList();
        primarySerializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Helper", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gridSpacingProp);
        EditorGUILayout.PropertyField(gridColumnsProp);

        EditorGUILayout.Space();
        DrawActionButtons();

        serializedObject.ApplyModifiedProperties();
        if (useScaleToggled)
            HandleUseScaleToggle(useScaleEnabled);
    }

    private void HandleUseScaleToggle(bool enabled)
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            if (group == null)
                continue;

            Undo.RecordObject(group, "Toggle Use Scale For Size");
            if (enabled)
                group.CaptureReferenceSizes();
            else
                group.BakeScaleToSize();

            group.ApplySizes();
            EditorUtility.SetDirty(group);
        }
    }

    private void DrawActionButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected"))
                AddSelectedEntries();
            if (GUILayout.Button("Add Children"))
                AddChildrenEntries();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Layout"))
                CaptureLayout();
            if (GUILayout.Button("Apply Layout"))
                ApplyLayout();
            if (GUILayout.Button("Apply Size"))
                ApplySize();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Arrange Grid"))
                AutoArrangeGrid();
            if (GUILayout.Button("Remove Missing"))
                RemoveMissingEntries();
        }
    }

    private void AddSelectedEntries()
    {
        RectTransform[] selection = Selection.GetFiltered<RectTransform>(SelectionMode.Editable);
        if (selection == null || selection.Length == 0)
            return;

        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Add Layout Entry");
            foreach (RectTransform rect in selection)
            {
                if (rect == null || !rect.IsChildOf(group.transform))
                    continue;
                group.AddEntry(rect);
            }
            EditorUtility.SetDirty(group);
        }
    }

    private void AddChildrenEntries()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Add Layout Entries");
            foreach (Transform child in group.transform)
            {
                if (child is RectTransform rect)
                    group.AddEntry(rect);
            }
            EditorUtility.SetDirty(group);
        }
    }

    private void CaptureLayout()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Capture Layout");
            group.CaptureLayout();
            EditorUtility.SetDirty(group);
        }
    }

    private void ApplyLayout()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Apply Layout");
            group.ApplyLayout();
            EditorUtility.SetDirty(group);
        }
    }

    private void ApplySize()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Apply Size");
            group.ApplySizes();
            EditorUtility.SetDirty(group);
        }
    }

    private void AutoArrangeGrid()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Auto Arrange Grid");
            group.AutoArrangeGrid();
            EditorUtility.SetDirty(group);
        }
    }

    private void RemoveMissingEntries()
    {
        foreach (FreeformLayoutGroup group in targets)
        {
            Undo.RecordObject(group, "Remove Missing Entries");
            group.RemoveMissingEntries();
            EditorUtility.SetDirty(group);
        }
    }
}
#endif
