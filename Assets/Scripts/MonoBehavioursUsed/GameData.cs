using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Contient les données persistantes du jeu.
/// </summary>
[CreateAssetMenu(fileName = "GameData", menuName = "Symphonie/GameData")]
public class GameData : ScriptableObject
{
    private const int CurrentSaveVersion = 2;

    public List<int> defeatedEnemies = new();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
    public string muninName = "Munin"; // Nom de la caméra contrôlée par le joueur
    public bool muninMet; // Succès : indique si le joueur a déjà rencontré Munin

    [Header("Persistances secondaires (IDs)")]
    [Tooltip("Liste des succès déjà débloqués (IDs) pour la sauvegarde.")]
    public List<string> unlockedAchievementIds = new();
    [Tooltip("Liste des attaques musicales connues (IDs) pour la sauvegarde.")]
    public List<string> knownMoveIds = new();
    [Tooltip("Liste des Sceaux débloqués (IDs) pour la sauvegarde.")]
    public List<string> unlockedSealIds = new();
    [Tooltip("Liste des Sceaux équipés (IDs) pour la sauvegarde.")]
    public List<string> equippedSealIds = new();

    /// <summary>
    /// Sauvegarde les données dans un fichier JSON situé dans le dossier persistant.
    /// </summary>
    public void SaveToFile(string fileName = "save.json")
    {
        SyncPersistentCollectionsFromManagers();

        var save = new GameDataSave
        {
            saveVersion = CurrentSaveVersion,
            defeatedEnemyIDs = new List<int>(defeatedEnemies),
            squadLevel = squadLevel,
            squadXP = squadXP,
            enemiesDefeatedCount = enemiesDefeatedCount,
            muninName = muninName,
            muninMet = muninMet, // Persistance du succès "Munin rencontré"
            unlockedAchievementIds = new List<string>(unlockedAchievementIds),
            knownMoveIds = new List<string>(knownMoveIds),
            unlockedSealIds = new List<string>(unlockedSealIds),
            equippedSealIds = new List<string>(equippedSealIds)
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

        // Les anciennes sauvegardes n'avaient pas ces collections.
        // On ne les applique que si elles existent explicitement.
        if (HasPersistentCollections(loaded))
        {
            unlockedAchievementIds = loaded.unlockedAchievementIds ?? new List<string>();
            knownMoveIds = loaded.knownMoveIds ?? new List<string>();
            unlockedSealIds = loaded.unlockedSealIds ?? new List<string>();
            equippedSealIds = loaded.equippedSealIds ?? new List<string>();

            ApplyPersistentCollectionsToManagers();
        }
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
        unlockedAchievementIds.Clear();
        knownMoveIds.Clear();
        unlockedSealIds.Clear();
        equippedSealIds.Clear();
        Debug.Log("GameData has been reset.");
    }

    /// <summary>
    /// Met à jour les listes d'IDs en lisant l'état des managers runtime.
    /// Cette méthode est appelée avant la sérialisation pour éviter toute divergence.
    /// </summary>
    private void SyncPersistentCollectionsFromManagers()
    {
        if (AchievementManager.Instance != null)
            unlockedAchievementIds = AchievementManager.Instance.BuildUnlockedAchievementIds();

        if (MusicalCodexManager.Instance != null)
            knownMoveIds = MusicalCodexManager.Instance.BuildKnownMoveIds();

        if (InventoryManager.Instance != null)
        {
            unlockedSealIds = InventoryManager.Instance.BuildUnlockedSealIds();
            equippedSealIds = InventoryManager.Instance.BuildEquippedSealIds();
        }
    }

    /// <summary>
    /// Applique les listes d'IDs aux managers déjà chargés en scène.
    /// </summary>
    private void ApplyPersistentCollectionsToManagers()
    {
        AchievementManager.Instance?.ApplyUnlockedAchievementIds(unlockedAchievementIds);
        MusicalCodexManager.Instance?.ApplyKnownMoveIds(knownMoveIds);
        InventoryManager.Instance?.ApplySealIds(unlockedSealIds, equippedSealIds);
    }

    /// <summary>
    /// Appelée par les managers lorsqu'ils apparaissent après le chargement.
    /// </summary>
    public void ApplyAchievementsTo(AchievementManager manager)
    {
        if (manager == null)
            return;

        manager.ApplyUnlockedAchievementIds(unlockedAchievementIds);
    }

    /// <summary>
    /// Appelée par le Codex lorsqu'il apparaît après le chargement.
    /// </summary>
    public void ApplyKnownMovesTo(MusicalCodexManager manager)
    {
        if (manager == null)
            return;

        manager.ApplyKnownMoveIds(knownMoveIds);
    }

    /// <summary>
    /// Appelée par l'inventaire lorsqu'il apparaît après le chargement.
    /// </summary>
    public void ApplySealsTo(InventoryManager manager)
    {
        if (manager == null)
            return;

        manager.ApplySealIds(unlockedSealIds, equippedSealIds);
    }

    private static bool HasPersistentCollections(GameDataSave loaded)
    {
        if (loaded == null)
            return false;

        if (loaded.saveVersion >= CurrentSaveVersion)
            return true;

        return loaded.unlockedAchievementIds != null
            || loaded.knownMoveIds != null
            || loaded.unlockedSealIds != null
            || loaded.equippedSealIds != null;
    }
}
