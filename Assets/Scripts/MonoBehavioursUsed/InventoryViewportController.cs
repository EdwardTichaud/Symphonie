using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contrôle un ScrollRect dédié à la sélection de slots (moves ou items).
/// La navigation au stick gauche/aux flèches est convertie en focus vertical
/// afin d'assurer une expérience fluide au pad comme au clavier.
/// </summary>
public class InventoryViewportController : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("ScrollRect parent utilisé pour afficher la liste des slots.")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Conteneur recevant les instances de slots.")]
    [SerializeField] private RectTransform contentRoot;

    [Tooltip("Prefab instancié pour chaque entrée de la liste.")]
    [SerializeField] private InventorySetSlot slotPrefab;

    [Header("Paramètres de navigation")]
    [Tooltip("Temps minimal (en secondes) entre deux déplacements successifs au stick.")]
    [SerializeField] private float navigationCooldown = 0.15f;

    private readonly List<InventorySetSlot> slots = new();
    private int focusedIndex = 0;
    private bool hasFocus = false;
    private float lastNavigationTime = -999f;

    /// <summary>
    /// Fournit un accès en lecture aux slots instanciés (utile pour le panneau parent).
    /// </summary>
    public IReadOnlyList<InventorySetSlot> Slots => slots;

    /// <summary>
    /// Retourne le slot actuellement ciblé par le focus.
    /// </summary>
    public InventorySetSlot CurrentSlot
    {
        get
        {
            if (focusedIndex < 0 || focusedIndex >= slots.Count)
                return null;
            return slots[focusedIndex];
        }
    }

    private void Awake()
    {
        // Permet de configurer automatiquement la hiérarchie lorsque le ScrollRect est connu.
        if (scrollRect != null && contentRoot == null)
            contentRoot = scrollRect.content;
    }

    /// <summary>
    /// Supprime tous les slots existants et réinitialise l'état de navigation.
    /// </summary>
    public void Clear()
    {
        foreach (var slot in slots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }

        slots.Clear();
        focusedIndex = 0;
        UpdateFocusVisuals();
    }

    /// <summary>
    /// Crée un nouveau slot via le prefab configuré et l'ajoute au contenu.
    /// </summary>
    public InventorySetSlot CreateSlot()
    {
        if (slotPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("[InventoryViewport] Impossible de créer un slot : prefab ou content manquant.");
            return null;
        }

        var instance = Instantiate(slotPrefab, contentRoot);
        instance.SetOrderIndex(-1); // Valeur neutre par défaut.
        slots.Add(instance);

        // Lorsque l'on ajoute le tout premier slot, on force le focus dessus pour éviter les états incohérents.
        if (slots.Count == 1)
            focusedIndex = 0;

        UpdateFocusVisuals();
        return instance;
    }

    /// <summary>
    /// Définit si ce viewport est actuellement ciblé par le joueur.
    /// </summary>
    public void SetFocus(bool focus)
    {
        hasFocus = focus;
        UpdateFocusVisuals();
    }

    /// <summary>
    /// Positionne le focus sur le premier slot valide (utile lors d'un changement de panneau).
    /// </summary>
    public void FocusFirstEntry()
    {
        if (slots.Count == 0)
        {
            focusedIndex = -1;
        }
        else
        {
            focusedIndex = Mathf.Clamp(focusedIndex, 0, slots.Count - 1);
        }

        UpdateFocusVisuals();
    }

    /// <summary>
    /// Interprète un vecteur de navigation pour déplacer le focus.
    /// </summary>
    public void HandleNavigation(Vector2 input)
    {
        if (!hasFocus || slots.Count == 0)
            return;

        if (Time.unscaledTime - lastNavigationTime < navigationCooldown)
            return; // Laisse le temps au joueur de relâcher le stick.

        int delta = 0;

        // On privilégie la composante verticale mais on accepte aussi les mouvements horizontaux
        // pour les joueurs utilisant la croix directionnelle.
        if (input.y > 0.5f || input.x < -0.5f)
            delta = -1;
        else if (input.y < -0.5f || input.x > 0.5f)
            delta = 1;

        if (delta == 0)
            return;

        int newIndex = Mathf.Clamp(focusedIndex + delta, 0, slots.Count - 1);
        if (newIndex == focusedIndex)
            return;

        focusedIndex = newIndex;
        lastNavigationTime = Time.unscaledTime;
        UpdateFocusVisuals();
    }

    /// <summary>
    /// Force la caméra du ScrollRect à afficher l'élément actuellement ciblé.
    /// </summary>
    private void ScrollToFocusedSlot()
    {
        if (scrollRect == null || contentRoot == null)
            return;

        if (focusedIndex < 0 || focusedIndex >= slots.Count)
            return;

        if (scrollRect.viewport == null)
            return;

        var target = slots[focusedIndex]?.transform as RectTransform;
        if (target == null)
            return;

        // Garantit que les tailles sont à jour avant le calcul du ratio.
        Canvas.ForceUpdateCanvases();

        float viewportHeight = scrollRect.viewport.rect.height;
        float contentHeight = contentRoot.rect.height;

        if (contentHeight <= viewportHeight)
            return; // Rien à scroller.

        // Les positions dans une ScrollView verticale sont inversées (0 en haut, positif vers le bas).
        float targetY = Mathf.Abs(target.anchoredPosition.y);
        float normalized = Mathf.Clamp01(targetY / (contentHeight - viewportHeight));
        scrollRect.verticalNormalizedPosition = 1f - normalized;
    }

    /// <summary>
    /// Met à jour l'état visuel de chaque slot en fonction du focus courant.
    /// </summary>
    private void UpdateFocusVisuals()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;

            bool active = hasFocus && i == focusedIndex;
            slots[i].SetFocus(active);
        }

        if (hasFocus)
            ScrollToFocusedSlot();
    }
}

