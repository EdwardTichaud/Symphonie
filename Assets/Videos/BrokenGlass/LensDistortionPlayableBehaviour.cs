using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class LensDistortionPlayableBehaviour : PlayableBehaviour
{
    public Volume volume;
    public AnimationCurve distortionCurve;

    private LensDistortion lensDistortion;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (volume == null) return;

        if (lensDistortion == null)
        {
            volume.profile.TryGet(out lensDistortion);
        }

        if (lensDistortion != null)
        {
            float time = (float)playable.GetTime() / (float)playable.GetDuration();
            float value = distortionCurve.Evaluate(time);
            lensDistortion.intensity.Override(value);
        }
    }
}
