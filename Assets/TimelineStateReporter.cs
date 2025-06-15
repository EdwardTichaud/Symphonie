using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class TimelineStateReporter : MonoBehaviour
{
    private PlayableDirector director;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.played += OnPlayed;
        director.stopped += OnStopped;
    }

    private void OnPlayed(PlayableDirector pd)
    {
        TimelineStatus.IsTimelinePlaying = true;
    }

    private void OnStopped(PlayableDirector pd)
    {
        TimelineStatus.IsTimelinePlaying = false;
    }
}
