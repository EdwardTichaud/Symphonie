using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Nécessaire pour utiliser TMP_InputField
using UnityEngine.SceneManagement; // Pour charger le menu principal lors d'un game over

/// <summary>
/// États possibles du déroulement du jeu.
/// </summary>
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

/// <summary>
/// Gère les transitions d'états et l'accès aux données de jeu.
/// </summary>
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

    [Header("Checkpoint")]
    [SerializeField] private bool hasCheckpoint = false;
    [SerializeField] private Vector3 lastCheckpointPosition;
    [SerializeField] private Vector3 lastCheckpointEuler;

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

        EnsureGameData();
    }

    private void EnsureGameData()
    {
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, creation d'une instance runtime.");
            gameData = ScriptableObject.CreateInstance<GameData>();
            return;
        }

        gameData = Instantiate(gameData);
    }

    private void Start()
    {
        // Au démarrage, on récupère le nom sauvegardé de Munin s'il existe
        if (MuninNameStore.HasSavedName())
        {
            gameData.muninName = MuninNameStore.GetNameFromPrefs(defaultMuninName);
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
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, ajout d'XP ignore.");
            return;
        }

        gameData.squadXP += xp;
        Debug.Log($"Added {xp} XP to squad. Total XP: {gameData.squadXP}");
    }

    public void AddSquadLevel(int level)
    {
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, ajout de niveau ignore.");
            return;
        }

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
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, ennemi non enregistre.");
            return;
        }

        if (!gameData.defeatedEnemies.Contains(enemyID))
        {
            gameData.defeatedEnemies.Add(enemyID);
            Debug.Log($"[GameManager] Ennemi {enemyID} marqué comme vaincu.");
        }
    }

    public void IncrementEnemiesDefeated()
    {
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, compteur non incremente.");
            return;
        }

        gameData.enemiesDefeatedCount++;
        Debug.Log($"[GameManager] Ennemis vaincus : {gameData.enemiesDefeatedCount}");
    }

    public void ResetEnemiesDefeatedCount()
    {
        if (gameData == null)
        {
            Debug.LogWarning("[GameManager] GameData manquant, compteur non reinitialise.");
            return;
        }

        gameData.enemiesDefeatedCount = 0;
        Debug.Log("[GameManager] Compteur d'ennemis vaincus réinitialisé.");
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("[GameManager] Checkpoint null, mise a jour ignoree.");
            return;
        }

        lastCheckpointPosition = checkpoint.position;
        lastCheckpointEuler = checkpoint.rotation.eulerAngles;
        hasCheckpoint = true;

        Debug.Log($"[GameManager] Checkpoint mis a jour : {checkpoint.name}");
    }

    public void EnsureCheckpointInitialized(Transform fallbackTransform)
    {
        if (hasCheckpoint || fallbackTransform == null)
            return;

        lastCheckpointPosition = fallbackTransform.position;
        lastCheckpointEuler = fallbackTransform.rotation.eulerAngles;
        hasCheckpoint = true;
    }

    public bool RespawnPlayerAtCheckpoint(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[GameManager] Joueur introuvable pour le respawn.");
            return false;
        }

        if (!TryGetLastCheckpoint(out Vector3 position, out Quaternion rotation))
        {
            Debug.LogWarning("[GameManager] Aucun checkpoint disponible pour le respawn.");
            return false;
        }

        var controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(position, rotation);

        if (controller != null)
            controller.enabled = true;

        return true;
    }

    private bool TryGetLastCheckpoint(out Vector3 position, out Quaternion rotation)
    {
        if (!hasCheckpoint)
        {
            position = default;
            rotation = default;
            return false;
        }

        position = lastCheckpointPosition;
        rotation = Quaternion.Euler(lastCheckpointEuler);
        return true;
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

        MuninNameStore.SetName(chosenName, defaultMuninName);

        if (namePanel != null)
            namePanel.SetActive(false); // Cache le panneau après validation
    }

    /// <summary>
    /// Déclenche un game over et retourne au menu principal.
    /// </summary>
    public void TriggerGameOver()
    {
        // Mise à jour de l'état du jeu
        ChangeGameState(GameState.GameOver);
        Debug.Log("[GameManager] Game Over déclenché, retour au menu principal.");

        // Restaure la vitesse du temps au cas où elle aurait été modifiée
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Charge la scène du menu principal
        SceneManager.LoadScene("MainMenu");
    }
}
