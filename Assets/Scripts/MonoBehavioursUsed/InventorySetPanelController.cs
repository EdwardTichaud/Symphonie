using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestionnaire principal de l'onglet "Inventory_Sets". Cette classe orchestre
/// la navigation entre les deux viewports (attaques musicales et items), permet
/// de construire des sets personnalisés puis de les sauvegarder ou de les
/// supprimer.
/// </summary>
public class InventorySetPanelController : MonoBehaviour, PlayerInputs.IInventoryActions
{
    public static InventorySetPanelController Instance { get; private set; }

    [Header("Racine UI")]
    [Tooltip("GameObject racine du panneau d'inventaire.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Viewports de sélection")]
    [SerializeField] private InventoryViewportController musicalViewport;
    [SerializeField] private InventoryViewportController itemViewport;

    [Header("Sélection des sets existants")]
    [SerializeField] private TMP_Dropdown musicalSetsDropdown;
    [SerializeField] private TMP_Dropdown itemSetsDropdown;

    [Header("Boutons d'action")]
    [SerializeField] private Button saveMusicalSetButton;
    [SerializeField] private Button deleteMusicalSetButton;
    [SerializeField] private Button saveItemSetButton;
    [SerializeField] private Button deleteItemSetButton;
    [SerializeField] private Button closeButton;

    [Header("Libellés d'aide")]
    [SerializeField] private TextMeshProUGUI headerLabel;
    [SerializeField] private TextMeshProUGUI helperLabel;
    [SerializeField] private TextMeshProUGUI feedbackLabel;

    [Header("Textes par défaut")]
    [SerializeField] private string musicalViewportTitle = "Répertoire musical";
    [SerializeField] private string itemViewportTitle = "Sac d'objets";
    [SerializeField] [TextArea] private string helperNavigationText = "🕹️ Joystick pour naviguer — ✅ pour ajouter/retirer — ❌ pour fermer.";

    // Données runtime manipulées par le panneau.
    private readonly List<ItemData> availableItems = new();
    private readonly List<InventorySetSlot> musicalSelection = new();
    private readonly List<InventorySetSlot> itemSelection = new();
    private readonly Dictionary<MusicalMoveSO, InventorySetSlot> musicalSlotLookup = new();
    private readonly Dictionary<ItemData, InventorySetSlot> itemSlotLookup = new();
    private readonly List<int> musicalDropdownMap = new();
    private readonly List<int> itemDropdownMap = new();

    private InventoryViewportController[] viewports;
    private int currentViewportIndex = 0;
    private bool isOpen = false;
    private bool ownsLocalInputs = false;
    private bool isAwaitingNameInput = false;

    private CharacterUnit currentCharacter;
    private InputsManager inputsManager;
    private PlayerInputs playerInputs;

    private enum PendingPromptType { None, SaveMusical, SaveItem }
    private PendingPromptType pendingPrompt = PendingPromptType.None;
    private string pendingSetName = string.Empty;
    private List<MusicalMoveSO> pendingMusicalOrder;
    private List<ItemData> pendingItemOrder;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        inputsManager = InputsManager.Instance;
        if (inputsManager != null)
        {
            playerInputs = inputsManager.playerInputs;
            ownsLocalInputs = false;
        }
        else
        {
            playerInputs = new PlayerInputs();
            ownsLocalInputs = true;
        }

        if (panelRoot == null)
            panelRoot = gameObject;

        panelRoot.SetActive(false);
        viewports = new[] { musicalViewport, itemViewport };

        RegisterButton(saveMusicalSetButton, RequestSaveMusicalSet);
        RegisterButton(deleteMusicalSetButton, RequestDeleteMusicalSet);
        RegisterButton(saveItemSetButton, RequestSaveItemSet);
        RegisterButton(deleteItemSetButton, RequestDeleteItemSet);
        RegisterButton(closeButton, ClosePanel);

        if (musicalSetsDropdown != null)
            musicalSetsDropdown.onValueChanged.AddListener(OnMusicalSetDropdownChanged);
        if (itemSetsDropdown != null)
            itemSetsDropdown.onValueChanged.AddListener(OnItemSetDropdownChanged);
    }

    private void OnDestroy()
    {
        if (musicalSetsDropdown != null)
            musicalSetsDropdown.onValueChanged.RemoveListener(OnMusicalSetDropdownChanged);
        if (itemSetsDropdown != null)
            itemSetsDropdown.onValueChanged.RemoveListener(OnItemSetDropdownChanged);

        if (ownsLocalInputs && playerInputs != null)
            playerInputs.Dispose();
    }

    private void OnEnable()
    {
        if (playerInputs != null)
            playerInputs.World.Inventory.performed += OnInventoryShortcut;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
            playerInputs.World.Inventory.performed -= OnInventoryShortcut;

        // Sécurité : garantit la fermeture même en cas de désactivation extérieure.
        if (isOpen)
            ClosePanelImmediate();
    }

    #region Configuration publique

    /// <summary>
    /// Définit le personnage dont on souhaite configurer les sets.
    /// </summary>
    public void SetTargetCharacter(CharacterUnit unit)
    {
        currentCharacter = unit;
        RebuildMusicalSlots();
        RefreshMusicalSetDropdown();
    }

    /// <summary>
    /// Indique la liste d'objets disponibles pour la construction des sets.
    /// </summary>
    public void SetAvailableItems(IEnumerable<ItemData> items)
    {
        availableItems.Clear();
        if (items != null)
            availableItems.AddRange(items.Where(i => i != null));

        RebuildItemSlots();
        RefreshItemSetDropdown();
    }

    #endregion

    #region Ouverture / fermeture du panneau

    /// <summary>
    /// Appelé par l'InputsManager lorsque le joueur presse la touche d'inventaire.
    /// </summary>
    private void OnInventoryShortcut(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (isOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    /// <summary>
    /// Ouvre le panneau et active la map d'inputs "Inventory" uniquement.
    /// </summary>
    private void OpenPanel()
    {
        if (isOpen)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (inputsManager != null)
            inputsManager.ActivateOnly(playerInputs.Inventory.Get());
        else
            playerInputs.Inventory.Enable();

        playerInputs.Inventory.AddCallbacks(this);
        isOpen = true;
        isAwaitingNameInput = false;

        SelectViewport(0);
        UpdateHelperTexts();
        ShowFeedback("Bienvenue dans la gestion des sets !");

        // Propose par défaut les objets actuellement dans l'inventaire si aucun contexte n'a été fourni.
        if (availableItems.Count == 0 && InventoryManager.Instance != null)
            SetAvailableItems(InventoryManager.Instance.GetInventoryItems());

        // S'assure que les slots reflètent la configuration courante.
        RebuildMusicalSlots();
        RebuildItemSlots();
        ApplyMusicalSetFromDropdown();
        ApplyItemSetFromDropdown();
    }

    /// <summary>
    /// Ferme le panneau et réactive les contrôles monde.
    /// </summary>
    public void ClosePanel()
    {
        if (!isOpen)
            return;

        ClosePanelImmediate();

        if (inputsManager != null)
            inputsManager.ActivateOnly(playerInputs.World.Get());
        else
            playerInputs.World.Enable();
    }

    /// <summary>
    /// Ferme le panneau sans réactiver automatiquement les autres maps
    /// (utilisé lors des OnDisable pour éviter les appels superflus).
    /// </summary>
    private void ClosePanelImmediate()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (playerInputs != null)
        {
            playerInputs.Inventory.RemoveCallbacks(this);
            playerInputs.Inventory.Disable();
        }
        isOpen = false;
        isAwaitingNameInput = false;
        pendingPrompt = PendingPromptType.None;
    }

    #endregion

    #region Gestion des slots et des viewports

    private void RebuildMusicalSlots()
    {
        if (musicalViewport == null)
            return;

        musicalViewport.Clear();
        musicalSelection.Clear();
        musicalSlotLookup.Clear();

        var data = currentCharacter?.Data;
        if (data == null)
        {
            ShowFeedback("Aucun personnage sélectionné.");
            return;
        }

        HashSet<MusicalMoveSO> uniqueMoves = new();
        if (data.musicalAttacks != null)
        {
            foreach (var move in data.musicalAttacks)
            {
                if (move == null || !uniqueMoves.Add(move))
                    continue;

                CreateMusicalSlot(move);
            }
        }

        if (data.specialMusicalMove != null && uniqueMoves.Add(data.specialMusicalMove))
            CreateMusicalSlot(data.specialMusicalMove);

        musicalViewport.FocusFirstEntry();
    }

    private void RebuildItemSlots()
    {
        if (itemViewport == null)
            return;

        itemViewport.Clear();
        itemSelection.Clear();
        itemSlotLookup.Clear();

        if (availableItems.Count == 0)
        {
            ShowFeedback("Aucun objet disponible pour construire un set.");
            return;
        }

        HashSet<ItemData> uniqueItems = new();
        foreach (var item in availableItems)
        {
            if (item == null || !uniqueItems.Add(item))
                continue;

            var slot = itemViewport.CreateSlot();
            if (slot == null)
                continue;

            slot.BindItem(item);
            slot.Clicked += OnSlotClicked;
            itemSlotLookup[item] = slot;
        }

        itemViewport.FocusFirstEntry();
    }

    private void CreateMusicalSlot(MusicalMoveSO move)
    {
        var slot = musicalViewport.CreateSlot();
        if (slot == null)
            return;

        slot.BindMusicalMove(move);
        slot.Clicked += OnSlotClicked;
        musicalSlotLookup[move] = slot;
    }

    private void OnSlotClicked(InventorySetSlot slot)
    {
        if (!isOpen || slot == null || isAwaitingNameInput)
            return;

        ToggleSlotSelection(slot);
    }

    private void ToggleSlotSelection(InventorySetSlot slot)
    {
        if (slot.Kind == InventorySetSlot.SlotKind.MusicalMove)
        {
            if (slot.BoundMove == null)
                return;

            ToggleSlotInList(slot, musicalSelection);
        }
        else
        {
            if (slot.BoundItem == null)
                return;

            ToggleSlotInList(slot, itemSelection);
        }
    }

    private static void ToggleSlotInList(InventorySetSlot slot, List<InventorySetSlot> list)
    {
        if (!slot.IsSelected)
        {
            list.Add(slot);
            slot.SetOrderIndex(list.Count - 1);
        }
        else
        {
            int index = list.IndexOf(slot);
            if (index < 0)
                return;

            list.RemoveAt(index);
            slot.SetOrderIndex(-1);

            for (int i = index; i < list.Count; i++)
                list[i].SetOrderIndex(i);
        }
    }

    private void SelectViewport(int index)
    {
        if (viewports == null || viewports.Length == 0)
            return;

        if (index < 0)
            index = viewports.Length - 1;
        if (index >= viewports.Length)
            index = 0;

        currentViewportIndex = index;

        for (int i = 0; i < viewports.Length; i++)
            viewports[i]?.SetFocus(i == currentViewportIndex);

        viewports[currentViewportIndex]?.FocusFirstEntry();
        UpdateHelperTexts();
    }

    private InventoryViewportController CurrentViewport =>
        (viewports != null && currentViewportIndex >= 0 && currentViewportIndex < viewports.Length)
            ? viewports[currentViewportIndex]
            : null;

    #endregion

    #region Dropdowns et chargement des sets

    private void RefreshMusicalSetDropdown(string setToSelect = null)
    {
        if (musicalSetsDropdown == null)
            return;

        musicalDropdownMap.Clear();
        musicalSetsDropdown.ClearOptions();
        musicalSetsDropdown.value = 0;
        musicalSetsDropdown.RefreshShownValue();

        var data = currentCharacter?.Data;
        if (data == null || data.musicalMoveSets == null || data.musicalMoveSets.Count == 0)
            return;

        List<string> options = new();
        for (int i = 0; i < data.musicalMoveSets.Count; i++)
        {
            var set = data.musicalMoveSets[i];
            if (set == null || string.IsNullOrWhiteSpace(set.setName))
                continue;

            musicalDropdownMap.Add(i);
            options.Add(set.setName);
        }

        musicalSetsDropdown.AddOptions(options);

        if (options.Count == 0)
            return;

        int targetIndex = 0;
        if (!string.IsNullOrWhiteSpace(setToSelect))
        {
            int idx = options.FindIndex(o => string.Equals(o, setToSelect, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                targetIndex = idx;
        }

        musicalSetsDropdown.value = targetIndex;
        musicalSetsDropdown.RefreshShownValue();
    }

    private void RefreshItemSetDropdown(string setToSelect = null)
    {
        if (itemSetsDropdown == null)
            return;

        itemDropdownMap.Clear();
        itemSetsDropdown.ClearOptions();
        itemSetsDropdown.value = 0;
        itemSetsDropdown.RefreshShownValue();

        var data = currentCharacter?.Data;
        if (data == null || data.itemSets == null || data.itemSets.Count == 0)
            return;

        List<string> options = new();
        for (int i = 0; i < data.itemSets.Count; i++)
        {
            var set = data.itemSets[i];
            if (set == null || string.IsNullOrWhiteSpace(set.setName))
                continue;

            itemDropdownMap.Add(i);
            options.Add(set.setName);
        }

        itemSetsDropdown.AddOptions(options);

        if (options.Count == 0)
            return;

        int targetIndex = 0;
        if (!string.IsNullOrWhiteSpace(setToSelect))
        {
            int idx = options.FindIndex(o => string.Equals(o, setToSelect, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                targetIndex = idx;
        }

        itemSetsDropdown.value = targetIndex;
        itemSetsDropdown.RefreshShownValue();
    }

    private void OnMusicalSetDropdownChanged(int index)
    {
        if (!isOpen || isAwaitingNameInput)
            return;

        ApplyMusicalSetFromDropdown();
    }

    private void OnItemSetDropdownChanged(int index)
    {
        if (!isOpen || isAwaitingNameInput)
            return;

        ApplyItemSetFromDropdown();
    }

    private void ApplyMusicalSetFromDropdown()
    {
        var data = currentCharacter?.Data;
        if (data == null || data.musicalMoveSets == null)
            return;

        CharacterMusicalMoveSet selected = null;
        if (musicalSetsDropdown != null && musicalDropdownMap.Count > 0)
        {
            int uiIndex = Mathf.Clamp(musicalSetsDropdown.value, 0, musicalDropdownMap.Count - 1);
            int dataIndex = musicalDropdownMap[uiIndex];
            if (dataIndex >= 0 && dataIndex < data.musicalMoveSets.Count)
                selected = data.musicalMoveSets[dataIndex];
        }

        ApplyMusicalSet(selected);
    }

    private void ApplyItemSetFromDropdown()
    {
        var data = currentCharacter?.Data;
        if (data == null || data.itemSets == null)
            return;

        CharacterItemSet selected = null;
        if (itemSetsDropdown != null && itemDropdownMap.Count > 0)
        {
            int uiIndex = Mathf.Clamp(itemSetsDropdown.value, 0, itemDropdownMap.Count - 1);
            int dataIndex = itemDropdownMap[uiIndex];
            if (dataIndex >= 0 && dataIndex < data.itemSets.Count)
                selected = data.itemSets[dataIndex];
        }

        ApplyItemSet(selected);
    }

    private void ApplyMusicalSet(CharacterMusicalMoveSet set)
    {
        musicalSelection.Clear();
        foreach (var slot in musicalSlotLookup.Values)
            slot.SetOrderIndex(-1);

        if (set == null || set.prioritizedMoves == null)
            return;

        foreach (var move in set.prioritizedMoves)
        {
            if (move == null)
                continue;

            if (!musicalSlotLookup.TryGetValue(move, out var slot))
                continue;

            if (musicalSelection.Contains(slot))
                continue;

            musicalSelection.Add(slot);
            slot.SetOrderIndex(musicalSelection.Count - 1);
        }
    }

    private void ApplyItemSet(CharacterItemSet set)
    {
        itemSelection.Clear();
        foreach (var slot in itemSlotLookup.Values)
            slot.SetOrderIndex(-1);

        if (set == null || set.prioritizedItems == null)
            return;

        foreach (var item in set.prioritizedItems)
        {
            if (item == null)
                continue;

            if (!itemSlotLookup.TryGetValue(item, out var slot))
                continue;

            if (itemSelection.Contains(slot))
                continue;

            itemSelection.Add(slot);
            slot.SetOrderIndex(itemSelection.Count - 1);
        }
    }

    #endregion

    #region Sauvegarde / suppression des sets

    private void RequestSaveMusicalSet()
    {
        if (!isOpen || isAwaitingNameInput)
            return;

        if (currentCharacter?.Data == null)
        {
            ShowFeedback("Aucun personnage sélectionné.");
            return;
        }

        pendingPrompt = PendingPromptType.SaveMusical;
        pendingSetName = string.Empty;
        pendingMusicalOrder = BuildMusicalOrder();

        if (pendingMusicalOrder.Count == 0)
        {
            ShowFeedback("Sélectionnez au moins une attaque avant d'enregistrer un set.");
            pendingPrompt = PendingPromptType.None;
            return;
        }

        OpenNamePrompt("Nom du set musical");
    }

    private void RequestSaveItemSet()
    {
        if (!isOpen || isAwaitingNameInput)
            return;

        if (currentCharacter?.Data == null)
        {
            ShowFeedback("Aucun personnage sélectionné.");
            return;
        }

        pendingPrompt = PendingPromptType.SaveItem;
        pendingSetName = string.Empty;
        pendingItemOrder = BuildItemOrder();

        if (pendingItemOrder.Count == 0)
        {
            ShowFeedback("Sélectionnez au moins un objet avant d'enregistrer un set.");
            pendingPrompt = PendingPromptType.None;
            return;
        }

        OpenNamePrompt("Nom du set d'objets");
    }

    private void RequestDeleteMusicalSet()
    {
        var data = currentCharacter?.Data;
        if (data == null || data.musicalMoveSets == null || data.musicalMoveSets.Count == 0 || musicalDropdownMap.Count == 0)
        {
            ShowFeedback("Aucun set musical à supprimer.");
            return;
        }

        string setName = GetSelectedDropdownName(musicalSetsDropdown, data.musicalMoveSets, musicalDropdownMap);
        if (string.IsNullOrWhiteSpace(setName))
        {
            ShowFeedback("Sélectionnez d'abord un set musical.");
            return;
        }

        ConfirmationBox.Instance?.Show(
            $"Supprimer le set musical \"{setName}\" ?",
            () =>
            {
                DeleteMusicalSet(setName);
                RestoreInventoryInputAfterPopup();
            },
            () => RestoreInventoryInputAfterPopup());
    }

    private void RequestDeleteItemSet()
    {
        var data = currentCharacter?.Data;
        if (data == null || data.itemSets == null || data.itemSets.Count == 0 || itemDropdownMap.Count == 0)
        {
            ShowFeedback("Aucun set d'objets à supprimer.");
            return;
        }

        string setName = GetSelectedDropdownName(itemSetsDropdown, data.itemSets, itemDropdownMap);
        if (string.IsNullOrWhiteSpace(setName))
        {
            ShowFeedback("Sélectionnez d'abord un set d'objets.");
            return;
        }

        ConfirmationBox.Instance?.Show(
            $"Supprimer le set d'objets \"{setName}\" ?",
            () =>
            {
                DeleteItemSet(setName);
                RestoreInventoryInputAfterPopup();
            },
            () => RestoreInventoryInputAfterPopup());
    }

    private void DeleteMusicalSet(string setName)
    {
        var data = currentCharacter?.Data;
        if (data == null || data.musicalMoveSets == null)
            return;

        data.musicalMoveSets.RemoveAll(s => s != null && string.Equals(s.setName, setName, StringComparison.OrdinalIgnoreCase));
        RefreshMusicalSetDropdown();
        ApplyMusicalSetFromDropdown();
        ShowFeedback($"Set musical \"{setName}\" supprimé.");
    }

    private void DeleteItemSet(string setName)
    {
        var data = currentCharacter?.Data;
        if (data == null || data.itemSets == null)
            return;

        data.itemSets.RemoveAll(s => s != null && string.Equals(s.setName, setName, StringComparison.OrdinalIgnoreCase));
        RefreshItemSetDropdown();
        ApplyItemSetFromDropdown();
        ShowFeedback($"Set d'objets \"{setName}\" supprimé.");
    }

    private void SaveMusicalSet(string setName, List<MusicalMoveSO> order)
    {
        var data = currentCharacter?.Data;
        if (data == null)
            return;

        if (data.musicalMoveSets == null)
            data.musicalMoveSets = new List<CharacterMusicalMoveSet>();

        var existing = data.musicalMoveSets.FirstOrDefault(s => s != null && string.Equals(s.setName, setName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.prioritizedMoves = new List<MusicalMoveSO>(order);
        }
        else
        {
            data.musicalMoveSets.Add(new CharacterMusicalMoveSet
            {
                setName = setName,
                prioritizedMoves = new List<MusicalMoveSO>(order)
            });
        }

        RefreshMusicalSetDropdown(setName);
        ApplyMusicalSetFromDropdown();
        ShowFeedback($"Set musical \"{setName}\" enregistré !");
    }

    private void SaveItemSet(string setName, List<ItemData> order)
    {
        var data = currentCharacter?.Data;
        if (data == null)
            return;

        if (data.itemSets == null)
            data.itemSets = new List<CharacterItemSet>();

        var existing = data.itemSets.FirstOrDefault(s => s != null && string.Equals(s.setName, setName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.prioritizedItems = new List<ItemData>(order);
        }
        else
        {
            data.itemSets.Add(new CharacterItemSet
            {
                setName = setName,
                prioritizedItems = new List<ItemData>(order)
            });
        }

        RefreshItemSetDropdown(setName);
        ApplyItemSetFromDropdown();
        ShowFeedback($"Set d'objets \"{setName}\" enregistré !");
    }

    private List<MusicalMoveSO> BuildMusicalOrder()
    {
        List<MusicalMoveSO> order = new();
        foreach (var slot in musicalSelection)
        {
            if (slot?.BoundMove != null)
                order.Add(slot.BoundMove);
        }
        return order;
    }

    private List<ItemData> BuildItemOrder()
    {
        List<ItemData> order = new();
        foreach (var slot in itemSelection)
        {
            if (slot?.BoundItem != null)
                order.Add(slot.BoundItem);
        }
        return order;
    }

    private string GetSelectedDropdownName<T>(TMP_Dropdown dropdown, IList<T> sets, List<int> mapping) where T : class
    {
        if (dropdown == null || sets == null || mapping == null || mapping.Count == 0)
            return null;

        int uiIndex = Mathf.Clamp(dropdown.value, 0, mapping.Count - 1);
        int dataIndex = mapping[uiIndex];
        if (dataIndex < 0 || dataIndex >= sets.Count)
            return null;

        if (sets[dataIndex] is CharacterMusicalMoveSet musical)
            return musical?.setName;

        if (sets[dataIndex] is CharacterItemSet itemSet)
            return itemSet?.setName;

        return null;
    }

    #endregion

    #region Interaction avec le clavier virtuel

    private void OpenNamePrompt(string subject)
    {
        if (VirtualKeyboard.Instance == null)
        {
            ShowFeedback("Clavier virtuel indisponible.");
            pendingPrompt = PendingPromptType.None;
            return;
        }

        isAwaitingNameInput = true;
        VirtualKeyboard.Instance.WordValidated += OnVirtualKeyboardValidated;
        VirtualKeyboard.Instance.KeyboardClosed += OnVirtualKeyboardClosed;
        VirtualKeyboard.Instance.OpenVK(subject);
    }

    private void OnVirtualKeyboardValidated(string value)
    {
        HandleNameProvided(value);
    }

    private void OnVirtualKeyboardClosed()
    {
        CleanupKeyboardSubscriptions();
        RestoreInventoryInput();
    }

    private void HandleNameProvided(string rawValue)
    {
        string sanitized = (rawValue ?? string.Empty).Trim();
        CleanupKeyboardSubscriptions();
        RestoreInventoryInput();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            ShowFeedback("Le nom ne peut pas être vide.");
            pendingPrompt = PendingPromptType.None;
            return;
        }

        if (pendingPrompt == PendingPromptType.SaveMusical)
        {
            pendingSetName = sanitized;
            var data = currentCharacter?.Data;
            if (data != null && data.musicalMoveSets != null && data.musicalMoveSets.Any(s => s != null && string.Equals(s.setName, sanitized, StringComparison.OrdinalIgnoreCase)))
            {
                ConfirmationBox.Instance?.Show(
                    $"Remplacer le set musical \"{sanitized}\" ?",
                    () =>
                    {
                        SaveMusicalSet(pendingSetName, pendingMusicalOrder);
                        RestoreInventoryInputAfterPopup();
                    },
                    () => RestoreInventoryInputAfterPopup());
            }
            else
            {
                SaveMusicalSet(pendingSetName, pendingMusicalOrder);
            }
        }
        else if (pendingPrompt == PendingPromptType.SaveItem)
        {
            pendingSetName = sanitized;
            var data = currentCharacter?.Data;
            if (data != null && data.itemSets != null && data.itemSets.Any(s => s != null && string.Equals(s.setName, sanitized, StringComparison.OrdinalIgnoreCase)))
            {
                ConfirmationBox.Instance?.Show(
                    $"Remplacer le set d'objets \"{sanitized}\" ?",
                    () =>
                    {
                        SaveItemSet(pendingSetName, pendingItemOrder);
                        RestoreInventoryInputAfterPopup();
                    },
                    () => RestoreInventoryInputAfterPopup());
            }
            else
            {
                SaveItemSet(pendingSetName, pendingItemOrder);
            }
        }

        pendingPrompt = PendingPromptType.None;
    }

    private void CleanupKeyboardSubscriptions()
    {
        if (VirtualKeyboard.Instance != null)
        {
            VirtualKeyboard.Instance.WordValidated -= OnVirtualKeyboardValidated;
            VirtualKeyboard.Instance.KeyboardClosed -= OnVirtualKeyboardClosed;
        }

        isAwaitingNameInput = false;
    }

    private void RestoreInventoryInput()
    {
        if (!isOpen)
            return;

        if (inputsManager != null)
            inputsManager.ActivateOnly(playerInputs.Inventory.Get());
        else
            playerInputs.Inventory.Enable();
    }

    private void RestoreInventoryInputAfterPopup()
    {
        RestoreInventoryInput();
        ApplyMusicalSetFromDropdown();
        ApplyItemSetFromDropdown();
    }

    #endregion

    #region Helpers UI

    private void UpdateHelperTexts()
    {
        if (headerLabel != null)
        {
            headerLabel.text = currentViewportIndex == 0 ? musicalViewportTitle : itemViewportTitle;
        }

        if (helperLabel != null)
            helperLabel.text = helperNavigationText;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = message;
    }

    private void RegisterButton(Button button, Action callback)
    {
        if (button == null || callback == null)
            return;

        button.onClick.AddListener(() => callback());
    }

    #endregion

    #region Implémentation de IInventoryActions

    public void OnSelectViewPort_Left(InputAction.CallbackContext context)
    {
        if (!isOpen || !context.performed || isAwaitingNameInput)
            return;

        SelectViewport(currentViewportIndex - 1);
    }

    public void OnSelectViewPort_Right(InputAction.CallbackContext context)
    {
        if (!isOpen || !context.performed || isAwaitingNameInput)
            return;

        SelectViewport(currentViewportIndex + 1);
    }

    public void OnNavigate(InputAction.CallbackContext context)
    {
        if (!isOpen || isAwaitingNameInput)
            return;

        CurrentViewport?.HandleNavigation(context.ReadValue<Vector2>());
    }

    public void OnConfirm(InputAction.CallbackContext context)
    {
        if (!isOpen || !context.performed || isAwaitingNameInput)
            return;

        var slot = CurrentViewport?.CurrentSlot;
        if (slot != null)
            ToggleSlotSelection(slot);
    }

    public void OnCancel(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (isAwaitingNameInput)
            return; // Le clavier virtuel gère sa propre annulation.

        ClosePanel();
    }

    #endregion
}

