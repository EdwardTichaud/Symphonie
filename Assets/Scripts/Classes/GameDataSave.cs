using System.Collections.Generic;

/// <summary>
/// Classe sérialisable utilisée pour sauvegarder les données du jeu.
/// </summary>
[System.Serializable]
public class GameDataSave
{
    public List<int> defeatedEnemyIDs = new();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
    public string muninName; // Nom sauvegardé de Munin
    public bool muninMet;    // Sauvegarde du succès "Munin rencontré"
}
