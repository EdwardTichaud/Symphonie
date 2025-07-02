using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

public class CinematicPlayer : MonoBehaviour
{
    [Header("Séquence à jouer")]
    public CinematicSequenceSO sequence;

    private bool playing;

    public void Play()
    {
        if (sequence == null || playing)
            return;
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        playing = true;
        foreach (var step in sequence.steps)
        {
            switch (step.type)
            {
                case CinematicStepType.PlayTimeline:
                    if (step.timeline != null)
                    {
                        TimelineManager.Instance.PlayTimeline(step.timeline);
                        while (TimelineManager.Instance.IsTimelinePlaying)
                            yield return null;
                    }
                    break;
                case CinematicStepType.Wait:
                    yield return new WaitForSeconds(step.waitDuration);
                    break;
                case CinematicStepType.Dialogue:
                    if (step.dialogue != null)
                    {
                        bool done = false;
                        DialogueManager.Instance.PlayDialogue(step.dialogue.lines, () => done = true);
                        while (!done)
                            yield return null;
                    }
                    break;
                case CinematicStepType.Event:
                    step.onEvent?.Invoke();
                    break;
            }
        }
        playing = false;
    }
}
