using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
        {
            allItems = GameManager.Instance.gameData.allItems;
            inventoryItems = GameManager.Instance.gameData.inventoryItems;
        }

            GameManager.Instance.gameData.inventoryItems.Add(item);
        GameManager.Instance.gameData.inventoryItems.Remove(item);
    [Header("Tous les items du jeu (à assigner dans l’inspecteur ou à charger dynamiquement)")]
    [SerializeField] private List<ItemData> allItems = new();

    [Header("Items actuellement en inventaire")]
    [SerializeField] private List<ItemData> inventoryItems = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Optionnel : charger dynamiquement depuis Resources/Items  
        // allItems = Resources.LoadAll<ItemData>("Items").ToList();
    }

    /// <summary>
    /// Renvoie la liste complète des items disponibles dans le jeu.
    /// </summary>
    public IReadOnlyList<ItemData> GetAllItems() => allItems;

    /// <summary>
    /// Renvoie la liste des items que le joueur possède.
    /// </summary>
    public IReadOnlyList<ItemData> GetInventoryItems() => inventoryItems;

    /// <summary>
    /// Renvoie la liste des items possédés et utilisables en combat.
    /// </summary>
    public List<ItemData> GetUsableItems()
        => inventoryItems.Where(item => item.isUsableInBattle).ToList();

    /// <summary>
    /// Ajoute un item à l'inventaire (uniquement si c'est un item valide du jeu).
    /// </summary>
    public void AddItem(List<ItemData> items)
    {
        foreach (ItemData item in items)
        {
            if (!allItems.Contains(item))
            {
                Debug.LogWarning($"L'item {item.itemName} n'existe pas dans la liste des items du jeu !");
                return;
            }

            inventoryItems.Add(item);
            Debug.Log($"[Inventory] Ajout de l'objet : {item.itemName}");
        }        
    }

    /// <summary>
    /// Utilise un item (et l'enlève de l'inventaire). La cible doit être définie à l'avance.
    /// </summary>
    public void UseItem(ItemData item, CharacterUnit target)
    {
        if (!inventoryItems.Contains(item))
        {
            Debug.LogWarning($"Impossible d'utiliser {item.itemName} : non trouvé en inventaire.");
            return;
        }

        Debug.Log($"[Inventory] Utilisation de l'objet : {item.itemName} sur {target.Data.characterName}");
        item.ApplyEffect(target);
        inventoryItems.Remove(item);
    }

    /// <summary>
    /// Utilise un item sans le consommer (debug ou test).
    /// </summary>
    public void PreviewItemEffect(ItemData item, CharacterUnit target)
    {
        Debug.Log($"[Inventory] Aperçu de l'effet de {item.itemName} sur {target.Data.characterName}");
        item.ApplyEffect(target);
    }
}
