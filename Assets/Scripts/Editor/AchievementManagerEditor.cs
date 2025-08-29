#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspecteur personnalisé pour <see cref="AchievementManager"/>.
/// Affiche en permanence les listes de succès disponibles et débloqués
/// et quelques informations utiles pendant le Play Mode.
/// </summary>
[CustomEditor(typeof(AchievementManager))]
public class AchievementManagerEditor : Editor
{
    private void OnEnable()
    {
        // Actualise l'inspecteur en temps réel pendant le Play Mode.
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
        // Sécurité : on vérifie que l'objet sérialisé existe toujours.
        if (serializedObject == null)
            return;

        serializedObject.Update();

        // Affiche l'inspecteur par défaut (listes des succès).
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("--- Informations ---", EditorStyles.boldLabel);

        // Affiche le nombre de succès restants et débloqués pour un aperçu rapide.
        AchievementManager manager = (AchievementManager)target;
        EditorGUILayout.LabelField("Succès disponibles", manager.achievements.Count.ToString());
        EditorGUILayout.LabelField("Succès débloqués", manager.unlockedAchievements.Count.ToString());

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
