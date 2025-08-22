using UnityEditor;
using UnityEngine;

public enum DestinationMode { WorldPosition, TargetByName }

[CreateAssetMenu(fileName = "MoveToConfig", menuName = "Symphonie/Move To Config", order = 10)]
public class MoveToConfigSO : ScriptableObject
{
    [Header("Sujet à déplacer")]
    public string subjectToMoveName;

    [Header("Destination")]
    public DestinationMode destinationMode = DestinationMode.WorldPosition;

    [Tooltip("Utilisé si destinationMode = WorldPosition")]
    public Vector3 worldPosition;

    [Tooltip("Nom du GameObject cible si destinationMode = TargetByName")]
    public string targetName;

    [Tooltip("Décalage appliqué à la destination en espace monde")]
    public Vector3 worldOffset = Vector3.zero;

    [Header("Durée & easing")]
    [Min(0f)] public float duration = 1f;
    [Tooltip("Courbe de 0→1 (null = linéaire)")]
    public AnimationCurve ease = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Options")]
    [Tooltip("True = utilise Time.unscaledDeltaTime")]
    public bool unscaledTime = false;

    [Tooltip("True = déplace en localPosition (si pertinent)")]
    public bool useLocalSpace = false;

    [Tooltip("S’il y a un déplacement en cours, le remplace")]
    public bool interruptCurrentMove = true;
}
#if UNITY_EDITOR

[CustomEditor(typeof(MoveToConfigSO))]
public class MoveToConfigSOEditor : Editor
{
    SerializedProperty subjectToMoveName;
    SerializedProperty destinationMode;
    SerializedProperty worldPosition;
    SerializedProperty targetName;
    SerializedProperty worldOffset;
    SerializedProperty duration;
    SerializedProperty ease;
    SerializedProperty unscaledTime;
    SerializedProperty useLocalSpace;
    SerializedProperty interruptCurrentMove;

    void OnEnable()
    {
        subjectToMoveName   = serializedObject.FindProperty("subjectToMoveName");
        destinationMode     = serializedObject.FindProperty("destinationMode");
        worldPosition       = serializedObject.FindProperty("worldPosition");
        targetName          = serializedObject.FindProperty("targetName");
        worldOffset         = serializedObject.FindProperty("worldOffset");
        duration            = serializedObject.FindProperty("duration");
        ease                = serializedObject.FindProperty("ease");
        unscaledTime        = serializedObject.FindProperty("unscaledTime");
        useLocalSpace       = serializedObject.FindProperty("useLocalSpace");
        interruptCurrentMove= serializedObject.FindProperty("interruptCurrentMove");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Sujet à déplacer", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(subjectToMoveName, new GUIContent("Nom du sujet"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(destinationMode);

        var mode = (DestinationMode)destinationMode.enumValueIndex;
        switch (mode)
        {
            case DestinationMode.WorldPosition:
                EditorGUILayout.PropertyField(worldPosition, new GUIContent("World Position"));
                break;
            case DestinationMode.TargetByName:
                EditorGUILayout.PropertyField(targetName, new GUIContent("Nom du GameObject cible"));
                break;
        }

        EditorGUILayout.PropertyField(worldOffset, new GUIContent("World Offset"));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Durée & easing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(duration);
        EditorGUILayout.PropertyField(ease);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(unscaledTime);
        EditorGUILayout.PropertyField(useLocalSpace);
        EditorGUILayout.PropertyField(interruptCurrentMove);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
