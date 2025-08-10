#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Caméra d’édition permettant de prévisualiser
/// différents points de vue sans lancer le jeu.
/// Inspirée par Munin, l’observateur silencieux de l’histoire.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("Camera/Edition Camera")]
public class EditionCamera : MonoBehaviour
{
    [Header("Caméras de référence")]
    [Tooltip("Liste des caméras de la scène à prévisualiser.")]
    public Camera[] referenceCameras;

    [Tooltip("Index de la caméra actuellement affichée.")]
    public int currentIndex = 0;

    private Camera self;

    void OnEnable()
    {
        self = GetComponent<Camera>();

        // En mode Play, on masque immédiatement la caméra d’édition
        if (Application.isPlaying)
        {
            if (self != null) self.enabled = false;
            return;
        }

        // En édition, on affiche la caméra et on applique la vue courante
        if (self != null) self.enabled = true;
        ApplyCurrentView();
    }

    void Update()
    {
        // Si le jeu se lance pendant que la caméra est active, on la désactive
        if (Application.isPlaying)
        {
            if (self != null && self.enabled)
                self.enabled = false;
        }
    }

    /// <summary>
    /// Permet de changer de vue via l’inspector.
    /// </summary>
    /// <param name="index">Index de la caméra à afficher</param>
    public void SwitchToCamera(int index)
    {
        if (referenceCameras == null || index < 0 || index >= referenceCameras.Length)
            return;

        currentIndex = index;
        ApplyCurrentView();
    }

    /// <summary>
    /// Copie la position et la rotation de la caméra sélectionnée.
    /// </summary>
    void ApplyCurrentView()
    {
        if (referenceCameras == null || currentIndex < 0 || currentIndex >= referenceCameras.Length)
            return;

        Camera target = referenceCameras[currentIndex];
        if (target == null) return;

        transform.position = target.transform.position;
        transform.rotation = target.transform.rotation;
    }
}
#endif
