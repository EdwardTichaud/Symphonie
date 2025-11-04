using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Combat/Animation Groups", fileName = "CombatAnimationGroups")]
public class CombatAnimationGroups : ScriptableObject
{
    [Header("Idle (1D: IdleVar)")]
    public AnimationClip[] idleVariations = new AnimationClip[3];

    [Header("Move (2D: SpeedX,SpeedY)  L/R/F/B")]
    public AnimationClip strafeL;
    public AnimationClip strafeR;
    public AnimationClip forwardShuffle;
    public AnimationClip backShuffle;

    [Header("Approach (1D: DistanceNorm)")]
    public AnimationClip walkApproach;
    public AnimationClip runApproach;
    public AnimationClip shortLunge;

    [Header("Retreat (1D: DistanceNorm)")]
    public AnimationClip walkRetreat;
    public AnimationClip runRetreat;
    public AnimationClip shortRetreatLunge;

    [Header("Hit React (Directional 2D: F/B/L/R)")]
    public AnimationClip hitF_small;
    public AnimationClip hitB_small;
    public AnimationClip hitL_small;
    public AnimationClip hitR_small;

    [Header("Evade (SSM)")]
    public AnimationClip evadeF;
    public AnimationClip evadeB;
    public AnimationClip evadeL;
    public AnimationClip evadeR;

    [Header("Cast (1D: CastIntensity)")]
    public AnimationClip castLow;
    public AnimationClip castMid;
    public AnimationClip castMax;

    [Header("Item Use")]
    public AnimationClip itemUse;

    [Header("Attacks A (1D: AttackStyle)")]
    public AnimationClip atk_light_A;
    public AnimationClip atk_heavy_A;
    public AnimationClip atk_thrust_A;
    public AnimationClip atk_cleave_A;
    public AnimationClip atk_projectile_start;

    [Header("Attacks B (1D: AttackStyle)")]
    public AnimationClip atk_light_B;
    public AnimationClip atk_heavy_B;
    public AnimationClip atk_finisher_B;
    public AnimationClip atk_projectile_release;

    [Header("KO / GetUp / Death")]
    public AnimationClip knockdown;
    public AnimationClip getUp;
    public AnimationClip death;

    [Header("Turn In Place")]
    public AnimationClip turn90L;
    public AnimationClip turn90R;
    public AnimationClip turn180;
}
