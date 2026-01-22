using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BattleEventTrigger.ThresholdData))]
public class BattleEventTriggerThresholdDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new Rect(position.x, position.y, position.width, lineHeight);

        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        lineRect.y += lineHeight + spacing;
        SerializedProperty categoryProp = property.FindPropertyRelative("category");
        EditorGUI.PropertyField(lineRect, categoryProp);

        bool showHpRatio = ShouldShowHpRatio(categoryProp);
        if (showHpRatio)
        {
            lineRect.y += lineHeight + spacing;
            SerializedProperty hpProp = property.FindPropertyRelative("hpRatio");
            EditorGUI.PropertyField(lineRect, hpProp);
        }

        lineRect.y += lineHeight + spacing;
        SerializedProperty delayModeProp = property.FindPropertyRelative("triggerDelayMode");
        EditorGUI.PropertyField(lineRect, delayModeProp);

        bool showTriggerDelay = delayModeProp != null
            && delayModeProp.propertyType == SerializedPropertyType.Enum
            && delayModeProp.enumNames[delayModeProp.enumValueIndex] == "DelaySeconds";
        if (showTriggerDelay)
        {
            lineRect.y += lineHeight + spacing;
            SerializedProperty delaySecondsProp = property.FindPropertyRelative("triggerDelaySeconds");
            EditorGUI.PropertyField(lineRect, delaySecondsProp);
        }

        lineRect.y += lineHeight + spacing;
        SerializedProperty motifProp = property.FindPropertyRelative("cameraMotif");
        EditorGUI.PropertyField(lineRect, motifProp);

        lineRect.y += lineHeight + spacing;
        SerializedProperty animProp = property.FindPropertyRelative("animationClip");
        EditorGUI.PropertyField(lineRect, animProp);

        lineRect.y += lineHeight + spacing;
        SerializedProperty audioProp = property.FindPropertyRelative("audioClip");
        EditorGUI.PropertyField(lineRect, audioProp);

        lineRect.y += lineHeight + spacing;
        SerializedProperty timelineProp = property.FindPropertyRelative("timeline");
        EditorGUI.PropertyField(lineRect, timelineProp);

        lineRect.y += lineHeight + spacing;
        SerializedProperty unlockProp = property.FindPropertyRelative("unlockMotifAfterAnimation");
        EditorGUI.PropertyField(lineRect, unlockProp);

        bool showUnlockDelay = unlockProp != null && !unlockProp.boolValue;
        if (showUnlockDelay)
        {
            lineRect.y += lineHeight + spacing;
            SerializedProperty unlockDelayProp = property.FindPropertyRelative("unlockMotifDelay");
            EditorGUI.PropertyField(lineRect, unlockDelayProp);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return lineHeight;

        SerializedProperty categoryProp = property.FindPropertyRelative("category");
        bool showHpRatio = ShouldShowHpRatio(categoryProp);
        SerializedProperty delayModeProp = property.FindPropertyRelative("triggerDelayMode");
        bool showTriggerDelay = delayModeProp != null
            && delayModeProp.propertyType == SerializedPropertyType.Enum
            && delayModeProp.enumNames[delayModeProp.enumValueIndex] == "DelaySeconds";
        SerializedProperty unlockProp = property.FindPropertyRelative("unlockMotifAfterAnimation");
        bool showUnlockDelay = unlockProp != null && !unlockProp.boolValue;

        int lineCount = 1; // foldout
        lineCount += 7; // category + triggerDelayMode + cameraMotif + animationClip + audioClip + timeline + unlockMotifAfterAnimation
        if (showHpRatio)
            lineCount += 1;
        if (showTriggerDelay)
            lineCount += 1;
        if (showUnlockDelay)
            lineCount += 1;

        return (lineHeight * lineCount) + (spacing * (lineCount - 1));
    }

    private static bool ShouldShowHpRatio(SerializedProperty categoryProp)
    {
        if (categoryProp == null || categoryProp.propertyType != SerializedPropertyType.Enum)
            return true;

        string currentName = categoryProp.enumNames[categoryProp.enumValueIndex];
        return currentName != "LastStandUnit"
            && currentName != "LastStandEnemy"
            && currentName != "LastStandAllUnits";
    }
}
