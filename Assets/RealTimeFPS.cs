using UnityEngine;

/// <summary>
/// Affiche en temps réel le nombre de FPS du jeu.
/// </summary>
public class RealTimeFPS : MonoBehaviour
{
    // Durée de la fenêtre pour la moyenne (en secondes)
    public float refreshRate = 0.5f;

    // Compteurs internes pour calculer les FPS
    private int framesCount = 0;
    private float timePassed = 0f;
    private float fps = 0f;

    void Update()
    {
        // On accumule les frames et le temps non-scalé
        framesCount++;
        timePassed += Time.unscaledDeltaTime;

        // Lorsque la durée de la fenêtre est écoulée, on calcule la moyenne
        if (timePassed >= refreshRate)
        {
            fps = framesCount / timePassed;
            framesCount = 0;
            timePassed = 0f;
        }
    }

    void OnGUI()
    {
        // Style du texte : plus gros pour une meilleure lisibilité
        GUIStyle style = new GUIStyle();
        style.fontSize = 30; // texte agrandi
        style.normal.textColor = Color.white;

        // Affichage simple en haut à gauche de l'écran
        GUI.Label(new Rect(10, 10, 220, 40), $"FPS : {fps:F1}", style);
    }
}
