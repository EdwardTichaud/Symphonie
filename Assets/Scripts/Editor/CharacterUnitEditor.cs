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

                EditorGUILayout.LabelField("Attributs fondamentaux", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("Réflexe", unit.currentReflex);
                EditorGUILayout.FloatField("Mobilité", unit.currentMobility);
                EditorGUILayout.FloatField("Vitalité", unit.currentVitality);
                EditorGUILayout.FloatField("Puissance", unit.currentPower);
                EditorGUILayout.FloatField("Stabilité", unit.currentStability);
                EditorGUILayout.FloatField("Sagacité", unit.currentSagacity);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Critical", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("Critical rate", unit.CriticalRate);
                EditorGUILayout.FloatField("Critical chance", unit.CriticalChance);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Paramètres communs", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("HP", unit.currentHP);
                EditorGUILayout.FloatField("Force", unit.currentStrength);
                EditorGUILayout.FloatField("Défense", unit.currentDefense);
                EditorGUILayout.FloatField("Vitesse", unit.currentSpeed);
                EditorGUILayout.FloatField("Portée", unit.currentRange);
                EditorGUILayout.FloatField("Initiative", unit.currentInitiative);
                EditorGUILayout.FloatField("Portée d'interception", unit.currentInterceptionRange);
                EditorGUILayout.FloatField("Chance d'interception", unit.currentInterceptionChance);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Paramètres spécifiques - Lucian", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("Rage", unit.currentRage);
                EditorGUILayout.FloatField("Rage max", unit.Data != null ? unit.Data.maxRage : 0f);
                EditorGUILayout.FloatField("Multiplicateur rage", unit.Data != null ? unit.Data.rageDamageMultiplier : 0f);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Paramètres spécifiques - Thalia", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("Fatigue", unit.currentFatigue);
                EditorGUILayout.FloatField("Fatigue max", unit.Data != null ? unit.Data.maxFatigue : 0f);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("Ressources & jauges", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.FloatField("MP", unit.currentMP);
                EditorGUILayout.FloatField("Jauge musicale", unit.currentMusicalGauge);
                EditorGUILayout.FloatField("ATB", unit.currentATB);
                EditorGUILayout.FloatField("ATB max", unit.ATBMax);
                EditorGUI.indentLevel--;

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
