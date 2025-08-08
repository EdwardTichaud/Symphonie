using UnityEngine;

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

    private void Awake()
    {
        // Force Unity à viser le framerate défini
        Application.targetFrameRate = targetFrameRate;
    }

    private void Update()
    {
        // Actualise le deltaTime non affecté par le timeScale pour obtenir un calcul d'FPS fiable
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        // Convertit le deltaTime en FPS et l'affiche en haut à gauche de l'écran
        int fps = Mathf.CeilToInt(1f / deltaTime);
        GUI.Label(new Rect(10, 10, 100, 20), fps + " FPS");
    }
}
