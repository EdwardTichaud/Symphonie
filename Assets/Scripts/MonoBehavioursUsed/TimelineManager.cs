using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem; // Nécessaire pour manipuler les InputAction du joueur

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    /// <summary>
    /// Référence de la Timeline en cours.
    /// </summary>
    private PlayableDirector currentDirector;

    /// <summary>
    /// Indique si une Timeline est en train de jouer.
    /// </summary>
    public bool IsTimelinePlaying { get; private set; }

    /// <summary>
    /// Active ou désactive l'ensemble des entrées de la map <c>World</c> pendant
    /// l'exécution d'une Timeline. Cela garantit qu'aucune action du joueur ne
    /// vient perturber une cinématique en cours.
    /// </summary>
    /// <param name="enable">True pour réactiver les inputs, false pour les bloquer.</param>
    private void ToggleWorldInputs(bool enable)
    {
        // Vérifie que l'InputsManager et la map World existent avant de manipuler les actions.
        if (InputsManager.Instance == null) return;

        // Récupère la map complète afin de pouvoir l'activer ou la désactiver en un seul appel.
        var worldMap = InputsManager.Instance.playerInputs.World;

        if (enable)
        {
            // Réactivation globale de toutes les actions World une fois la Timeline terminée.
            worldMap.Enable();
        }
        else
        {
            // Désactive immédiatement toutes les actions World pour empêcher toute interaction.
            worldMap.Disable();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Joue une nouvelle Timeline. Arrête proprement la précédente.
    /// </summary>
    public void PlayTimeline(PlayableDirector newDirector)
    {
        if (newDirector == null)
        {
            Debug.LogWarning("[TimelineManager] PlayTimeline appelé avec null !");
            return;
        }

        // Arrête la timeline en cours si elle est différente
        if (currentDirector != null && currentDirector != newDirector && currentDirector.state == PlayState.Playing)
        {
            Debug.Log("[TimelineManager] Arrêt de la Timeline en cours avant de jouer la nouvelle.");
            currentDirector.Stop();
        }

        // Abonnement aux events
        newDirector.played -= OnPlayed;
        newDirector.stopped -= OnStopped;
        newDirector.played += OnPlayed;
        newDirector.stopped += OnStopped;

        currentDirector = newDirector;
        currentDirector.Play();
    }

    /// <summary>
    /// Arrête explicitement la Timeline en cours.
    /// </summary>
    public void StopCurrentTimeline()
    {
        if (currentDirector != null && currentDirector.state == PlayState.Playing)
        {
            currentDirector.Stop();
        }
    }

    /// <summary>
    /// Met en pause la Timeline en cours (appelé par un Signal).
    /// </summary>
    public void PauseCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Playing) return;

        currentDirector.Pause();
        // Stabilise l'état visuel/audio au frame exact de pause.
        currentDirector.Evaluate();

        // On reste en mode "cinématique" pendant la pause :
        // => on NE réactive PAS les déplacements ici.
        // (si tu veux autoriser un prompt/QTE, gère-le dans ton UI/Input dédié)
        Debug.Log("[TimelineManager] PauseCurrentTimeline()");
    }

    /// <summary>
    /// Reprend la Timeline en cours (appelé par une action joueur / QTE / autre).
    /// </summary>
    public void ResumeCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Paused) return;

        currentDirector.Resume();
        Debug.Log("[TimelineManager] ResumeCurrentTimeline()");
    }

    /// <summary>
    /// Callback quand une Timeline démarre.
    /// </summary>
    private void OnPlayed(PlayableDirector pd)
    {
        IsTimelinePlaying = true;
        // Bloque immédiatement toutes les actions "World" pendant l'exécution de la timeline.
        ToggleWorldInputs(false);
        // Désactive le CameraController pour laisser la Timeline contrôler totalement la caméra
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = false;
        Debug.Log($"[TimelineManager] Timeline jouée : {pd.name}");
    }

    /// <summary>
    /// Callback quand une Timeline s'arrête.
    /// </summary>
    private void OnStopped(PlayableDirector pd)
    {
        if (currentDirector == pd)
        {
            Debug.Log($"[TimelineManager] Timeline stoppée : {pd.name}");
            IsTimelinePlaying = false;
            currentDirector = null;
            // La timeline est terminée : on redonne le contrôle de Lucian en réactivant les inputs "World".
            ToggleWorldInputs(true);
            // Réactive le CameraController pour rendre la main après la cinématique
            if (CameraController.Instance != null)
                CameraController.Instance.enabled = true;
        }
    }
}
