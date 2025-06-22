using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class SelectionFollower : MonoBehaviour
{
    [Tooltip("L'objet qui va suivre la s�lection dans la hi�rarchie.")]
    public Transform objectToMove;

    private Transform currentSelected;
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    void Update()
    {
        #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // R�cup�re la s�lection actuelle
                    Transform selected = Selection.activeTransform;

                    // S'il y a une s�lection valide
                    if (selected != null)
                    {
                        // D�tecte un changement de s�lection OU modification de position/rotation
                        bool selectionChanged = selected != currentSelected;
                        bool transformChanged = selected.position != lastPosition || selected.rotation != lastRotation;

                        if (selectionChanged || transformChanged)
                        {
                            // Met � jour le suivi
                            if (objectToMove != null)
                            {
                                objectToMove.position = selected.position;
                                objectToMove.rotation = selected.rotation;
                            }

                            // Sauvegarde l'�tat actuel pour le prochain tour
                            currentSelected = selected;
                            lastPosition = selected.position;
                            lastRotation = selected.rotation;

                            // Force un repaint pour le SceneView
                            SceneView.RepaintAll();
                        }
                    }
                }
        #endif
    }
}
