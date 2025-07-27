using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections;

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    /// <summary>
    /// Référence de la Timeline en cours.
    /// </summary>
    private PlayableDirector currentDirector;

    [Header("Director utilisé pour les timelines dynamiques")]
    [SerializeField] private PlayableDirector director;
    private Coroutine followCoroutine;

    /// <summary>
    /// Indique si n'importe quelle Timeline est active (lecture ou pause).
    /// </summary>
    public bool IsTimelineActive => currentDirector != null &&
        (currentDirector.state == PlayState.Playing || currentDirector.state == PlayState.Paused);

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
    /// Joue dynamiquement une TimelineAsset en liant automatiquement les tracks
    /// "Caster" et "Camera" si elles existent.
    /// </summary>
    public void PlayTimeline(TimelineAsset timelineAsset, GameObject caster, string cameraTag)
    {
        if (timelineAsset == null || director == null)
        {
            Debug.LogError("[TimelineManager] TimelineAsset ou Director manquant !");
            return;
        }

        // Stoppe la timeline en cours le cas échéant
        if (currentDirector != null && currentDirector.state == PlayState.Playing)
            currentDirector.Stop();

        director.playableAsset = timelineAsset;
        currentDirector = director;

        GameObject cameraGO = null;
        Transform cameraParent = null;
        GameObject cameraRoot = null;
        if (!string.IsNullOrEmpty(cameraTag))
        {
            cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
            cameraParent = cameraGO != null ? cameraGO.transform.parent : null;
            cameraRoot = cameraParent != null ? cameraParent.gameObject : cameraGO;

            if (caster != null && cameraParent != null)
            {
                cameraParent.position = caster.transform.position;
                cameraParent.rotation = caster.transform.rotation;
            }
        }

        foreach (var output in timelineAsset.outputs)
        {
            string trackName = output.streamName.ToLower();

            if (trackName.Contains("caster") && caster != null)
            {
                BindObjectToTrack(output, caster);
            }
            else if (trackName.Contains("camera") && cameraTag != null)
            {
                BindObjectToTrack(output, cameraRoot);
            }
            else
            {
                Debug.LogWarning($"[TimelineManager] Aucun binding pour la track : {trackName}");
            }
        }

        director.played -= OnPlayed;
        director.stopped -= OnStopped;
        director.played += OnPlayed;
        director.stopped += OnStopped;

        director.Play();

        if (caster != null && cameraParent != null)
        {
            if (followCoroutine != null)
                StopCoroutine(followCoroutine);
            followCoroutine = StartCoroutine(FollowCaster(cameraParent, caster.transform));
        }
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

    /// <summary>
    /// Associe un GameObject ou son Animator à une track donnée.
    /// </summary>
    private void BindObjectToTrack(PlayableBinding output, GameObject go)
    {
        if (output.outputTargetType == typeof(Animator))
        {
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                director.SetGenericBinding(output.sourceObject, animator);
            else
                Debug.LogWarning($"[TimelineManager] Animator manquant sur {go.name}");
        }
        else
        {
            director.SetGenericBinding(output.sourceObject, go);
        }
    }

    private IEnumerator FollowCaster(Transform cameraParent, Transform caster)
    {
        while (director != null && director.state == PlayState.Playing)
        {
            if (cameraParent != null && caster != null)
            {
                cameraParent.position = caster.position;
                cameraParent.rotation = caster.rotation;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Stoppe la timeline courante si elle est en lecture ou en pause.
    /// </summary>
    public void StopTimeline()
    {
        if (currentDirector != null && (currentDirector.state == PlayState.Playing || currentDirector.state == PlayState.Paused))
        {
            currentDirector.Stop();
        }
    }
}
