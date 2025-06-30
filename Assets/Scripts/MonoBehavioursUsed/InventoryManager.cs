using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

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

        if (item.cameraPathPrefab != null && target != null
            && NewBattleManager.Instance != null
            && NewBattleManager.Instance.currentBattleState != BattleState.None)
        {
            GameObject pathGO = Instantiate(item.cameraPathPrefab, target.transform.position, target.transform.rotation, target.transform);
            CameraPath camPath = pathGO.GetComponent<CameraPath>();
            if (camPath != null)
            {
                CameraController.Instance.StartPathFollow(camPath, target.transform, alignImmediately: false);
                Destroy(pathGO, camPath.GetTotalDuration() + 0.5f);
            }
            else
            {
                Debug.LogWarning("[InventoryManager] CameraPath component manquant sur le prefab de l'item.");
            }
        }

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

    public void ApplyBuff(CharacterUnit target, BuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (target == null || stat == BuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat(target, stat);
        float value = isPercentage ? baseValue * amount / 100f : amount;
        StartCoroutine(ApplyStatModifier(target, stat, value, duration));
    }

    public void ApplyDebuff(CharacterUnit target, DebuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (target == null || stat == DebuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat(target, (BuffStatType)stat);
        float value = isPercentage ? baseValue * amount / 100f : amount;
        StartCoroutine(ApplyStatModifier(target, (BuffStatType)stat, -value, duration));
    }

    private IEnumerator ApplyStatModifier(CharacterUnit target, BuffStatType stat, float value, float duration)
    {
        ModifyStat(target, stat, value);
        yield return new WaitForSeconds(duration);
        ModifyStat(target, stat, -value);
    }

    private void ModifyStat(CharacterUnit target, BuffStatType stat, float delta)
    {
        switch (stat)
        {
            case BuffStatType.Strength:
                target.currentStrength += delta;
                break;
            case BuffStatType.Defense:
                target.currentDefense += delta;
                break;
            case BuffStatType.Initiative:
                target.currentInitiative += delta;
                break;
        }
    }

    private float GetBaseStat(CharacterUnit target, BuffStatType stat)
    {
        return stat switch
        {
            BuffStatType.Strength => target.Data.baseStrength,
            BuffStatType.Defense => target.Data.baseDefense,
            BuffStatType.Initiative => target.Data.baseInitiative,
            _ => 0f,
        };
    }

    public void ApplyInterceptionImmunity(CharacterUnit target, int turns)
    {
        if (target == null)
            return;
        target.isInterceptionImmune = true;
        target.interceptionImmunityTurns = Mathf.Max(target.interceptionImmunityTurns, turns);
    }

    public void ExtendEffectDurations(CharacterUnit target, int additionalTurns)
    {
        if (target == null || additionalTurns <= 0)
            return;

        if (target.interceptionImmunityTurns > 0)
            target.interceptionImmunityTurns += additionalTurns;

        // TODO: étendre la durée des autres effets quand ils seront implémentés
    }

    public void ApplySleep(CharacterUnit target)
    {
        if (target == null)
            return;
        var sleep = target.GetComponent<SleepStatus>();
        if (sleep == null)
            sleep = target.gameObject.AddComponent<SleepStatus>();
        sleep.Sleep();
    }

    public void RemoveSleep(CharacterUnit target)
    {
        if (target == null)
            return;
        var sleep = target.GetComponent<SleepStatus>();
        if (sleep != null)
            sleep.WakeUp();
    }
}
