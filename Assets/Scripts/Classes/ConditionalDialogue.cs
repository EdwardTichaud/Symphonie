using UnityEngine;

/// <summary>
/// Association d'un dialogue à un succès requis.
/// Si le succès est débloqué et que le dialogue principal a déjà été joué,
/// ce dialogue alternatif peut être sélectionné.
/// </summary>
[System.Serializable]
public class ConditionalDialogue
{
    [Tooltip("Succès requis pour activer ce dialogue alternatif.")]
    public AchievementSO requiredAchievement;

    [Tooltip("Dialogue joué si la condition est remplie.")]
    public DialogueContainer dialogue;

    /// <summary>
    /// Vérifie si la condition associée est satisfaite.
    /// Le dialogue alternatif est sélectionné uniquement si le dialogue principal
    /// a déjà été joué et si le succès requis est débloqué.
    /// </summary>
    /// <param name="mainDialoguePlayed">Indique si le dialogue principal a été joué au moins une fois.</param>
    public bool IsConditionMet(bool mainDialoguePlayed)
    {
        // Sans succès requis, aucune condition n'est remplie
        if (requiredAchievement == null)
            return false;

        // Le succès doit être débloqué ET le dialogue principal déjà joué
        return mainDialoguePlayed && requiredAchievement.unlocked;
    }
}
