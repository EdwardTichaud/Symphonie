using UnityEngine;
using Unity.Cinemachine;

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
    /// Replace l'animation du joueur sur l'état d'attente du monde.
    /// </summary>
    void SetIdleInWorld()
    {
        SceneBindings bindings = ServiceRegistry.GetOrFind<SceneBindings>(FindObjectsInactive.Include);
        GameObject player = bindings != null ? bindings.Player : null;
        if (player == null)
        {
            Debug.LogWarning("[StartManager] Aucun joueur trouvé pour forcer l'animation Idle_World.");
            return;
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("[StartManager] Aucun Animator trouvé sur le joueur.");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[StartManager] Animator sans controller, impossible de jouer Idle_World.");
            return;
        }

        animator.Play("Idle_World");
    }
}
