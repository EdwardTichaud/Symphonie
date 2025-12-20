using System.Collections.Generic;

/// <summary>
/// Classe sérialisable utilisée pour sauvegarder les données du jeu.
/// </summary>
[System.Serializable]
public class GameDataSave
{
    // Version interne de la structure de sauvegarde (permet de gérer les évolutions).
    public int saveVersion = 2;
    public List<int> defeatedEnemyIDs = new();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
    public string muninName; // Nom sauvegardé de Munin
    public bool muninMet;    // Sauvegarde du succès "Munin rencontré"

    // --- Collections persistées (IDs stables) ---
    public List<string> unlockedAchievementIds = new();
    public List<string> knownMoveIds = new();
    public List<string> unlockedSealIds = new();
    public List<string> equippedSealIds = new();
}
