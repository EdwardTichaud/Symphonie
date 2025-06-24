using UnityEngine;
using UnityEditor;

/// <summary>
/// Permet de bloquer la position et/ou la rotation globale de cet objet,
/// ind�pendamment des transformations du parent.
/// </summary>
public class DetachTransform : MonoBehaviour
{
    [Header("Detach Options")]
    [Tooltip("Si activ�, la position globale reste fixe.")]
    public bool detachPosition = true;

    [Tooltip("Si activ�, la rotation globale reste fixe.")]
    public bool detachRotation = true;

    [Header("Fixed Values")]
    [Tooltip("Position globale � maintenir. Ignor� si 'Detach Position' est d�sactiv�.")]
    public Vector3 fixedWorldPosition;

    [Tooltip("Rotation globale � maintenir. Ignor� si 'Detach Rotation' est d�sactiv�.")]
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
            "Ce composant force la position et/ou la rotation globale de l'objet � rester fixe, " +
            "peu importe les transformations de son parent.",
            MessageType.Info
        );
    }
}
#endif
