using UnityEngine;

/// <summary>
/// Récepteur de Signal utilisé par les timelines Performing pour indiquer au <see cref="SlowBattleManager"/>
/// qu'elles doivent être suspendues jusqu'à la fin du tour en cours. Le Signal appelle simplement
/// <see cref="PauseTimelineForSlowTurn"/> depuis la timeline.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterUnit))]
public class SlowBattleTimelineSignalReceiver : MonoBehaviour
{
    private CharacterUnit owner;

    private void Awake()
    {
        owner = GetComponent<CharacterUnit>();
    }

    /// <summary>
    /// Appelé par un Signal d'une timeline Performing pour la mettre en pause le temps que le tour
    /// se termine (mode de combat lent). On délègue la mise en pause au <see cref="BattleTimelineManager"/>
    /// puis on enregistre l'information auprès du <see cref="SlowBattleManager"/> afin qu'il reprenne
    /// la lecture au moment opportun.
    /// </summary>
    public void PauseTimelineForSlowTurn()
    {
        if (owner == null)
            return;

        if (BattleTimelineManager.Instance == null || SlowBattleManager.Instance == null)
            return;

        BattleTimelineManager.Instance.PauseCasterTimeline(owner);
        SlowBattleManager.Instance.NotifyPerformingTimelinePaused(owner);
    }
}
