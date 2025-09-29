using System;
using UnityEngine;

/// <summary>
/// Représente une action proposée au <see cref="SlowBattleManager"/>.
/// Cette classe reste volontairement générique pour permettre d'y rattacher
/// ultérieurement des données plus complexes (compétences, items, scripts de caméra…).
/// </summary>
[Serializable]
public class SlowBattleAction
{
    [Tooltip("Type d'action permettant de guider les comportements par défaut du gestionnaire.")]
    public SlowBattleActionType actionType = SlowBattleActionType.None;

    [Tooltip("Cible principale de l'action (si pertinente).")]
    public CharacterUnit primaryTarget;

    [Tooltip("Libellé purement informatif pour faciliter le débogage dans la console.")]
    public string debugLabel = "Action non décrite";

    [Tooltip("Permet de court-circuiter la phase de prévisualisation pour les actions instantanées.")]
    public bool skipPreview;

    /// <summary>
    /// Données supplémentaires libres (ex : référence vers un move, un item, un script personnalisé).
    /// </summary>
    public ScriptableObject payload;

    /// <summary>
    /// Usine simplifiée pour générer une action de passage de tour.
    /// </summary>
    public static SlowBattleAction CreateSkip(CharacterUnit actor, string reason = null)
    {
        return new SlowBattleAction
        {
            actionType = SlowBattleActionType.Skip,
            primaryTarget = actor,
            debugLabel = string.IsNullOrEmpty(reason) ? "Passage de tour" : reason,
            skipPreview = true
        };
    }
}

/// <summary>
/// Types d'actions basiques supportés par le gestionnaire lent.
/// </summary>
public enum SlowBattleActionType
{
    None,
    Attack,
    Skill,
    Item,
    Defend,
    Skip,
    Custom
}
