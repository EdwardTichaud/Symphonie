using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

public class VideoScrubPlayableBehaviour : PlayableBehaviour
{
    public VideoPlayer videoPlayer;
    public float speed = 1f; // Multiplicateur de vitesse

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (videoPlayer == null || videoPlayer.clip == null) return;

        double absoluteTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        double targetTime = absoluteTime * speed;

        if (Mathf.Abs((float)(videoPlayer.time - targetTime)) > 0.033f)
        {
            videoPlayer.time = targetTime;
            videoPlayer.Pause();
            videoPlayer.StepForward();
        }
    }
}
