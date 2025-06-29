using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

[CreateAssetMenu(fileName = "GameData", menuName = "Symphonie/GameData")]
public class GameData : ScriptableObject
{
    public List<int> defeatedEnemies = new List<int>();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;

    public void SaveToFile(string fileName = "save.json")
    {
        var save = new GameDataSave
        {
            defeatedEnemyIDs = new List<int>(defeatedEnemies),
            squadLevel = squadLevel,
            squadXP = squadXP,
            enemiesDefeatedCount = enemiesDefeatedCount
        };

        string json = JsonUtility.ToJson(save, true);
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, json);

        Debug.Log($"[GameData] Données sauvegardées : {path}");
    }

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

        Debug.Log($"[GameData] Données chargées depuis : {path}");
    }

    public void ResetGameData()
    {
        defeatedEnemies.Clear();
        squadLevel = 0;
        squadXP = 0;
        enemiesDefeatedCount = 0;
        Debug.Log("GameData has been reset.");
    }
}

[System.Serializable]
public class GameDataSave
{
    public List<int> defeatedEnemyIDs = new();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
}

public enum GameState
{
    Menu,
    Exploration,
    PartySetup,
    BattleTransition,
    StartBattle,
    Battle,
    Victory,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Données de jeu")]
    public GameData gameData;

    [Header("État du jeu")]
    [SerializeField] private GameState currentState = GameState.Menu;

    public GameState CurrentState
    {
        get => currentState;
        set
        {
            if (currentState == value) return;
            currentState = value;
            Debug.Log($"GameState → {currentState}");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // A implémenter
    //GameManager.Instance.gameData.SaveToFile();      // Sauvegarde
    //GameManager.Instance.gameData.LoadFromFile();     // Chargement

    public void ChangeGameState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Changement d'état du jeu vers : {newState}");
    }

    public void AddXPToSquad(int xp)
    {
        gameData.squadXP += xp;
        Debug.Log($"Added {xp} XP to squad. Total XP: {gameData.squadXP}");
    }

    public void AddSquadLevel(int level)
    {
        gameData.squadLevel += level;
        Debug.Log($"Added {level} to squad level. Total Level: {gameData.squadLevel}");
    }

    public void AddItemToInventory(ItemData item)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] InventoryManager non disponible.");
            return;
        }

        InventoryManager.Instance.AddItem(new List<ItemData> { item });
    }

    public void AddItemsToInventory(List<ItemData> items)
    {
        foreach (var item in items)
        {
            AddItemToInventory(item);
        }
    }

    public void MarkEnemyAsDefeated(int enemyID)
    {
        if (!gameData.defeatedEnemies.Contains(enemyID))
        {
            gameData.defeatedEnemies.Add(enemyID);
            Debug.Log($"[GameManager] Ennemi {enemyID} marqué comme vaincu.");
        }
    }

    public void IncrementEnemiesDefeated()
    {
        gameData.enemiesDefeatedCount++;
        Debug.Log($"[GameManager] Ennemis vaincus : {gameData.enemiesDefeatedCount}");
    }
}
