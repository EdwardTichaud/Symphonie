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
        if (playing)
            return;

        if (sequence == null || sequence.steps == null || sequence.steps.Length == 0)
        {
            Debug.LogWarning("[CinematicPlayer] Impossible de jouer : aucune étape définie.", this);
            return;
        }

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        if (sequence == null || sequence.steps == null)
            yield break;

        playing = true;
        foreach (var step in sequence.steps)
        {
            if (step == null)
                continue;

            switch (step.type)
            {
                case CinematicStepType.PlayTimeline:
                    yield return PlayTimelineStep(step.timeline);
                    break;
                case CinematicStepType.Wait:
                    yield return new WaitForSeconds(step.waitDuration);
                    break;
                case CinematicStepType.Dialogue:
                    if (step.dialogue != null)
                    {
                        bool done = false;
                        // Passe le DialogueContainer complet pour bénéficier de la
                        // configuration de position (aléatoire ou fixe)
                        DialogueManager.Instance.PlayDialogue(step.dialogue, () => done = true);
                        while (!done)
                            yield return null;
                    }
                    break;
                case CinematicStepType.Event:
                    step.onEvent?.Invoke();
                    break;
                default:
                    break;
            }
        }
        playing = false;
    }

    private IEnumerator PlayTimelineStep(PlayableDirector director)
    {
        if (director == null)
            yield break;

        if (TimelineManager.Instance != null)
        {
            TimelineManager.Instance.PlayTimeline(director);
            while (TimelineManager.Instance.IsTimelinePlaying)
                yield return null;
        }
        else
        {
            director.Play();
            while (director.state == PlayState.Playing)
                yield return null;
        }
    }
}
