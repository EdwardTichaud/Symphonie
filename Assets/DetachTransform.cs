using UnityEngine;
using UnityEditor;

/// <summary>
/// Permet de bloquer la position et/ou la rotation globale de cet objet,
/// indépendamment des transformations du parent.
/// </summary>
public class DetachTransform : MonoBehaviour
{
    [Header("Detach Options")]
    [Tooltip("Si activé, la position globale reste fixe.")]
    public bool detachPosition = true;

    [Tooltip("Si activé, la rotation globale reste fixe.")]
    public bool detachRotation = true;

    [Header("Fixed Values")]
    [Tooltip("Position globale à maintenir. Ignoré si 'Detach Position' est désactivé.")]
    public Vector3 fixedWorldPosition;

    [Tooltip("Rotation globale à maintenir. Ignoré si 'Detach Rotation' est désactivé.")]
    public Quaternion fixedWorldRotation;

    private void Start()
    {
        if (detachPosition) fixedWorldPosition = transform.position;
        if (detachRotation) fixedWorldRotation = transform.rotation;
    }

    void LateUpdate()
    {
        if (detachPosition) transform.position = fixedWorldPosition;
        if (detachRotation) transform.rotation = fixedWorldRotation;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(DetachTransform))]
[CanEditMultipleObjects]
public class DetachTransformEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Affiche les champs normaux
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ce composant force la position et/ou la rotation globale de l'objet à rester fixe, " +
            "peu importe les transformations de son parent.",
            MessageType.Info
        );
    }
}
#endif
