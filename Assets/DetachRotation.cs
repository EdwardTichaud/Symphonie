using UnityEngine;
using UnityEditor;

/// <summary>
/// Maintient la rotation globale fixe, indépendamment de la rotation du parent.
/// Permet à l'objet de suivre la position du parent mais pas la rotation.
/// </summary>
public class DetachRotation : MonoBehaviour
{
    [Tooltip("Rotation globale à maintenir (si laissé vide, la rotation initiale sera utilisée).")]
    public Quaternion fixedWorldRotation;

    private void Start()
    {
        // Si aucune rotation fixée manuellement, utilise la rotation initiale au démarrage
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
        // Affiche l'inspecteur par défaut
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ce composant force cet objet à ignorer la rotation de son parent.\nIl suit la position mais garde une rotation globale fixe.",
            MessageType.Info
        );
    }
}
