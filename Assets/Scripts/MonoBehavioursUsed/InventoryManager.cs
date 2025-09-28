using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Configuration initiale")]
    [FormerlySerializedAs("inventoryItems")]
    [SerializeField, HideInInspector] private List<ItemData> legacySerializedItems = new();

    [Tooltip("Piles d'objets disponibles au lancement. Chaque entrée référence un ScriptableObject ItemData.")]
    [SerializeField] private List<InventoryItemStack> itemStacks = new();

    [Header("Interface utilisateur - Inventaire")]
    [Tooltip("CanvasGroup principal de l'inventaire. Il est fondu lors de l'ouverture/fermeture.")]
    [SerializeField] private CanvasGroup inventoryCanvasGroup;
    [Tooltip("Durée en secondes du fondu d'ouverture/fermeture de l'inventaire.")]
    [SerializeField] private float inventoryFadeDuration = 2f;
    [Tooltip("Définit si l'inventaire doit être visible au lancement.")]
    [SerializeField] private bool openInventoryOnStart = false;

    /// <summary>
    /// Dictionnaire interne pour accéder rapidement aux piles existantes.
    /// </summary>
    private readonly Dictionary<ItemData, InventoryItemStack> stackLookup = new();

    /// <summary>
    /// Liste réutilisée pour exposer un inventaire à plat (utilisée par l'UI).
    /// </summary>
    private readonly List<ItemData> flatInventoryCache = new();

    /// <summary>
    /// Permet d'interroger directement les piles (utile pour les interfaces avancées).
    /// </summary>
    public IReadOnlyList<InventoryItemStack> ItemStacks => itemStacks;

    /// <summary>
    /// Coroutine en cours responsable du fondu d'alpha du CanvasGroup.
    /// On la conserve pour éviter d'empiler plusieurs transitions concurrentes.
    /// </summary>
    private Coroutine inventoryFadeRoutine;

    /// <summary>
    /// Lorsque ce drapeau est à <c>true</c>, l'inventaire est pleinement affiché.
    /// Il nous permet de connaître l'état actuel sans nous baser uniquement sur l'alpha.
    /// </summary>
    private bool isInventoryVisible;

    /// <summary>
    /// Action d'input utilisée pour ouvrir/fermer l'inventaire.
    /// Stockée pour pouvoir se désabonner proprement.
    /// </summary>
    private InputAction inventoryToggleAction;

    /// <summary>
    /// Routine utilisée pour attendre que l'InputsManager soit disponible si nécessaire.
    /// </summary>
    private Coroutine waitForInputsRoutine;

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

        // On reconstruit le dictionnaire runtime à partir des données sérialisées.
        RebuildLookup();

        // Conversion automatique des anciennes sauvegardes qui utilisaient une
        // simple liste de ScriptableObjects pour représenter l'inventaire.
        ImportLegacySerializedItems();

        // Un tri systématique garantit un affichage cohérent dans tous les menus.
        SortStacks();

        // On prépare l'état visuel initial de l'inventaire (ouvert ou fermé).
        InitializeInventoryUI();

        // Dès que possible, on s'abonne à l'input d'ouverture/fermeture.
        EnsureInventoryInputSubscription();
    }

    private void OnEnable()
    {
        // Lors d'un rechargement de scène ou d'une réactivation du GameObject,
        // on vérifie que le CanvasGroup est correctement configuré.
        InitializeInventoryUI();

        // On relance la souscription à l'action d'inventaire si nécessaire.
        EnsureInventoryInputSubscription();
    }

    private void OnDisable()
    {
        // On évite les fuites d'évènements en se désabonnant systématiquement.
        ReleaseInventoryInputSubscription();

        // Si une routine d'attente était active (InputsManager manquant), on l'annule.
        if (waitForInputsRoutine != null)
        {
            StopCoroutine(waitForInputsRoutine);
            waitForInputsRoutine = null;
        }
    }

    /// <summary>
    /// Renvoie la liste des items que le joueur possède.
    /// </summary>
    public IReadOnlyList<ItemData> GetInventoryItems()
    {
        flatInventoryCache.Clear();

        foreach (var stack in itemStacks)
        {
            if (stack?.item == null)
                continue;

            if (stack.item.consumeOnUse && stack.quantity <= 0)
                continue; // Pile vide : on ne l'affiche plus dans l'UI.

            int copies = stack.item.consumeOnUse
                ? stack.quantity
                : Mathf.Max(1, stack.quantity);
            for (int i = 0; i < copies; i++)
                flatInventoryCache.Add(stack.item);
        }

        return flatInventoryCache;
    }

    /// <summary>
    /// Renvoie la liste des items possédés et utilisables en combat.
    /// Lorsque <paramref name="prioritizedFor"/> est défini, les items sont
    /// réordonnés pour placer les favoris du set actif en tête de liste.
    /// </summary>
    public List<ItemData> GetUsableItems(CharacterUnit prioritizedFor = null)
    {
        List<ItemData> usable = new();

        foreach (var stack in itemStacks)
        {
            if (!IsStackUsable(stack))
                continue;

            int copies = stack.item.consumeOnUse
                ? stack.quantity
                : Mathf.Max(1, stack.quantity);
            for (int i = 0; i < copies; i++)
                usable.Add(stack.item);
        }

        if (prioritizedFor == null || prioritizedFor.Data == null)
            return usable;

        return prioritizedFor.OrderItemsForCurrentSet(usable);
    }

    /// <summary>
    /// Ajoute un item à l'inventaire (uniquement si c'est un item valide du jeu).
    /// </summary>
    public void AddItem(ItemData item, int quantity = 1)
    {
        AddItemInternal(item, quantity, true);
    }

    private void AddItemInternal(ItemData item, int quantity, bool logAddition)
    {
        if (item == null || quantity <= 0)
            return;

        if (!stackLookup.TryGetValue(item, out var stack))
        {
            stack = new InventoryItemStack(item, 0);
            itemStacks.Add(stack);
            stackLookup[item] = stack;
        }

        stack.AddQuantity(quantity);
        SortStacks();

        if (logAddition)
            Debug.Log($"[Inventory] Ajout de l'objet : {item.itemName} (x{quantity}).");
    }

    /// <summary>
    /// Ajoute une série d'items à l'inventaire (conserve la compatibilité avec l'ancien appel).
    /// </summary>
    public void AddItem(List<ItemData> items)
    {
        foreach (ItemData item in items)
        {
            AddItem(item);
        }        
    }

    /// <summary>
    /// Utilise un item (et l'enlève de l'inventaire). La cible doit être définie à l'avance.
    /// </summary>
    public void UseItem(ItemData item, CharacterUnit caster, CharacterUnit target, bool isCritical)
    {
        if (!CanUseItem(item))
        {
            Debug.LogWarning($"Limite d'utilisation atteinte pour {item.itemName}.");
            return;
        }

        Debug.Log($"[Inventory] Utilisation de l'objet : {item.itemName} sur {target.Data.characterName}");

        item.ApplyEffect(caster, target, isCritical);
        RegisterItemUse(item);
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

        if (!stackLookup.TryGetValue(item, out var stack))
            return false;

        return IsStackUsable(stack);
    }

    /// <summary>
    /// Enregistre l'utilisation de l'item.
    /// </summary>
    public void RegisterItemUse(ItemData item)
    {
        if (!stackLookup.TryGetValue(item, out var stack))
            return;

        bool consumed = stack.RegisterUse(item.consumeOnUse);
        if (!consumed && item.consumeOnUse)
            return;

            if (item.consumeOnUse && stack.quantity <= 0)
            {
                itemStacks.Remove(stack);
                stackLookup.Remove(item);
            }
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par tour.
    /// </summary>
    public void ResetTurnItemUsage()
    {
        foreach (var stack in itemStacks)
            stack?.ResetTurnUsage();
    }

    /// <summary>
    /// Réinitialise les compteurs d'utilisation par combat.
    /// </summary>
    public void ResetBattleItemUsage()
    {
        foreach (var stack in itemStacks)
            stack?.ResetBattleUsage();
    }

    /// <summary>
    /// Vérifie si la pile peut être utilisée (stock suffisant + limites respectées).
    /// </summary>
    private bool IsStackUsable(InventoryItemStack stack)
    {
        if (stack == null || stack.item == null)
            return false;

        if (!stack.item.isUsableInBattle)
            return false;

        bool hasStock = stack.item.consumeOnUse ? stack.quantity > 0 : true;
        if (!hasStock)
            return false;

        if (stack.item.maxUsesPerTurn > 0 && stack.usesThisTurn >= stack.item.maxUsesPerTurn)
            return false;

        if (stack.item.maxUsesPerBattle > 0 && stack.usesThisBattle >= stack.item.maxUsesPerBattle)
            return false;

        return true;
    }

    /// <summary>
    /// Configure l'apparence initiale de l'inventaire et sécurise la référence au CanvasGroup.
    /// </summary>
    private void InitializeInventoryUI()
    {
        // Si aucun CanvasGroup n'est assigné dans l'inspecteur, on tente d'en trouver un
        // parmi les enfants pour éviter une erreur bloquante. Cette solution de repli
        // reste volontairement verbeuse afin de faciliter les diagnostics.
        if (inventoryCanvasGroup == null)
        {
            inventoryCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            if (inventoryCanvasGroup == null)
            {
                Debug.LogWarning("[InventoryManager] Aucun CanvasGroup assigné pour l'inventaire. Le fondu sera désactivé.");
                return;
            }
        }

        // On force l'alpha et l'interactivité selon l'état initial souhaité.
        isInventoryVisible = openInventoryOnStart;
        float initialAlpha = isInventoryVisible ? 1f : 0f;
        inventoryCanvasGroup.alpha = initialAlpha;
        inventoryCanvasGroup.interactable = isInventoryVisible;
        inventoryCanvasGroup.blocksRaycasts = isInventoryVisible;
    }

    /// <summary>
    /// Assure l'abonnement à l'action d'input "Inventory" du mapping Inventory.
    /// </summary>
    private void EnsureInventoryInputSubscription()
    {
        if (!isActiveAndEnabled)
            return;

        // Si on est déjà inscrit (et que l'action existe), aucune autre opération n'est nécessaire.
        if (inventoryToggleAction != null)
            return;

        var inputsManager = InputsManager.Instance;
        if (inputsManager == null || inputsManager.playerInputs == null)
        {
            // L'InputsManager peut être instancié plus tard dans le cycle de vie.
            // On patiente donc via une coroutine dédiée.
            if (waitForInputsRoutine == null)
                waitForInputsRoutine = StartCoroutine(WaitForInputsManager());
            return;
        }

        inventoryToggleAction = inputsManager.playerInputs.Inventory.Inventory;
        if (inventoryToggleAction == null)
        {
            Debug.LogWarning("[InventoryManager] L'action 'Inventory' est introuvable dans l'ActionMap Inventory.");
            return;
        }

        // On écoute l'évènement performed pour capturer les appuis validés et on active l'action.
        inventoryToggleAction.performed += OnInventoryActionPerformed;
        inventoryToggleAction.Enable();
    }

    /// <summary>
    /// Coroutine d'attente qui surveille l'apparition de l'InputsManager avant de s'inscrire aux inputs.
    /// </summary>
    private IEnumerator WaitForInputsManager()
    {
        while (InputsManager.Instance == null || InputsManager.Instance.playerInputs == null)
            yield return null;

        // La routine n'est plus nécessaire une fois l'InputsManager prêt.
        waitForInputsRoutine = null;
        EnsureInventoryInputSubscription();
    }

    /// <summary>
    /// Désabonne l'action d'input et réinitialise les références associées.
    /// </summary>
    private void ReleaseInventoryInputSubscription()
    {
        if (inventoryToggleAction == null)
            return;

        inventoryToggleAction.performed -= OnInventoryActionPerformed;
        inventoryToggleAction.Disable();
        inventoryToggleAction = null;
    }

    /// <summary>
    /// Callback invoqué par l'Input System lorsqu'un appui valide est détecté.
    /// </summary>
    private void OnInventoryActionPerformed(InputAction.CallbackContext context)
    {
        // La méthode n'est appelée que pour l'évènement "performed", mais ce test garde
        // la fonction robuste en cas de changement futur.
        if (!context.performed)
            return;

        ToggleInventory();
    }

    /// <summary>
    /// Inverse l'état d'affichage de l'inventaire.
    /// </summary>
    public void ToggleInventory()
    {
        SetInventoryVisibility(!isInventoryVisible);
    }

    /// <summary>
    /// Applique un état ouvert/fermé et gère le fondu correspondant.
    /// </summary>
    /// <param name="visible">Nouvel état souhaité.</param>
    private void SetInventoryVisibility(bool visible)
    {
        if (inventoryCanvasGroup == null)
        {
            Debug.LogWarning("[InventoryManager] Impossible de modifier l'UI d'inventaire : CanvasGroup manquant.");
            return;
        }

        isInventoryVisible = visible;

        // On interrompt toute transition précédente pour garantir un comportement déterministe.
        if (inventoryFadeRoutine != null)
        {
            StopCoroutine(inventoryFadeRoutine);
            inventoryFadeRoutine = null;
        }

        float targetAlpha = visible ? 1f : 0f;

        // Si la durée est quasi nulle, on applique immédiatement le résultat final.
        if (inventoryFadeDuration <= 0f)
        {
            inventoryCanvasGroup.alpha = targetAlpha;
            inventoryCanvasGroup.interactable = visible;
            inventoryCanvasGroup.blocksRaycasts = visible;
            return;
        }

        // Lors de l'ouverture on bloque immédiatement les clics sur le monde.
        if (visible)
        {
            inventoryCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            // À la fermeture, l'interactivité est coupée dès le début du fondu pour éviter
            // qu'un clic résiduel déclenche une action pendant la transition.
            inventoryCanvasGroup.interactable = false;
        }

        inventoryFadeRoutine = StartCoroutine(FadeInventoryCanvas(targetAlpha, visible));
    }

    /// <summary>
    /// Coroutine responsable de l'interpolation de l'alpha du CanvasGroup.
    /// </summary>
    /// <param name="targetAlpha">Alpha final à atteindre.</param>
    /// <param name="targetVisible">État logique attendu à la fin du fondu.</param>
    private IEnumerator FadeInventoryCanvas(float targetAlpha, bool targetVisible)
    {
        float startingAlpha = inventoryCanvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, inventoryFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // On utilise le temps non-scalé pour garder le fondu actif même en pause.
            float t = Mathf.Clamp01(elapsed / duration);
            inventoryCanvasGroup.alpha = Mathf.Lerp(startingAlpha, targetAlpha, t);
            yield return null;
        }

        // On force la valeur finale pour éviter les approximations flottantes.
        inventoryCanvasGroup.alpha = targetAlpha;
        inventoryCanvasGroup.interactable = targetVisible;
        inventoryCanvasGroup.blocksRaycasts = targetVisible;

        inventoryFadeRoutine = null;
    }

    private void RebuildLookup()
    {
        stackLookup.Clear();
        itemStacks ??= new List<InventoryItemStack>();
        itemStacks.RemoveAll(stack => stack == null || stack.item == null);

        foreach (var stack in itemStacks)
            stackLookup[stack.item] = stack;
    }

    private void ImportLegacySerializedItems()
    {
        if (legacySerializedItems == null || legacySerializedItems.Count == 0)
            return;

        foreach (var item in legacySerializedItems)
            AddItemInternal(item, 1, false);

        legacySerializedItems.Clear();
    }

    private void SortStacks()
    {
        itemStacks.Sort((a, b) =>
        {
            string aName = a?.item != null ? a.item.itemName : string.Empty;
            string bName = b?.item != null ? b.item.itemName : string.Empty;
            return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
        });
    }
}
