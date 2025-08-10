#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspecteur personnalisé pour EditionCamera.
/// Affiche des boutons permettant de basculer rapidement entre les vues.
/// </summary>
[CustomEditor(typeof(EditionCamera))]
public class EditionCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditionCamera editionCam = (EditionCamera)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Prévisualisation rapide", EditorStyles.boldLabel);

        if (editionCam.referenceCameras != null)
        {
            for (int i = 0; i < editionCam.referenceCameras.Length; i++)
            {
                Camera cam = editionCam.referenceCameras[i];
                string name = cam != null ? cam.name : $"Caméra {i}";

                if (GUILayout.Button($"Voir {name}"))
                {
                    editionCam.SwitchToCamera(i);
                }
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("La caméra d’édition est masquée pendant l’exécution.", MessageType.Info);
        }
    }
}
#endif
