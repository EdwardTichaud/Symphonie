using UnityEngine;

/// <summary>
/// Limite le nombre d'images par seconde afin de stabiliser l'expérience utilisateur.
/// Placé sur un GameObject persistant, ce script applique automatiquement la limitation au démarrage.
/// </summary>
public class FrameRateLimiter : MonoBehaviour
{
    // Cadence cible que l'on souhaite imposer.
    // Exposée dans l'inspecteur pour ajuster facilement sans modifier le code.
    [Tooltip("Nombre maximal de frames par seconde que le jeu essaiera de maintenir.")]
    [SerializeField] private int targetFrameRate = 60;

    /// <summary>
    /// Méthode appelée dès l'initialisation du script avant la première frame.
    /// C'est ici que l'on force les paramètres de rendu pour verrouiller les FPS.
    /// </summary>
    private void Awake()
    {
        // Désactive la synchronisation verticale afin que Application.targetFrameRate soit pris en compte.
        // Sans cette ligne, certains appareils ignorent la limitation si la VSync est active.
        QualitySettings.vSyncCount = 0;

        // Applique la cadence d'images souhaitée au moteur Unity.
        Application.targetFrameRate = targetFrameRate;

        // Message de debug pour confirmer que la limitation est en place.
        Debug.Log($"Limitation des FPS activée : {targetFrameRate} fps maximum.");
    }
}
