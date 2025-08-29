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

    [Tooltip("Valeur attendue pour que le dialogue soit sélectionné.")]
    public bool requiredValue = true;

    [Tooltip("Dialogue joué si la condition est remplie.")]
    public DialogueContainer dialogue;

    /// <summary>
    /// Vérifie si la condition associée est satisfaite en se basant sur les GameData fournies.
    /// </summary>
    public bool IsConditionMet(GameData data)
    {
        if (data == null)
            return false;

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

        return false; // Champ introuvable ou type incorrect
    }
}
