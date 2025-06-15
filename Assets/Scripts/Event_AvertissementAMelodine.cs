using UnityEngine;
using System.Collections;

public class Event_AvertissementAMelodine : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if(DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            return; // Ne pas interagir si un dialogue est d�j� ouvert ou un �v�nement est en cours
        }
        StartCoroutine(LancerSequencePremiere());
    }

    private IEnumerator LancerSequencePremiere()
    {
        EventsManager.Instance.eventInProgress = true;

        yield return DialogueManager.Instance.StartDialogue(new[]
        {
            new DialogueLine { speakerName = "Lucian", text = "Ceux qui s'arrogent la perfection paieront le prix de l'harmonie bris�e." },
            new DialogueLine { speakerName = "Lucian",    text = "Qui a �crit �a? Je me sens... �trange quand je regarde ces �critures." },
            new DialogueLine { speakerName = "Lucian", text = "Je ferais mieux de continuer." }
        });

        EventsManager.Instance.eventInProgress = false;
    }

    /// IInteractable implementation
    public GameObject GameObject { get; }
    public void IncrementDialogueStage()
    {

    }
}
