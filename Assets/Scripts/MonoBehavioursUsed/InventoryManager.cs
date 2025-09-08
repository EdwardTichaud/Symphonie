using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Items actuellement en inventaire")]
    [SerializeField] private List<ItemData> inventoryItems = new();

    // Suivi des limitations d'utilisation des items
    private Dictionary<ItemData, int> itemUsesThisTurn = new();
    private Dictionary<ItemData, int> itemUsesThisBattle = new();

    // --- Suivi des effets temporaires appliqués aux personnages ---
    // Pour chaque personnage, on garde la liste des modificateurs actifs afin
    // de pouvoir prolonger leur durée si nécessaire.
    private class ActiveStatModifier
    {
        public BuffStatType stat;    // Statistique affectée
        public float value;          // Valeur actuellement appliquée
        public float remaining;      // Temps restant avant la fin de l'effet
        public Coroutine routine;    // Coroutine responsable de la durée
    }

    // Dictionnaire principal : clé = unité concernée, valeur = liste de ses modificateurs
    private readonly Dictionary<CharacterUnit, List<ActiveStatModifier>> activeModifiers = new();

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
    /// Renvoie la liste des items que le joueur possède.
    /// </summary>
    public IReadOnlyList<ItemData> GetInventoryItems() => inventoryItems;

    /// <summary>
    /// Renvoie la liste des items possédés et utilisables en combat.
    /// </summary>
    public List<ItemData> GetUsableItems()
        => inventoryItems.Where(item => item.isUsableInBattle && CanUseItem(item)).ToList();

    /// <summary>
    /// Ajoute un item à l'inventaire (uniquement si c'est un item valide du jeu).
    /// </summary>
    public void AddItem(List<ItemData> items)
    {
        foreach (ItemData item in items)
        {
            inventoryItems.Add(item);
            Debug.Log($"[Inventory] Ajout de l'objet : {item.itemName}");
        }        
    }

    /// <summary>
    /// Utilise un item (et l'enlève de l'inventaire). La cible doit être définie à l'avance.
    /// </summary>
    public void UseItem(ItemData item, CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        if (!inventoryItems.Contains(item))
        {
            Debug.LogWarning($"Impossible d'utiliser {item.itemName} : non trouvé en inventaire.");
            return;
        }

        if (!CanUseItem(item))
        {
            Debug.LogWarning($"Limite d'utilisation atteinte pour {item.itemName}.");
            return;
        }

        Debug.Log($"[Inventory] Utilisation de l'objet : {item.itemName} sur {target.Data.characterName}");

        item.ApplyEffect(caster, target, isCritical);
        RegisterItemUse(item);
        inventoryItems.Remove(item);
    }

    /// <summary>
    /// Utilise un item sans le consommer (debug ou test).
    /// </summary>
    public void PreviewItemEffect(ItemData item, CharacterUnit target)
    {
        Debug.Log($"[Inventory] Aperçu de l'effet de {item.itemName} sur {target.Data.characterName}");
        item.ApplyEffect(null, target, false);
    }

    public void ApplyBuff(CharacterUnit target, BuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (target == null || stat == BuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat(target, stat);
        float value = isPercentage ? baseValue * amount / 100f : amount;
        ApplyStatModifier(target, stat, value, duration);
    }

    public void ApplyDebuff(CharacterUnit target, DebuffStatType stat, int amount, float duration, bool isPercentage)
    {
        if (target == null || stat == DebuffStatType.None || amount == 0)
            return;

        float baseValue = GetBaseStat(target, (BuffStatType)stat);
        float value = isPercentage ? baseValue * amount / 100f : amount;
        ApplyStatModifier(target, (BuffStatType)stat, -value, duration);
    }

    /// <summary>
    /// Applique ou prolonge un modificateur de statistique.
    /// </summary>
    private void ApplyStatModifier(CharacterUnit target, BuffStatType stat, float value, float duration)
    {
        if (!activeModifiers.TryGetValue(target, out var list))
        {
            list = new List<ActiveStatModifier>();
            activeModifiers[target] = list;
        }

        var modifier = list.Find(m => m.stat == stat);
        if (modifier != null)
        {
            // Si un modificateur existe déjà, on cumule la valeur et on prolonge sa durée
            ModifyStat(target, stat, value);
            modifier.value += value;
            modifier.remaining += duration;
        }
        else
        {
            modifier = new ActiveStatModifier
            {
                stat = stat,
                value = value,
                remaining = duration
            };
            ModifyStat(target, stat, value);
            modifier.routine = StartCoroutine(StatModifierRoutine(target, modifier));
            list.Add(modifier);
        }
    }

    /// <summary>
    /// Coroutine gérant la durée d'un modificateur.
    /// </summary>
    private IEnumerator StatModifierRoutine(CharacterUnit target, ActiveStatModifier modifier)
    {
        while (modifier.remaining > 0f)
        {
            yield return null;
            modifier.remaining -= Time.deltaTime;
        }
        ModifyStat(target, modifier.stat, -modifier.value);
        activeModifiers[target].Remove(modifier);
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

    /// <summary>
    /// Prolonge la durée de tous les effets temporaires actuellement actifs sur la cible.
    /// </summary>
    public void ExtendEffectDurations(CharacterUnit target, int additionalTurns)
    {
        if (target == null || additionalTurns <= 0)
            return;

        // Prolonge l'immunité à l'interception si présente
        if (target.interceptionImmunityTurns > 0)
            target.interceptionImmunityTurns += additionalTurns;

        // Prolonge également tous les buffs/debuffs suivis pour cette unité
        if (activeModifiers.TryGetValue(target, out var list))
        {
            foreach (var modifier in list)
            {
                modifier.remaining += additionalTurns;
            }
        }
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

    // ------------------------------------------------------------------
    // Gestion des limitations d'utilisation des items
    // ------------------------------------------------------------------

    /// <summary>
    /// Vérifie si l'item peut être utilisé en fonction des limites définies.
    /// </summary>
    public bool CanUseItem(ItemData item)
    {
        if (item == null)
            return false;

        if (item.maxUsesPerTurn > 0)
        {
            itemUsesThisTurn.TryGetValue(item, out int usedTurn);
            if (usedTurn >= item.maxUsesPerTurn)
                return false;
        }

        if (item.maxUsesPerBattle > 0)
        {
            itemUsesThisBattle.TryGetValue(item, out int usedBattle);
            if (usedBattle >= item.maxUsesPerBattle)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Enregistre l'utilisation de l'item.
    /// </summary>
    public void RegisterItemUse(ItemData item)
    {
        if (item.maxUsesPerTurn > 0)
        {
            itemUsesThisTurn.TryGetValue(item, out int usedTurn);
            itemUsesThisTurn[item] = usedTurn + 1;
        }

        if (item.maxUsesPerBattle > 0)
        {
            itemUsesThisBattle.TryGetValue(item, out int usedBattle);
            itemUsesThisBattle[item] = usedBattle + 1;
        }
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par tour.
    /// </summary>
    public void ResetTurnItemUsage()
    {
        itemUsesThisTurn.Clear();
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par combat.
    /// </summary>
    public void ResetBattleItemUsage()
    {
        itemUsesThisTurn.Clear();
        itemUsesThisBattle.Clear();
    }
}
