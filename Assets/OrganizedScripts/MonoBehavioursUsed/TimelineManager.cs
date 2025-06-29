using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    /// <summary>
    /// R�f�rence de la Timeline en cours.
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
    /// Joue une nouvelle Timeline. Arr�te proprement la pr�c�dente.
    /// </summary>
    public void PlayTimeline(PlayableDirector newDirector)
    {
        if (newDirector == null)
        {
            Debug.LogWarning("[TimelineManager] PlayTimeline appel� avec null !");
            return;
        }

        // Arr�te la timeline en cours si elle est diff�rente
        if (currentDirector != null && currentDirector != newDirector && currentDirector.state == PlayState.Playing)
        {
            Debug.Log("[TimelineManager] Arr�t de la Timeline en cours avant de jouer la nouvelle.");
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
    /// Arr�te explicitement la Timeline en cours.
    /// </summary>
    public void StopCurrentTimeline()
    {
        if (currentDirector != null && currentDirector.state == PlayState.Playing)
        {
            currentDirector.Stop();
        }
    }

    /// <summary>
    /// Callback quand une Timeline d�marre.
    /// </summary>
    private void OnPlayed(PlayableDirector pd)
    {
        IsTimelinePlaying = true;
        Debug.Log($"[TimelineManager] Timeline jou�e : {pd.name}");
    }

    /// <summary>
    /// Callback quand une Timeline s'arr�te.
    /// </summary>
    private void OnStopped(PlayableDirector pd)
    {
        if (currentDirector == pd)
        {
            Debug.Log($"[TimelineManager] Timeline stopp�e : {pd.name}");
            IsTimelinePlaying = false;
            currentDirector = null;
        }
    }
}
