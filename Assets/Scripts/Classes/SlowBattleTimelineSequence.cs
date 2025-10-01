using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
///     Données sérialisables décrivant une séquence différée (reprise de Performing + repli)
///     à jouer lorsque le <see cref="SlowBattleManager"/> est actif.
///     La structure est volontairement générique afin de couvrir aussi bien
///     les <see cref="MusicalMoveSO"/> que les <see cref="ItemData"/>.
/// </summary>
[System.Serializable]
public class SlowBattleTimelineSequence
{
    [Header("Références de gameplay")]
    public MusicalMoveSO move;
    public ItemData item;
    public CharacterUnit caster;
    public CharacterUnit target;

    [Header("Contexte de déplacement")]
    public Vector3 originPosition;
    public bool requiresReturn;
    public bool stayInPlace;

    [Header("Timelines")] 
    public TimelineAsset performingTimeline;
    public TimelineAsset retreatTimeline;

    [Header("Contrôle de lecture")]
    public bool resumePausedTimeline;

    [Header("Caméra et bindings")]
    public bool useOverlay;
    public GameObject casterAnimatorGO;
    public GameObject performingCameraTarget;
    public GameObject casterCameraTarget;
    public Quaternion initialRotation;
    public float performingCameraDelay;
    public BattleCameraRole performingCameraRole;
    public BattleCameraRole retreatCameraRole;

    [Header("Résolution")]
    public bool wasCritical;

    /// <summary>
    ///     Crée une séquence différée pour un <see cref="MusicalMoveSO"/>.
    /// </summary>
    public static SlowBattleTimelineSequence ForMusicalMove(
        MusicalMoveSO move,
        CharacterUnit caster,
        CharacterUnit target,
        Vector3 originPosition,
        bool useOverlay,
        GameObject casterAnimatorGO,
        GameObject performingCameraTarget,
        GameObject casterCameraTarget,
        Quaternion initialRotation,
        TimelineAsset performingTimeline,
        TimelineAsset retreatTimeline,
        float performingCameraDelay,
        BattleCameraRole performingCameraRole,
        BattleCameraRole retreatCameraRole,
        bool wasCritical,
        bool resumePausedTimeline)
    {
        return new SlowBattleTimelineSequence
        {
            move = move,
            item = null,
            caster = caster,
            target = target,
            originPosition = originPosition,
            requiresReturn = move != null && move.requiresMovement && !move.stayInPlace,
            stayInPlace = move != null && move.stayInPlace,
            performingTimeline = performingTimeline,
            retreatTimeline = retreatTimeline,
            useOverlay = useOverlay,
            casterAnimatorGO = casterAnimatorGO,
            performingCameraTarget = performingCameraTarget,
            casterCameraTarget = casterCameraTarget,
            initialRotation = initialRotation,
            performingCameraDelay = performingCameraDelay,
            performingCameraRole = performingCameraRole,
            retreatCameraRole = retreatCameraRole,
            wasCritical = wasCritical,
            resumePausedTimeline = resumePausedTimeline
        };
    }

    /// <summary>
    ///     Crée une séquence différée pour un <see cref="ItemData"/>.
    /// </summary>
    public static SlowBattleTimelineSequence ForItem(
        ItemData item,
        CharacterUnit caster,
        CharacterUnit target,
        Vector3 originPosition,
        bool useOverlay,
        GameObject casterAnimatorGO,
        GameObject performingCameraTarget,
        GameObject casterCameraTarget,
        Quaternion initialRotation,
        TimelineAsset performingTimeline,
        TimelineAsset retreatTimeline,
        float performingCameraDelay,
        BattleCameraRole performingCameraRole,
        BattleCameraRole retreatCameraRole,
        bool resumePausedTimeline)
    {
        return new SlowBattleTimelineSequence
        {
            move = null,
            item = item,
            caster = caster,
            target = target,
            originPosition = originPosition,
            requiresReturn = item != null && item.requiresMovement && !item.stayInPlace,
            stayInPlace = item != null && item.stayInPlace,
            performingTimeline = performingTimeline,
            retreatTimeline = retreatTimeline,
            useOverlay = useOverlay,
            casterAnimatorGO = casterAnimatorGO,
            performingCameraTarget = performingCameraTarget,
            casterCameraTarget = casterCameraTarget,
            initialRotation = initialRotation,
            performingCameraDelay = performingCameraDelay,
            performingCameraRole = performingCameraRole,
            retreatCameraRole = retreatCameraRole,
            wasCritical = false,
            resumePausedTimeline = resumePausedTimeline
        };
    }
}
