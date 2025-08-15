using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestionnaire simple de performance.
/// - Fixe un framerate cible pour améliorer la fluidité ressentie.
/// - Affiche le nombre d'images par seconde pour aider au diagnostic.
/// </summary>
[DisallowMultipleComponent]
public class PerformanceManager : MonoBehaviour
{
    [Tooltip("Framerate souhaité en mode jeu. 60 par défaut.")]
    public int targetFrameRate = 60;

    // Moyenne glissante du temps entre deux frames (utilisé pour calculer un FPS stable)
    private float deltaTime = 0f;

    // Référence facultative vers un composant UI Text pour afficher les FPS
    [Tooltip("Texte UI où afficher les FPS. Laisser vide pour ne rien afficher.")]
    public Text fpsText;

    private void Awake()
    {
        // Force Unity à viser le framerate défini
        Application.targetFrameRate = targetFrameRate;
    }

    private void Update()
    {
        // Actualise le deltaTime non affecté par le timeScale pour obtenir un calcul d'FPS fiable
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // Affiche les FPS via un élément UI plus léger que OnGUI
        if (fpsText != null)
        {
            int fps = Mathf.CeilToInt(1f / deltaTime);
            fpsText.text = fps + " FPS";
        }
    }
}
