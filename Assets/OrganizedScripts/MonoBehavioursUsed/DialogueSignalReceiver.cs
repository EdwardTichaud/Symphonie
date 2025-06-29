using UnityEngine;
using UnityEngine.Playables;

public class DialogueSignalReceiver : MonoBehaviour
{
    public PlayableDirector timeline;

    // Appel� depuis la Timeline avec des dialogueLines sp�cifiques
    public void TriggerDialogueAndPause(DialogueContainer dialogueContainer)
    {
        DialogueManager.Instance.PlayDialogue(dialogueContainer.lines, OnDialogueEnded);
        timeline.Pause();
    }

    private void OnDialogueEnded()
    {
        timeline.Resume();
    }

    public void TriggerDialogueNoPause(DialogueContainer dialogueContainer)
    {
        DialogueManager.Instance.PlayDialogue(dialogueContainer.lines);
    }
}
