using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleTimelineUnit : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image hpBarImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider atbSlider;
    [SerializeField] private CustomBar customBar;
    [SerializeField] private TextMeshProUGUI harmonicsText;

    [Header("Couleurs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color deadColor = Color.red;

    [HideInInspector] public CharacterData characterData;

    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>Référence directe vers l'unité affichée pour faciliter l'accès aux stats dynamiques.</summary>
    private CharacterUnit boundUnit;
    /// <summary>Adaptateur reliant la barre de vie de la timeline au CharacterUnit.</summary>
    private TimelineHPBarAdapter timelineHPBarAdapter;

    private float baseScale = 1f;
    private float highlightMultiplier = 1f;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Initialize(CharacterUnit unit)
    {
        if (unit == null || unit.Data == null)
        {
            Debug.LogWarning("[TimelineUnit] Unité invalide.");
            return;
        }

        characterData = unit.Data;
        boundUnit = unit;

        // Portrait et nom
        if (portraitImage) portraitImage.sprite = characterData.portrait;
        if (nameText) nameText.text = characterData.characterName;

        // HP : on crée/initialise l'adaptateur qui mettra à jour la jauge timeline
        if (timelineHPBarAdapter == null)
            timelineHPBarAdapter = GetComponent<TimelineHPBarAdapter>();

        if (timelineHPBarAdapter == null)
            timelineHPBarAdapter = gameObject.AddComponent<TimelineHPBarAdapter>();

        Color aliveColor = hpBarImage != null ? hpBarImage.color : Color.white;
        timelineHPBarAdapter.Setup(unit, hpBarImage, hpText, aliveColor, deadColor);

        float maxHP = unit != null
            ? unit.Data.baseHP + unit.currentVitality
            : characterData.baseHP + characterData.currentVitality;
        timelineHPBarAdapter.SetMaxValue(maxHP);
        timelineHPBarAdapter.SetValue(unit != null ? unit.currentHP : characterData.currentHP);

        // Custom bar (Rage/Fatigue/Concentration)
        if (customBar != null)
        {
            if (characterData.gameplayType == GameplayType.Rage)
            {
                customBar.SetMaxValue(characterData.maxRage);
                customBar.SetValue(unit.currentRage);
            }
            else if (characterData.gameplayType == GameplayType.Fatigue)
            {
                customBar.SetMaxValue(characterData.maxFatigue);
                customBar.SetValue(unit.currentFatigue);
            }
            else if (unit.TryGetComponent<ConcentrationSystem>(out var c))
            {
                customBar.SetMaxValue(c.maxConcentration);
                customBar.SetValue(c.currentConcentration);
            }
        }

        // ATB
        if (atbSlider != null)
        {
            atbSlider.maxValue = unit.ATBMax;
            atbSlider.value = unit.currentATB;
        }

        UpdateHarmonicsDisplay();

        SetHighlight(false);
    }

    void Update()
    {
        UpdateHarmonicsDisplay();
    }

    public void UpdateATBGauge()
    {
        var unit = NewBattleManager.Instance.activeCharacterUnits
            .Find(u => u.Data == characterData);

        if (unit != null && atbSlider != null)
        {
            atbSlider.value = unit.currentATB;
        }
    }

    public void UpdateCustomBar()
    {
        var unit = NewBattleManager.Instance.activeCharacterUnits
            .Find(u => u.Data == characterData);

        if (unit == null || customBar == null)
            return;

        if (characterData.gameplayType == GameplayType.Rage)
        {
            customBar.SetValue(unit.currentRage);
        }
        else if (characterData.gameplayType == GameplayType.Fatigue)
        {
            customBar.SetValue(unit.currentFatigue);
        }
        else if (unit.TryGetComponent<ConcentrationSystem>(out var c))
        {
            customBar.SetValue(c.currentConcentration);
        }
    }

    public void UpdateHarmonicsDisplay()
    {
        var unit = NewBattleManager.Instance.activeCharacterUnits
            .Find(u => u.Data == characterData);

        if (unit == null || harmonicsText == null)
            return;

        int count = unit.GetHarmonicCount(unit.Data.harmonicType);
        harmonicsText.text = count.ToString();
    }

    public void UpdateHPBar()
    {
        if (timelineHPBarAdapter == null)
            return;

        if (boundUnit != null && boundUnit.Data != null)
        {
            float maxHP = boundUnit.Data.baseHP + boundUnit.currentVitality;
            timelineHPBarAdapter.UpdateInstant(boundUnit.currentHP, maxHP);
        }
        else if (characterData != null)
        {
            float maxHP = characterData.baseHP + characterData.currentVitality;
            timelineHPBarAdapter.UpdateInstant(characterData.currentHP, maxHP);
        }
    }

    public void SetHighlight(bool active)
    {
        if (backgroundImage != null)
            backgroundImage.color = active ? highlightColor : normalColor;

        highlightMultiplier = active ? 1.15f : 1f;
        ApplyScale();
    }

    public void SetAppearance(float scale, float alpha)
    {
        baseScale = scale;
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        ApplyScale();
    }

    private void ApplyScale()
    {
        transform.localScale = Vector3.one * baseScale * highlightMultiplier;
    }
}

