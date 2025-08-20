using UnityEngine;
using UnityEngine.Timeline; // 📽️ Requis pour utiliser TimelineAsset dans les phases de dialogue
using System.Collections;

/// <summary>
/// PNJ "Léandre" : personnage désabusé et apathique,
/// bloqué sur la plateforme et espérant que Lucian trouvera un moyen de tous les libérer.
/// À chaque interaction, il partage son découragement mais rappelle qu'il compte sur Lucian.
/// </summary>
public class NPC_Leandre : MonoBehaviour, IInteractable
{
    [Header("Dialogues du PNJ")]
    [Tooltip("Liste des phases de dialogue (0 = première interaction, etc.).")]
    public NPCDialoguePhase[] dialoguePhases; // Phases complètes : timeline + dialogue + options

    // Indice de la prochaine phase à jouer
    private int dialogueStage = 0;

    // --- Implémentation de IInteractable ---

    /// <summary>
    /// Retourne le GameObject porteur de ce script.
    /// Permet à l'InteractionManager de récupérer la référence du PNJ.
    /// </summary>
    public GameObject GameObject => gameObject;

    /// <summary>
    /// Déclenche le dialogue correspondant à la phase en cours
    /// si aucune autre interaction ou cinématique n'est active.
    /// </summary>
    public void Interact()
    {
        if (DialogueManager.Instance.isOpen || EventsManager.Instance.eventInProgress)
        {
            // Empêche les dialogues simultanés ou les cinématiques concurrentes
            return;
        }

        // Vérifie qu'au moins un dialogue est configuré
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

        // Lance le dialogue approprié
        StartCoroutine(PlayDialogue());
    }

    /// <summary>
    /// Lance la phase de dialogue courante (timeline + dialogue) et gère les enchaînements.
    /// </summary>
    private IEnumerator PlayDialogue()
    {
        // Signale qu'un événement est en cours pour bloquer d'autres interactions
        EventsManager.Instance.eventInProgress = true;

        bool continueSequence = true; // Gestion de l'enchaînement automatique
        while (continueSequence)
        {
            NPCDialoguePhase phase = dialoguePhases[dialogueStage];

            // Animation de début : uniquement si aucune Timeline n'est associée à la phase.
            // Si une Timeline est fournie, elle gère elle-même les animations nécessaires.
            Animator anim = GetComponent<Animator>();
            if (anim != null && phase.timeline == null)
            {
                // Animation par défaut de début de dialogue.
                anim.Play("Dialogue_Start");
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

            // Démarrage du dialogue
            if (phase.dialogue != null)
            {
                yield return DialogueManager.Instance.StartDialogue(phase.dialogue);
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

