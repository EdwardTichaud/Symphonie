using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gère l'interface de la timeline de combat affichant l'ordre des tours.
/// Ce gestionnaire instancie et organise les <see cref="BattleTimelineUnit"/> sous
/// un conteneur placé dans la scène de bataille.
/// </summary>
public class BattleTimelineUIManager : MonoBehaviour
{
    /// <summary>
    /// Instance unique accessible globalement.
    /// </summary>
    public static BattleTimelineUIManager Instance { get; private set; }

    [Header("Références UI")]
    [SerializeField] private RectTransform timelineContainer; // Parent accueillant les vignettes, enfant de BattleScene
    [SerializeField] private GameObject timelineUnitPrefab;    // Préfabriqué de vignette

    /// <summary>
    /// Liste des vignettes actuellement affichées dans la timeline.
    /// </summary>
    private readonly List<BattleTimelineUnit> timelineUIObjects = new();

    /// <summary>
    /// Accès au conteneur pour d'éventuels réglages extérieurs.
    /// </summary>
    public RectTransform TimelineContainer => timelineContainer;

    private void Awake()
    {
        // Mise en place du singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Instancie les vignettes représentant les unités actives.
    /// </summary>
    public void Initialize(IEnumerable<CharacterUnit> characters)
    {
        Clear();

        foreach (var unit in characters)
        {
            var slot = Instantiate(timelineUnitPrefab, timelineContainer);
            var ui = slot.GetComponent<BattleTimelineUnit>();
            ui.Initialize(unit);
            timelineUIObjects.Add(ui);
        }
    }

    /// <summary>
    /// Efface toutes les vignettes existantes.
    /// </summary>
    public void Clear()
    {
        foreach (var ui in timelineUIObjects)
            if (ui != null) Destroy(ui.gameObject);
        timelineUIObjects.Clear();
    }

    /// <summary>
    /// Met en évidence l'unité actuellement active.
    /// </summary>
    public void UpdateHighlight(CharacterUnit activeUnit)
    {
        foreach (var ui in timelineUIObjects)
        {
            bool isCurrent = activeUnit != null && ui.characterData == activeUnit.Data;
            ui.SetHighlight(isCurrent);
        }
    }

    /// <summary>
    /// Réorganise la timeline pour placer l'unité active sous le curseur.
    /// </summary>
    public void UpdateWheel(CharacterUnit activeUnit)
    {
        if (activeUnit == null || timelineUIObjects.Count == 0)
            return;

        int currentIndex = timelineUIObjects.FindIndex(ui => ui.characterData == activeUnit.Data);
        if (currentIndex == -1)
            return;

        int count = timelineUIObjects.Count;

        // Place l'unité active en tête de liste
        int shift = (count - currentIndex) % count;
        for (int i = 0; i < shift; i++)
        {
            var last = timelineUIObjects[count - 1];
            timelineUIObjects.RemoveAt(count - 1);
            timelineUIObjects.Insert(0, last);
        }

        for (int i = 0; i < count; i++)
        {
            timelineUIObjects[i].transform.SetSiblingIndex(i);

            float t = count > 1 ? Mathf.Clamp01((float)i / (count - 1)) : 0f;
            float scale = Mathf.Lerp(1f, 0.8f, t);
            float alpha = Mathf.Lerp(1f, 0.2f, t);
            timelineUIObjects[i].SetAppearance(scale, alpha);
        }
    }

    /// <summary>
    /// Supprime de la timeline l'unité donnée.
    /// </summary>
    public void RemoveFromTimeline(CharacterUnit deadUnit)
    {
        var ui = timelineUIObjects.FirstOrDefault(x => x.characterData == deadUnit.Data);
        if (ui != null)
        {
            timelineUIObjects.Remove(ui);
            Destroy(ui.gameObject);
        }
    }
}
