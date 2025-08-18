using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Représente une phase complète de dialogue pour un PNJ.
/// Chaque phase peut jouer une Timeline spécifique et enchaîner automatiquement.
/// </summary>
[System.Serializable]
public class NPCDialoguePhase
{
    [Tooltip("Dialogue associé à cette phase.")]
    public DialogueContainer dialogue;

    [Tooltip("Timeline à lancer avant le dialogue (optionnel).")]
    public PlayableDirector timeline;

    [Tooltip("Si activé, enchaîne automatiquement avec la phase suivante.")]
    public bool autoProceed;
}
