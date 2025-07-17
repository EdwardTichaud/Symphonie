using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

[System.Serializable]
public class VideoScrubPlayableAsset : PlayableAsset
{
    public ExposedReference<VideoPlayer> videoPlayer;
    public float speed = 1f;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<VideoScrubPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.videoPlayer = videoPlayer.Resolve(graph.GetResolver());
        behaviour.speed = speed;
        return playable;
    }
}
