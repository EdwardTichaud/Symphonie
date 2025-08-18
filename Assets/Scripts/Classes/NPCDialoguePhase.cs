using UnityEngine;
using UnityEngine.Timeline;

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
    public TimelineAsset timeline; // ⚙️ Remplacé PlayableDirector par TimelineAsset pour lecture via TimelineLauncher

    [Tooltip("Si activé, enchaîne automatiquement avec la phase suivante.")]
    public bool autoProceed;
}
