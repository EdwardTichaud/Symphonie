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

        // Dès l'initialisation, on tente de trier la liste selon l'ordre
        // de passage prévu afin d'éviter un affichage incohérent si des
        // valeurs d'ATB étaient déjà présentes (cas de reprises ou tests).
        SortTimelineByATB();
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
    /// Calcule le nombre d'itérations nécessaires pour qu'une unité soit prête
    /// à jouer en reproduisant la logique de <see cref="NewBattleManager"/>.
    /// </summary>
    private float EstimateTurnsToReady(CharacterUnit unit)
    {
        if (unit == null || unit.currentInitiative <= 0f)
            return float.MaxValue;

        // Reste d'ATB à parcourir avant d'atteindre le seuil de déclenchement.
        float remaining = Mathf.Max(0f, unit.ATBMax - unit.currentATB);
        return remaining / unit.currentInitiative;
    }

    /// <summary>
    /// Trie les vignettes de la timeline selon l'ordre réel de passage calculé
    /// à partir des jauges d'ATB et de l'initiative de chaque unité.
    /// </summary>
    private void SortTimelineByATB()
    {
        if (NewBattleManager.Instance == null)
            return;

        timelineUIObjects.Sort((a, b) =>
        {
            var unitA = NewBattleManager.Instance.activeCharacterUnits
                .Find(u => u.Data == a.characterData);
            var unitB = NewBattleManager.Instance.activeCharacterUnits
                .Find(u => u.Data == b.characterData);

            float turnsA = EstimateTurnsToReady(unitA);
            float turnsB = EstimateTurnsToReady(unitB);
            return turnsA.CompareTo(turnsB);
        });
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
    /// Réorganise la timeline pour placer l'unité active en première position et
    /// afficher ensuite les suivantes selon l'ordre réel de passage.
    /// </summary>
    public void UpdateWheel(CharacterUnit activeUnit)
    {
        if (timelineUIObjects.Count == 0)
            return;

        // Trie préalablement la liste pour refléter l'ordre de passage calculé.
        SortTimelineByATB();

        if (activeUnit != null)
        {
            // S'assure que l'unité actuellement active est bien en tête.
            int index = timelineUIObjects.FindIndex(ui => ui.characterData == activeUnit.Data);
            if (index > 0)
            {
                var ui = timelineUIObjects[index];
                timelineUIObjects.RemoveAt(index);
                timelineUIObjects.Insert(0, ui);
            }
        }

        // Met à jour l'apparence et l'ordre des vignettes dans la hiérarchie.
        int count = timelineUIObjects.Count;
        for (int i = 0; i < count; i++)
        {
            timelineUIObjects[i].transform.SetSiblingIndex(i);

            float t = count > 1 ? Mathf.Clamp01((float)i / (count - 1)) : 0f;
            float scale = Mathf.Lerp(1f, 0.8f, t);
            float alpha = Mathf.Lerp(1f, 0.2f, t);
            timelineUIObjects[i].SetAppearance(scale, alpha);
            // Met à jour la jauge d'ATB pour refléter l'avancée réelle.
            timelineUIObjects[i].UpdateATBGauge();
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
