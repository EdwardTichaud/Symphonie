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

    [Header("Couleurs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color deadColor = Color.red;

    [HideInInspector] public CharacterData characterData;

    public void Initialize(CharacterUnit unit)
    {
        if (unit == null || unit.Data == null)
        {
            Debug.LogWarning("[TimelineUnit] Unité invalide.");
            return;
        }

        characterData = unit.Data;

        // Portrait et nom
        if (portraitImage) portraitImage.sprite = characterData.portrait;
        if (nameText) nameText.text = characterData.characterName;

        // HP
        UpdateHPBar();

        // Custom bar (Rage/Fatigue)
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
        }

        // ATB
        if (atbSlider != null)
        {
            atbSlider.maxValue = unit.ATBMax;
            atbSlider.value = unit.currentATB;
        }

        SetHighlight(false);
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
    }

    public void UpdateHPBar()
    {
        if (characterData == null || hpBarImage == null || hpText == null) return;

        float ratio = Mathf.Clamp01((float)characterData.currentHP / characterData.baseHP);
        hpBarImage.fillAmount = ratio;
        hpText.text = $"{characterData.currentHP}/{characterData.baseHP}";

        bool isDead = characterData.currentHP <= 0;
        hpText.color = isDead ? deadColor : Color.white;
        hpBarImage.color = isDead ? deadColor : Color.white;
    }

    public void SetHighlight(bool active)
    {
        if (backgroundImage != null)
            backgroundImage.color = active ? highlightColor : normalColor;

        transform.localScale = active ? Vector3.one * 1.15f : Vector3.one;
    }
}
