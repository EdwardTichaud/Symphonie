using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Explosion Profile")]
public class ExplosionProfile : ScriptableObject
{
    [Header("Physique")]
    public float upForce = 8f;
    public float randomSpread = 3f;
    public float torque = 4f;
    public float upwardsModifier = 0.7f;

    [Header("Durées")]
    public float preDelay = 0.15f;    // petit “crack” avant
    public float burstDuration = 0.8f;

    [Header("Shader (optionnel)")]
    public string dissolveParam = "_Dissolve";
    public string offsetParam = "_OffsetAmp";
    public AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve offsetCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float offsetAmplitude = 1.2f;

    [Header("VFX/SFX (optionnels)")]
    public GameObject vfxPrefab;
    public AudioClip sfx;
}
