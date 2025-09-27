using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adapte l'affichage de la timeline de combat au système standard de <see cref="HPBar"/>.
/// Cette passerelle permet d'affecter dynamiquement la barre de vie timeline à un
/// <see cref="CharacterUnit"/> sans perdre l'éventuelle barre déjà configurée sur l'unité.
/// </summary>
[RequireComponent(typeof(BattleTimelineUnit))]
public class TimelineHPBarAdapter : HPBar
{
    [Header("Références visuelles")]
    [SerializeField] private Image timelineFillImage;
    [SerializeField] private TextMeshProUGUI timelineHPText;
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = Color.red;

    /// <summary>Barre éventuellement déjà assignée sur l'unité (ex : interface 3D).</summary>
    private HPBar fallbackBar;
    /// <summary>Référence vers l'unité dont on suit les points de vie.</summary>
    private CharacterUnit owner;
    /// <summary>Indique si la barre de secours a déjà été mémorisée.</summary>
    private bool fallbackCaptured;
    /// <summary>Valeur maximale actuelle utilisée pour le calcul du ratio.</summary>
    private float cachedMaxValue = 1f;
    /// <summary>Valeur courante retenue pour l'affichage.</summary>
    private float cachedCurrentValue = 1f;

    /// <summary>
    /// Configure l'adaptateur pour qu'il prenne le relais sur l'unité donnée.
    /// </summary>
    /// <param name="target">Unité dont on souhaite surveiller les PV.</param>
    /// <param name="fill">Image représentant le remplissage de la jauge.</param>
    /// <param name="hpLabel">Label textuel affichant les valeurs numériques.</param>
    /// <param name="alive">Couleur utilisée lorsque l'unité est en vie.</param>
    /// <param name="dead">Couleur utilisée lorsque l'unité est vaincue.</param>
    public void Setup(CharacterUnit target, Image fill, TextMeshProUGUI hpLabel, Color alive, Color dead)
    {
        owner = target;
        timelineFillImage = fill != null ? fill : timelineFillImage;
        timelineHPText = hpLabel != null ? hpLabel : timelineHPText;
        aliveColor = alive;
        deadColor = dead;

        if (owner == null)
            return;

        // Mémorise la barre existante avant de prendre la main afin de la restaurer plus tard.
        if (!fallbackCaptured && owner.hpBar != null && owner.hpBar != this)
        {
            fallbackBar = owner.hpBar;
            fallbackCaptured = true;
        }

        // Affecte cette barre comme référence principale pour les prochains dégâts/soins.
        owner.hpBar = this;
    }

    /// <inheritdoc />
    public override void SetMaxValue(float max)
    {
        cachedMaxValue = Mathf.Max(0f, max);
        // Propage la nouvelle limite vers l'ancienne barre si elle existe encore.
        fallbackBar?.SetMaxValue(max);

        // Utilise la valeur courante connue de l'unité pour maintenir la cohérence visuelle.
        float current = owner != null ? owner.currentHP : cachedCurrentValue;
        UpdateVisuals(current);
    }

    /// <inheritdoc />
    public override void SetValue(float current)
    {
        cachedCurrentValue = Mathf.Clamp(current, 0f, cachedMaxValue > 0f ? cachedMaxValue : current);
        fallbackBar?.SetValue(cachedCurrentValue);
        UpdateVisuals(cachedCurrentValue);
    }

    /// <summary>
    /// Met à jour l'affichage immédiatement sans toucher aux barres fallback.
    /// Utile lors de la phase d'initialisation de la timeline.
    /// </summary>
    /// <param name="current">Valeur actuelle des PV.</param>
    /// <param name="max">Valeur maximale des PV.</param>
    public void UpdateInstant(float current, float max)
    {
        cachedMaxValue = Mathf.Max(0f, max);
        cachedCurrentValue = Mathf.Clamp(current, 0f, cachedMaxValue > 0f ? cachedMaxValue : current);
        UpdateVisuals(cachedCurrentValue);
    }

    /// <summary>
    /// Rafraîchit les éléments visuels (remplissage + texte) selon les valeurs calculées.
    /// </summary>
    /// <param name="current">Nombre de PV à afficher.</param>
    private void UpdateVisuals(float current)
    {
        float ratio = cachedMaxValue > 0f ? current / cachedMaxValue : 0f;
        ratio = Mathf.Clamp01(ratio);

        if (timelineFillImage != null)
        {
            timelineFillImage.fillAmount = ratio;
            timelineFillImage.color = current <= 0f ? deadColor : aliveColor;
        }

        if (timelineHPText != null)
        {
            int currentInt = Mathf.RoundToInt(current);
            int maxInt = Mathf.RoundToInt(cachedMaxValue);
            timelineHPText.text = $"{currentInt}/{maxInt}";
            timelineHPText.color = current <= 0f ? deadColor : aliveColor;
        }
    }

    private void OnDestroy()
    {
        // Lorsque la timeline est détruite, on restitue la barre précédente pour éviter
        // de laisser l'unité sans référence valide (cas d'une nouvelle scène de combat).
        if (owner != null && owner.hpBar == this)
        {
            owner.hpBar = fallbackBar;

            if (fallbackBar != null)
            {
                // Synchronise la barre restaurée avec les valeurs les plus récentes.
                fallbackBar.SetMaxValue(cachedMaxValue);
                fallbackBar.SetValue(cachedCurrentValue);
            }
        }
    }
}
