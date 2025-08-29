using UnityEngine;

/// <summary>
/// Association d'un dialogue à une condition simple sur les <see cref="GameData"/>.
/// Cela permet de déclencher des conversations différentes selon les succès du joueur.
/// </summary>
[System.Serializable]
public class ConditionalDialogue
{
    [Tooltip("Nom du champ booléen dans GameData à vérifier. Laisser vide pour toujours vrai.")]
    public string gameDataBool;

    [Tooltip("Succès requis pour activer ce dialogue (optionnel).")]
    public AchievementSO requiredAchievement;

    [Tooltip("Valeur attendue pour que le dialogue soit sélectionné. S'applique au booléen ou à l'état du succès.")]
    public bool requiredValue = true;

    [Tooltip("Dialogue joué si la condition est remplie.")]
    public DialogueContainer dialogue;

    /// <summary>
    /// Vérifie si la condition associée est satisfaite.
    /// Priorité :
    /// 1) Succès requis via <see cref="AchievementSO"/> ;
    /// 2) Champ booléen dans les <see cref="GameData"/>.
    /// </summary>
    public bool IsConditionMet(GameData data)
    {
        // --- Vérification du succès ---
        // Si un AchievementSO est assigné, on compare son état "unlocked" à la valeur attendue.
        if (requiredAchievement != null)
        {
            return requiredAchievement.unlocked == requiredValue;
        }

        // --- Vérification des GameData ---
        if (data == null)
            return false; // Pas de données de jeu : condition non remplie

        // Aucune condition spécifiée : toujours vrai
        if (string.IsNullOrEmpty(gameDataBool))
            return true;

        // Utilise la réflexion pour lire le champ booléen demandé
        var field = typeof(GameData).GetField(gameDataBool);
        if (field != null && field.FieldType == typeof(bool))
        {
            bool value = (bool)field.GetValue(data);
            return value == requiredValue;
        }

        // Champ introuvable ou type incorrect
        return false;
    }
}
