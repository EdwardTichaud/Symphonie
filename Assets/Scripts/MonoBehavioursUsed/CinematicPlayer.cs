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
                    if (step.timelineDefinition != null)
                        yield return PlayDefinitionStep(step.timelineDefinition);
                    else
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

    private IEnumerator PlayDefinitionStep(CinematicDefinitionSO definition)
    {
        if (definition == null || !definition.HasPlayable)
        {
            Debug.LogWarning("[CinematicPlayer] Definition de timeline manquante ou vide.");
            yield break;
        }

        if (definition.directorPrefab != null)
        {
            PlayableDirector directorToPlay = definition.directorPrefab;
            bool destroyAfter = false;

            if (!directorToPlay.gameObject.scene.IsValid())
            {
                directorToPlay = Instantiate(definition.directorPrefab);
                destroyAfter = definition.destroyAfterPlay;
            }

            if (directorToPlay == null)
                yield break;

            directorToPlay.Stop();
            directorToPlay.time = 0d;
            directorToPlay.Evaluate();

            SetTimelineBidings bindings = directorToPlay.GetComponent<SetTimelineBidings>();
            if (bindings != null)
                bindings.ApplyBindings();

            if (TimelineManager.Instance != null)
            {
                TimelineManager.Instance.PlayTimeline(
                    directorToPlay,
                    definition.withFade,
                    definition.interruptMusic,
                    definition.allowSkip,
                    definition.autoRestore,
                    definition.requiresWorldCamera);

                while (TimelineManager.Instance.IsTimelinePlaying)
                    yield return null;
            }
            else
            {
                directorToPlay.Play();
                while (directorToPlay.state == PlayState.Playing)
                    yield return null;
            }

            if (destroyAfter && directorToPlay != null)
                Destroy(directorToPlay.gameObject);

            yield break;
        }

        if (definition.timelineAsset == null)
            yield break;

        if (TimelineManager.Instance == null)
        {
            Debug.LogWarning("[CinematicPlayer] TimelineManager introuvable pour jouer un TimelineAsset.");
            yield break;
        }

        GameObject caster = null;
        if (!string.IsNullOrWhiteSpace(definition.casterTag))
            SceneBindings.TryGetByTag(definition.casterTag, out caster);

        string cameraTag = string.IsNullOrWhiteSpace(definition.cameraTag) ? null : definition.cameraTag;

        TimelineManager.Instance.PlayTimeline(
            definition.timelineAsset,
            caster,
            cameraTag,
            definition.withFade,
            definition.interruptMusic,
            definition.allowSkip,
            definition.autoRestore);

        while (TimelineManager.Instance.IsTimelinePlaying)
            yield return null;
    }
}
