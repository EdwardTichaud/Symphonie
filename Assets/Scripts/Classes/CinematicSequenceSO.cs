using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;

public enum CinematicStepType
{
    PlayTimeline,
    Wait,
    Dialogue,
    Event
}

[System.Serializable]
public class CinematicStep
{
    public CinematicStepType type;

    [Header("Timeline")]
    public PlayableDirector timeline;
    public CinematicDefinitionSO timelineDefinition;

    [Header("Temps d'attente")]
    public float waitDuration = 1f;

    [Header("Dialogue")]
    public DialogueContainer dialogue;

    [Header("Evènement")]
    public UnityEvent onEvent;
}

[CreateAssetMenu(menuName = "Symphonie/Cinematic Sequence")]
public class CinematicSequenceSO : ScriptableObject
{
    public CinematicStep[] steps;
}
