using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class SelectionFollower : MonoBehaviour
{
    [Tooltip("L'objet qui va suivre la sélection dans la hiérarchie.")]
    public Transform objectToMove;

    private Transform currentSelected;
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    void Update()
    {
        #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // Récupère la sélection actuelle
                    Transform selected = Selection.activeTransform;

                    // S'il y a une sélection valide
                    if (selected != null)
                    {
                        // Détecte un changement de sélection OU modification de position/rotation
                        bool selectionChanged = selected != currentSelected;
                        bool transformChanged = selected.position != lastPosition || selected.rotation != lastRotation;

                        if (selectionChanged || transformChanged)
                        {
                            // Met à jour le suivi
                            if (objectToMove != null)
                            {
                                objectToMove.position = selected.position;
                                objectToMove.rotation = selected.rotation;
                            }

                            // Sauvegarde l'état actuel pour le prochain tour
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
