using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Reçoit les signaux de la Timeline pour déclencher les dialogues.
/// L'attribut <see cref="ExecuteAlways"/> garantit que ces signaux sont
/// traités même en mode Éditeur afin de faciliter la mise au point des
/// cinématiques sans lancer le jeu.
/// </summary>
[ExecuteAlways]
public class DialogueSignalReceiver : MonoBehaviour
{
    public PlayableDirector timeline;

    // Appelé depuis la Timeline avec des dialogueLines spécifiques
    public void TriggerDialogueAndPause(DialogueContainer dialogueContainer)
    {
        // Utilise le DialogueContainer pour gérer la position de la bulle
        DialogueManager.Instance.PlayDialogue(dialogueContainer, OnDialogueEnded);
        timeline.Pause();
    }

    private void OnDialogueEnded()
    {
        timeline.Resume();
    }

    public void TriggerDialogueNoPause(DialogueContainer dialogueContainer)
    {
        DialogueManager.Instance.PlayDialogue(dialogueContainer);
    }
}
