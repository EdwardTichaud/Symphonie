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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return; // Aucun joueur dans la scène : rien à réinitialiser.

        Animator playerAnimator = player.GetComponentInChildren<Animator>();
        if (playerAnimator == null)
            return; // L'avatar n'est pas prêt, on ne force pas l'animation.

        CharacterAnimationController controller = player.GetComponentInChildren<CharacterAnimationController>();
        if (controller == null)
        {
            // Ajout à la volée pour conserver la compatibilité des anciens prefabs.
            controller = playerAnimator.GetComponent<CharacterAnimationController>() ?? playerAnimator.gameObject.AddComponent<CharacterAnimationController>();
            controller.RefreshCachedParameters();
        }

        if (controller != null)
        {
            controller.SetBodyState(CharacterAnimationController.BodyAnimationState.IdleWorld, 0f, 0f, forceInstantTransition: true);
        }
        else
        {
            // Sécurité : on retombe sur l'ancien comportement si le contrôleur n'est pas disponible.
            playerAnimator.Play("Idle_World");
        }
    }
}
