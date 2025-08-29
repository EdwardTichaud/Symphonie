using UnityEngine;
using UnityEngine.Timeline; // 📽️ Gestion des Timeline dans les phases de dialogue
using System.Collections;

/// <summary>
/// Comportement générique pour un PNJ interactif.
/// Il gère les différentes phases de dialogue et permet d'afficher
/// une LocalInfoBox avec un décalage personnalisable.
/// </summary>
public class NPCBase : MonoBehaviour, IInteractable, ILocalInfoBoxTarget
{
    [Header("Dialogues du PNJ")]
    [Tooltip("Liste des phases de dialogue (0 = première interaction, etc.).")]
    public NPCDialoguePhase[] dialoguePhases; // Phases complètes : timeline + dialogue + options

    [Header("Local InfoBox")]
    [Tooltip("Décalage appliqué à la LocalInfoBox pour ce PNJ.")]
    public Vector3 localInfoBoxOffset; // Offset spécifique choisi dans l'inspecteur

    // Indice de la prochaine phase à jouer
    protected int dialogueStage = 0;

    /// <summary>
    /// Fournit l'offset personnalisé de la LocalInfoBox pour ce PNJ.
    /// </summary>
    public Vector3 LocalInfoBoxOffset => localInfoBoxOffset;

    // --- Implémentation de IInteractable ---

    /// <summary>
    /// Retourne le GameObject porteur de ce script.
    /// </summary>
    public GameObject GameObject => gameObject;

    /// <summary>
    /// Déclenche la phase de dialogue appropriée si aucune autre interaction n'est active.
    /// </summary>
    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            // Empêche les dialogues simultanés ou les cinématiques concurrentes
            return;
        }

        if (dialoguePhases == null || dialoguePhases.Length == 0)
        {
            // Aucun dialogue disponible pour ce PNJ
            return;
        }

        // Oriente le PNJ vers le joueur avant de lancer le dialogue
        NPCTurnHandler turnHandler = GetComponent<NPCTurnHandler>();
        if (turnHandler != null)
        {
            // Choix et lecture de l'animation de rotation
            turnHandler.TurnTowardPlayer();
        }

        // Lance la phase de dialogue courante
        StartCoroutine(PlayDialogue());
    }

    /// <summary>
    /// Lance la phase de dialogue courante (timeline + dialogue) et gère les enchaînements.
    /// </summary>
    protected IEnumerator PlayDialogue()
    {
        // Signale qu'un événement est en cours pour bloquer d'autres interactions
        EventsManager.Instance.eventInProgress = true;

        bool continueSequence = true; // Gestion de l'enchaînement automatique
        while (continueSequence)
        {
            NPCDialoguePhase phase = dialoguePhases[dialogueStage];

            // Animation de début : uniquement si aucune Timeline n'est associée à la phase
            // (si une Timeline est fournie, elle gère elle-même les animations nécessaires)
            Animator anim = GetComponent<Animator>();
            if (anim != null && phase.timeline == null)
            {
                anim.Play("Dialogue_Start"); // Animation par défaut de début de dialogue
            }

            // Lecture de la Timeline associée via le TimelineLauncher centralisé
            if (phase.timeline != null && TimelineLauncher.Instance != null)
            {
                // Joue la timeline en ciblant automatiquement le PNJ courant
                TimelineLauncher.Instance.PlayTimelineOnCurrentNPC(phase.timeline);

                // Attend la fin de la timeline pour enchaîner proprement
                while (TimelineLauncher.Instance.IsTimelineActive)
                    yield return null;
            }

            // Démarrage du dialogue adapté au contexte
            // Récupération du dialogue adapté à la progression et aux succès
            DialogueContainer container = phase.GetDialogue();
            if (container != null)
            {
                yield return DialogueManager.Instance.StartDialogue(container);
            }

            // Animation de fin de dialogue
            if (anim != null)
                anim.Play("Dialogue_End");

            // Boîte de confirmation éventuelle à la fin du dialogue
            if (phase.askConfirmation)
            {
                bool done = false;
                ConfirmationBox.Instance.Show(
                    phase.confirmationText,
                    () => { phase.onYes?.Invoke(); done = true; },
                    () => { phase.onNo?.Invoke(); done = true; }
                );
                while (!done)
                    yield return null; // Attend la réponse du joueur
            }

            // Gestion de la progression
            bool wasLast = dialogueStage >= dialoguePhases.Length - 1;
            bool auto = phase.autoProceed;

            IncrementDialogueStage();

            continueSequence = auto && !wasLast;
        }

        // Libère le verrou d'événement
        EventsManager.Instance.eventInProgress = false;
    }

    /// <summary>
    /// Avance à la phase de dialogue suivante.
    /// </summary>
    public void IncrementDialogueStage()
    {
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
