using UnityEngine;
using Cinemachine; // Utilisation de CinemachineCamera pour la BattleCamera

/// <summary>
/// Initialise différents paramètres dès le chargement de la scène Commencement.
/// </summary>
public class StartManager : MonoBehaviour
{
    /// <summary>
    /// Point d'entrée : réinitialise les systèmes essentiels avant que le joueur puisse agir.
    /// </summary>
    void Awake()
    {
        ResetTimeScale();       // S'assure que le temps s'écoule normalement.
        ResetCamerasDepth();    // Replace la caméra de bataille au dessus des autres.
        SetIdleInWorld();       // Force le joueur à revenir dans son animation d'attente.
        DialogueManager.Instance.CloseDialogue(); // Ferme tout dialogue résiduel.
    }

    // Update laissé vide pour garder la possibilité d'étendre facilement ce gestionnaire.
    void Update()
    {

    }

    /// <summary>
    /// Remet l'échelle de temps du jeu à sa valeur par défaut.
    /// </summary>
    void ResetTimeScale()
    {
        Debug.Log("Resetting Time Scale to 1");
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Replace la profondeur de la caméra de bataille pour éviter les conflits d'affichage.
    /// </summary>
    void ResetCamerasDepth()
    {
        Debug.Log("Resetting Camera Depths");
        // Le tag "BattleCamera" cible désormais un CinemachineCamera
        CinemachineCamera battleCamera = GameObject.FindGameObjectWithTag("BattleCamera").GetComponentInChildren<CinemachineCamera>();
        battleCamera.depth = 0;
    }

    /// <summary>
    /// Replace l'animation du joueur sur l'état d'attente du monde.
    /// </summary>
    void SetIdleInWorld()
    {
        GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Animator>().Play("Idle_World");
    }
}
