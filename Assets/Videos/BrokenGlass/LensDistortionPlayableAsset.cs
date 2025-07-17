using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
public class LensDistortionPlayableAsset : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Volume> volume;
    public AnimationCurve distortionCurve = AnimationCurve.Linear(0, 0, 1, 0);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<LensDistortionPlayableBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.volume = volume.Resolve(graph.GetResolver());
        behaviour.distortionCurve = distortionCurve;
        return playable;
    }
}
