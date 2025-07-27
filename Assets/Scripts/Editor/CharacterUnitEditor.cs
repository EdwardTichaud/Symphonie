#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterUnit))]
[CanEditMultipleObjects]
public class CharacterUnitEditor : Editor
{
    private void OnEnable()
    {
        // Actualise l'inspecteur en temps réel pendant le Play Mode
        EditorApplication.update += RepaintInspector;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RepaintInspector;
    }

    private void RepaintInspector()
    {
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        // Sécurité : on vérifie que l'objet sérialisé existe toujours
        if (serializedObject == null)
            return;

        serializedObject.Update();

        // Affiche l'inspecteur par défaut
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("--- Valeurs Runtime ---", EditorStyles.boldLabel);

            foreach (var obj in targets)
            {
                if (obj == null) continue;
                var unit = obj as CharacterUnit;
                if (unit == null) continue;

                EditorGUILayout.LabelField(unit.gameObject.name, EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("HP", unit.currentHP);
                EditorGUILayout.FloatField("MP", unit.currentMP);
                EditorGUILayout.FloatField("Rage", unit.currentRage);
                EditorGUILayout.FloatField("Force", unit.currentStrength);
                EditorGUILayout.FloatField("Défense", unit.currentDefense);
                EditorGUILayout.FloatField("Réflexe", unit.currentReflex);
                EditorGUILayout.FloatField("Mobilité", unit.currentMobility);
                EditorGUILayout.FloatField("Puissance", unit.currentPower);
                EditorGUILayout.FloatField("Stabilité", unit.currentStability);
                EditorGUILayout.FloatField("Vitalité", unit.currentVitality);
                EditorGUILayout.FloatField("Sagacité", unit.currentSagacity);
                EditorGUILayout.FloatField("Fatigue", unit.currentFatigue);
                EditorGUILayout.FloatField("Initiative", unit.currentInitiative);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Passez en Play Mode pour voir les valeurs runtime.",
                MessageType.Info
            );
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
