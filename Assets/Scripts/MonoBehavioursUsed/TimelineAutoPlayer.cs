using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class TimelineAutoPlayer : MonoBehaviour
{
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool withFade = true;
    [SerializeField] private bool interruptMusic = true;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private bool autoRestore = true;

    private PlayableDirector director;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
    }

    void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (director == null)
            return;

        if (TimelineManager.Instance != null)
        {
            TimelineManager.Instance.PlayTimeline(director, withFade, interruptMusic, allowSkip, autoRestore);
        }
        else
        {
            director.Play();
        }
    }
}
