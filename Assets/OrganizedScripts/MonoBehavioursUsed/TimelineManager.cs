using UnityEngine;
using UnityEngine.Playables;

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
    /// Callback quand une Timeline démarre.
    /// </summary>
    private void OnPlayed(PlayableDirector pd)
    {
        IsTimelinePlaying = true;
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
        }
    }
}
