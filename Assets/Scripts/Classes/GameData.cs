using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Contient les données persistantes du jeu.
/// </summary>
[CreateAssetMenu(fileName = "GameData", menuName = "Symphonie/GameData")]
public class GameData : ScriptableObject
{
    public List<int> defeatedEnemies = new();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
    public string muninName = "Munin"; // Nom de la caméra contrôlée par le joueur
    public bool muninMet; // Succès : indique si le joueur a déjà rencontré Munin

    /// <summary>
    /// Sauvegarde les données dans un fichier JSON situé dans le dossier persistant.
    /// </summary>
    public void SaveToFile(string fileName = "save.json")
    {
        var save = new GameDataSave
        {
            defeatedEnemyIDs = new List<int>(defeatedEnemies),
            squadLevel = squadLevel,
            squadXP = squadXP,
            enemiesDefeatedCount = enemiesDefeatedCount,
            muninName = muninName,
            muninMet = muninMet // Persistance du succès "Munin rencontré"
        };

        string json = JsonUtility.ToJson(save, true);
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, json);
        Debug.Log($"[GameData] Données sauvegardées : {path}");
    }

    /// <summary>
    /// Recharge les données depuis un fichier JSON.
    /// </summary>
    public void LoadFromFile(string fileName = "save.json")
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[GameData] Aucun fichier de sauvegarde trouvé !");
            return;
        }

        string json = File.ReadAllText(path);
        GameDataSave loaded = JsonUtility.FromJson<GameDataSave>(json);

        // Recharge les données
        defeatedEnemies = new List<int>(loaded.defeatedEnemyIDs);
        squadLevel = loaded.squadLevel;
        squadXP = loaded.squadXP;
        enemiesDefeatedCount = loaded.enemiesDefeatedCount;
        muninName = loaded.muninName; // Recharge le nom personnalisé de Munin
        muninMet = loaded.muninMet;   // Recharge le succès "Munin rencontré"
        Debug.Log($"[GameData] Données chargées depuis : {path}");
    }

    /// <summary>
    /// Réinitialise toutes les données du jeu.
    /// </summary>
    public void ResetGameData()
    {
        defeatedEnemies.Clear();
        squadLevel = 0;
        squadXP = 0;
        enemiesDefeatedCount = 0;
        muninName = "Munin"; // Réinitialise le nom de Munin par défaut
        muninMet = false;     // Réinitialise le succès
        Debug.Log("GameData has been reset.");
    }
}
