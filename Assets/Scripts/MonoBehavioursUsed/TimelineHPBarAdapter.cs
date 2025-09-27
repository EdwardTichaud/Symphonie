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
    [Header("Couleurs dynamiques de PV")]
    [SerializeField] private Color highHealthColor = Color.green; // Couleur utilisée lorsque la jauge est presque pleine.
    [SerializeField] private Color midHealthColor = Color.yellow; // Couleur d'avertissement pour une santé moyenne.
    [SerializeField] private Color lowHealthColor = new Color(0.75f, 0f, 0f); // Rouge légèrement assombri pour distinguer l'état critique.

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

        // --- Référence Slider ---------------------------------------------------------------
        // Certains prefabs n'assignent pas explicitement le Slider Unity utilisé par la barre de vie.
        // Lorsque ce champ reste à "null", la classe parente <see cref="HPBar"/> n'a aucun
        // composant à synchroniser : le slider visible en jeu reste bloqué à sa valeur maximale
        // (ex : 1f) même si les points de vie diminuent correctement.  Afin de fiabiliser la
        // configuration, on tente automatiquement de retrouver le slider correspondant dans la
        // hiérarchie de l'objet timeline.
        if (slider == null)
        {
            // On privilégie d'abord la recherche via l'image de remplissage car elle est en
            // général un enfant direct du slider, puis on élargit au besoin à tous les enfants.
            if (timelineFillImage != null)
            {
                slider = timelineFillImage.GetComponentInParent<Slider>();
            }

            if (slider == null)
            {
                slider = GetComponentInChildren<Slider>(includeInactive: true);
            }
        }

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

        // Détermine la couleur adaptée avant d'appliquer les nouveaux ratios sur les éléments visuels.
        Color displayColor = DetermineHealthColor(ratio, current);

        if (timelineFillImage != null)
        {
            timelineFillImage.fillAmount = ratio;
            timelineFillImage.color = displayColor;
        }

        if (timelineHPText != null)
        {
            int currentInt = Mathf.RoundToInt(current);
            int maxInt = Mathf.RoundToInt(cachedMaxValue);
            timelineHPText.text = $"{currentInt}/{maxInt}";
            timelineHPText.color = displayColor;
        }
    }

    /// <summary>
    /// Calcule la couleur à afficher selon le ratio de PV actuel.
    /// Garantit un dégradé rouge → jaune → vert pour informer visuellement du danger.
    /// </summary>
    /// <param name="ratio">Ratio de points de vie entre 0 et 1.</param>
    /// <param name="current">Valeur actuelle brute des PV pour détecter l'état KO.</param>
    /// <returns>Couleur à utiliser pour la jauge et le texte.</returns>
    private Color DetermineHealthColor(float ratio, float current)
    {
        // Dès que les PV tombent à zéro ou moins on force l'utilisation de la couleur « mort ».
        if (current <= 0f)
            return deadColor;

        // Ratio très faible : mélange entre le rouge foncé et le jaune afin de souligner le danger.
        if (ratio <= 0.5f)
        {
            float t = Mathf.InverseLerp(0f, 0.5f, ratio);
            return Color.Lerp(lowHealthColor, midHealthColor, t);
        }

        // Ratio supérieur à la moitié : transition douce du jaune vers le vert au fur et à mesure que l'on se rapproche du maximum.
        float highT = Mathf.InverseLerp(0.5f, 1f, ratio);
        Color progressive = Color.Lerp(midHealthColor, highHealthColor, highT);

        // On laisse la possibilité de conserver une teinte personnalisée définie via aliveColor pour les cas non critiques.
        return Color.Lerp(progressive, aliveColor, 0.15f);
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
