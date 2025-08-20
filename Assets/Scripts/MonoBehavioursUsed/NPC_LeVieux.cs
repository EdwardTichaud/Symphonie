using UnityEngine;
using UnityEngine.Timeline; // 📽️ Permet d'utiliser des TimelineAsset dans les phases de dialogue
using System.Collections;

/// <summary>
/// PNJ "Le Vieux" : gère plusieurs phases de dialogue en fonction
/// du nombre d'interactions du joueur.
/// </summary>
public class NPC_LeVieux : MonoBehaviour, IInteractable
{
    [Header("Dialogues du PNJ")]
    [Tooltip("Liste des phases de dialogue (0 = première interaction, etc.).")]
    public NPCDialoguePhase[] dialoguePhases; // Phases complètes : timeline + dialogue + options

    // Indice de la prochaine phase à jouer
    private int dialogueStage = 0;

    // --- Implémentation de IInteractable ---
    public GameObject GameObject => gameObject;

    /// <summary>
    /// Déclenche le dialogue correspondant à la phase en cours
    /// si aucune autre interaction n'est active.
    /// </summary>
    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            // Empêche les dialogues simultanés ou les cinématiques concurrentes
            return;
        }

        // Si aucun dialogue n'est configuré, on ne fait rien
        if (dialoguePhases == null || dialoguePhases.Length == 0)
        {
            // Aucun dialogue défini pour ce PNJ
            return;
        }

        // Oriente le PNJ vers le joueur avant de lancer le dialogue
        NPCTurnHandler turnHandler = GetComponent<NPCTurnHandler>();
        if (turnHandler != null)
        {
            // Choix et lecture de l'animation de rotation
            turnHandler.TurnTowardPlayer();
        }

        // Lance le dialogue approprié (le dernier tournera en boucle)
        StartCoroutine(PlayDialogue());
    }

    /// <summary>
    /// Lance la phase de dialogue courante (timeline + dialogue) et gère les enchaînements.
    /// </summary>
    private IEnumerator PlayDialogue()
    {
        // Bloque les autres interactions pendant l'échange.
        EventsManager.Instance.eventInProgress = true;

        bool continueSequence = true; // Permet d'enchaîner plusieurs phases automatiquement.
        while (continueSequence)
        {
            NPCDialoguePhase phase = dialoguePhases[dialogueStage];

            // Joue l'animation d'entrée uniquement si aucune Timeline n'est définie.
            // Lorsque l'on dispose d'une Timeline, celle-ci est censée gérer ses propres animations.
            Animator anim = GetComponent<Animator>();
            if (anim != null && phase.timeline == null)
            {
                // Animation par défaut pour signaler le début de la discussion.
                anim.Play("Dialogue_Start");
            }

            // Lance la Timeline éventuelle via le TimelineLauncher et attend sa fin.
            if (phase.timeline != null && TimelineLauncher.Instance != null)
            {
                // Joue la timeline en ciblant automatiquement ce PNJ
                TimelineLauncher.Instance.PlayTimelineOnCurrentNPC(phase.timeline);

                // Attente de la fin de la Timeline pour assurer la cohérence de la séquence
                while (TimelineLauncher.Instance.IsTimelineActive)
                    yield return null;
            }

            // Démarre le dialogue et attend sa conclusion.
            if (phase.dialogue != null)
            {
                yield return DialogueManager.Instance.StartDialogue(phase.dialogue);
            }

            // Joue l'animation de sortie de dialogue.
            if (anim != null)
                anim.Play("Dialogue_End");

            // Affiche une éventuelle boîte de confirmation à la fin du dialogue
            if (phase.askConfirmation)
            {
                bool done = false; // Attendra que le joueur fasse un choix
                ConfirmationBox.Instance.Show(
                    phase.confirmationText,
                    () => { phase.onYes?.Invoke(); done = true; },
                    () => { phase.onNo?.Invoke(); done = true; }
                );
                while (!done)
                    yield return null;
            }

            // Détermine si l'on doit passer automatiquement à la phase suivante.
            bool wasLast = dialogueStage >= dialoguePhases.Length - 1;
            bool auto = phase.autoProceed;

            // Prépare la phase suivante.
            IncrementDialogueStage();

            // Si auto et qu'il reste des phases, boucle; sinon on sort.
            continueSequence = auto && !wasLast;
        }

        // Libère le verrou d'événement une fois toutes les phases jouées.
        EventsManager.Instance.eventInProgress = false;
    }

    /// <summary>
    /// Avance à la prochaine phase de dialogue.
    /// </summary>
    public void IncrementDialogueStage()
    {
        // Sécurise en cas de tableau vide
        if (dialoguePhases == null || dialoguePhases.Length == 0)
        {
            return; // aucune phase de dialogue à parcourir
        }

        // Incrémente l'indice tant qu'on n'est pas au dernier dialogue
        // Une fois le dernier atteint, on reste dessus pour le répéter
        if (dialogueStage < dialoguePhases.Length - 1)
        {
            dialogueStage++;
        }
        // Sinon, ne rien faire : dialogueStage reste sur le dernier index
    }
}
