using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using TMPro; // Nécessaire pour utiliser TMP_InputField

[CreateAssetMenu(fileName = "GameData", menuName = "Symphonie/GameData")]
public class GameData : ScriptableObject
{
    public List<int> defeatedEnemies = new List<int>();
    public int squadLevel;
    public int squadXP;
    public int enemiesDefeatedCount;
    public string muninName = "Munin"; // Nom de la caméra contrôlée par le joueur
    // Succès : indique si le joueur a déjà rencontré Munin
    public bool muninMet;

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

    [Header("Nom de Munin")]
    [SerializeField] private GameObject namePanel; // Panneau UI demandant le nom
    [SerializeField] private TMP_InputField nameInputField; // Champ de saisie du nom
    [SerializeField] private string defaultMuninName = "Munin"; // Nom par défaut si aucun n'est choisi

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

    private void Start()
    {
        // Au démarrage, on récupère le nom sauvegardé de Munin s'il existe
        if (PlayerPrefs.HasKey("MuninName"))
        {
            string savedName = PlayerPrefs.GetString("MuninName");
            gameData.muninName = savedName;
        }
        else
        {
            // Aucun nom enregistré : on demande au joueur de choisir
            ShowNamePanel();
        }
    }

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

    public void ResetEnemiesDefeatedCount()
    {
        gameData.enemiesDefeatedCount = 0;
        Debug.Log("[GameManager] Compteur d'ennemis vaincus réinitialisé.");
    }

    /// <summary>
    /// Affiche le panneau permettant au joueur de saisir le nom de Munin.
    /// </summary>
    public void ShowNamePanel()
    {
        if (namePanel == null)
        {
            Debug.LogWarning("[GameManager] Aucun panneau de saisie de nom n'est assigné.");
            return;
        }

        namePanel.SetActive(true);

        // Pré-remplit le champ avec le nom par défaut pour guider le joueur
        if (nameInputField != null)
            nameInputField.text = defaultMuninName;
    }

    /// <summary>
    /// Valide le nom entré par le joueur, le sauvegarde et cache le panneau.
    /// </summary>
    public void ConfirmName()
    {
        string chosenName = nameInputField != null ? nameInputField.text : string.Empty;

        if (string.IsNullOrWhiteSpace(chosenName))
            chosenName = defaultMuninName; // Utilise le nom par défaut si la saisie est vide

        // Met à jour les données de jeu
        gameData.muninName = chosenName;

        // Sauvegarde le choix pour les prochaines sessions
        PlayerPrefs.SetString("MuninName", chosenName);
        PlayerPrefs.Save();

        if (namePanel != null)
            namePanel.SetActive(false); // Cache le panneau après validation
    }
}
