using UnityEngine;
using UnityEditor;

/// <summary>
/// Maintient la rotation globale fixe, ind�pendamment de la rotation du parent.
/// Permet � l'objet de suivre la position du parent mais pas la rotation.
/// </summary>
public class DetachRotation : MonoBehaviour
{
    [Tooltip("Rotation globale � maintenir (si laiss� vide, la rotation initiale sera utilis�e).")]
    public Quaternion fixedWorldRotation;

    private void Start()
    {
        // Si aucune rotation fix�e manuellement, utilise la rotation initiale au d�marrage
        if (fixedWorldRotation == Quaternion.identity)
        {
            fixedWorldRotation = transform.rotation;
        }
    }

    void LateUpdate()
    {
        // Maintenir la position locale mais forcer la rotation globale
        transform.rotation = fixedWorldRotation;
    }
}

[CustomEditor(typeof(DetachRotation))]
[CanEditMultipleObjects]
public class DetachRotationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Affiche l'inspecteur par d�faut
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ce composant force cet objet � ignorer la rotation de son parent.\nIl suit la position mais garde une rotation globale fixe.",
            MessageType.Info
        );
    }
}
