using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Représente un slot interactif dans l'onglet "Sets" de l'inventaire.
/// Chaque slot peut référencer soit une <see cref="MusicalMoveSO"/>,
/// soit un <see cref="ItemData"/> et affiche l'ordre de priorité
/// choisi par le joueur.
/// </summary>
public class InventorySetSlot : MonoBehaviour
{
    /// <summary>
    /// Distingue les deux familles de contenus gérées par le panneau.
    /// </summary>
    public enum SlotKind
    {
        MusicalMove,
        Item
    }

    [Header("Références UI")]
    [Tooltip("Texte principal affichant le nom du move ou de l'objet.")]
    [SerializeField] private TextMeshProUGUI nameLabel;

    [Tooltip("Label affichant la position du slot dans la liste ordonnée.")]
    [SerializeField] private TextMeshProUGUI orderLabel;

    [Tooltip("Icône illustrant le contenu du slot (optionnelle).")]
    [SerializeField] private Image iconImage;

    [Tooltip("Contour ou objet activé lorsque le slot possède le focus navigation.")]
    [SerializeField] private GameObject focusHighlight;

    [Tooltip("Bouton Unity utilisé pour capter les clics/sélections.")]
    [SerializeField] private Button button;

    /// <summary>
    /// Évènement déclenché lorsque le joueur valide ce slot (clic souris ou bouton A).
    /// </summary>
    public event Action<InventorySetSlot> Clicked;

    /// <summary>
    /// Type de donnée actuellement contenue dans le slot.
    /// </summary>
    public SlotKind Kind { get; private set; }

    /// <summary>
    /// Move associé lorsqu'il s'agit d'un slot d'attaque musicale.
    /// </summary>
    public MusicalMoveSO BoundMove { get; private set; }

    /// <summary>
    /// Item associé lorsque le slot représente un objet d'inventaire.
    /// </summary>
    public ItemData BoundItem { get; private set; }

    /// <summary>
    /// Index de priorité (0 = premier dans la liste). -1 signifie non sélectionné.
    /// </summary>
    public int OrderIndex { get; private set; } = -1;

    /// <summary>
    /// Indique rapidement si le slot fait partie de la sélection active.
    /// </summary>
    public bool IsSelected => OrderIndex >= 0;

    private void Awake()
    {
        // Sécurise le champ bouton pour éviter les NullReferenceException.
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>
    /// Associe le slot à une attaque musicale.
    /// </summary>
    public void BindMusicalMove(MusicalMoveSO move)
    {
        Kind = SlotKind.MusicalMove;
        BoundMove = move;
        BoundItem = null;
        RefreshTexts(move != null ? move.moveName : string.Empty);
        UpdateIcon(move != null ? move.moveIcon : null);
    }

    /// <summary>
    /// Associe le slot à un item d'inventaire.
    /// </summary>
    public void BindItem(ItemData item)
    {
        Kind = SlotKind.Item;
        BoundItem = item;
        BoundMove = null;
        RefreshTexts(item != null ? item.itemName : string.Empty);
        UpdateIcon(item != null ? item.itemIcon : null);
    }

    /// <summary>
    /// Modifie l'état visuel lorsque le slot reçoit ou perd le focus.
    /// </summary>
    public void SetFocus(bool hasFocus)
    {
        if (focusHighlight != null)
            focusHighlight.SetActive(hasFocus);
    }

    /// <summary>
    /// Applique l'index de priorité et met à jour l'affichage utilisateur.
    /// </summary>
    public void SetOrderIndex(int index)
    {
        OrderIndex = index;

        if (orderLabel == null)
            return;

        // Laisse le champ vide lorsqu'aucun ordre n'est défini pour éviter toute confusion.
        orderLabel.text = index >= 0 ? (index + 1).ToString() : string.Empty;
    }

    /// <summary>
    /// Force le libellé principal et assure une valeur non nulle.
    /// </summary>
    private void RefreshTexts(string label)
    {
        if (nameLabel != null)
            nameLabel.text = string.IsNullOrWhiteSpace(label) ? "?" : label;
    }

    /// <summary>
    /// Met à jour l'icône associée au slot (masque l'image si aucune icône n'est fournie).
    /// </summary>
    private void UpdateIcon(Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    /// <summary>
    /// Callback interne lorsque le bouton est activé par l'utilisateur.
    /// </summary>
    private void HandleClick()
    {
        Clicked?.Invoke(this);
    }
}

