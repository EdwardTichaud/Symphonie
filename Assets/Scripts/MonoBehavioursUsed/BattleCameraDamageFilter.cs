using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applique un filtre plein écran dont l'opacité reflète l'état de santé
/// de la <see cref="CharacterUnit"/> actuellement active côté escouade.
/// Plus la jauge de vie de l'unité active est basse, plus le filtre devient
/// opaque afin de renforcer la tension dramatique, en écho aux dangers
/// grandissants décrits dans l'Histoire de Symphonie.
/// </summary>
[DisallowMultipleComponent]
public class BattleCameraDamageFilter : MonoBehaviour
{
    /// <summary>Instance unique accessible depuis le <see cref="BattleCameraManager"/>.</summary>
    public static BattleCameraDamageFilter Instance { get; private set; }

    [Header("Références visuelles")]
    [Tooltip("Composant UI portant la texture du filtre (Image, RawImage, etc.).")]
    [SerializeField] private Graphic uiGraphic;

    [Tooltip("SpriteRenderer optionnel si le filtre est géré via un quad 2D.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Paramètres d'opacité")]
    [Tooltip("Opacité minimale conservée même quand l'unité est en pleine forme.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumOpacity = 0f;

    [Tooltip("Opacité maximale atteinte quand l'unité est au plus mal.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumOpacity = 0.85f;

    [Tooltip("Courbe appliquée sur le pourcentage de dégâts subis (0 = aucun dégât, 1 = HP à 0).")]
    [SerializeField] private AnimationCurve opacityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Dynamique de transition")]
    [Tooltip("Vitesse de transition vers la nouvelle opacité (valeur plus haute = adaptation plus rapide).")]
    [Min(0f)]
    [SerializeField] private float fadeSpeed = 4f;

    /// <summary>Unité actuellement considérée comme propriétaire du tour.</summary>
    private CharacterUnit activeUnit;

    /// <summary>Opacité réellement affichée à l'image.</summary>
    private float displayedOpacity;

    /// <summary>Vrai si aucune référence visuelle n'a été trouvée.</summary>
    private bool hasLoggedMissingRenderer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 🔄 Récupère automatiquement les composants visuels si l'utilisateur
        // oublie de les assigner, afin de réduire les risques d'erreur.
        if (uiGraphic == null)
            uiGraphic = GetComponent<Graphic>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyOpacityImmediate(0f); // On démarre l'effet totalement transparent.
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // ⏱️ Utilisation du delta temps non affecté par les ralentis pour conserver
        // une sensation cohérente, même lors des cinématiques en slow motion.
        float targetOpacity = ComputeTargetOpacity();
        displayedOpacity = Mathf.MoveTowards(displayedOpacity, targetOpacity, Time.unscaledDeltaTime * fadeSpeed);
        ApplyOpacityImmediate(displayedOpacity);
    }

    /// <summary>
    /// Déclare l'unité qui pilote actuellement le tour de l'escouade.
    /// Un appel avec <c>null</c> ou une unité ennemie annule purement l'effet.
    /// </summary>
    /// <param name="unit">Nouvelle unité prioritaire.</param>
    public void SetActiveUnit(CharacterUnit unit)
    {
        activeUnit = unit;

        // Mise à jour immédiate pour éviter tout clignotement entre deux tours.
        displayedOpacity = ComputeTargetOpacity();
        ApplyOpacityImmediate(displayedOpacity);
    }

    /// <summary>
    /// Calcule l'opacité désirée à partir de l'état de santé de l'unité active.
    /// </summary>
    private float ComputeTargetOpacity()
    {
        if (activeUnit == null || activeUnit.Data == null)
            return 0f; // 🔇 Aucun propriétaire de tour : pas d'effet visuel.

        if (!activeUnit.Data.isPlayerControlled)
            return 0f; // 🎯 L'effet ne concerne que les SquadUnits du joueur.

        float maxHP = Mathf.Max(0f, activeUnit.Data.baseHP + activeUnit.currentVitality);
        if (maxHP <= 0.0001f)
            return 0f; // ⚠️ Valeur de référence invalide, on reste transparent.

        float healthRatio = Mathf.Clamp01(activeUnit.currentHP / maxHP); // 1 = pleine vie, 0 = KO.
        float damageRatio = 1f - healthRatio; // 0 = sain, 1 = en péril.

        float curveValue = opacityCurve != null && opacityCurve.length > 0
            ? opacityCurve.Evaluate(Mathf.Clamp01(damageRatio))
            : damageRatio;

        float normalized = Mathf.Clamp01(curveValue);
        return Mathf.Lerp(minimumOpacity, maximumOpacity, normalized);
    }

    /// <summary>
    /// Applique immédiatement l'opacité calculée sur toutes les références connues.
    /// </summary>
    /// <param name="opacity">Opacité comprise entre 0 et 1.</param>
    private void ApplyOpacityImmediate(float opacity)
    {
        if (uiGraphic == null && spriteRenderer == null && !hasLoggedMissingRenderer)
        {
            Debug.LogWarning(
                "[BattleCameraDamageFilter] Aucun composant visuel assigné. " +
                "Veuillez renseigner une Image UI ou un SpriteRenderer.");
            hasLoggedMissingRenderer = true;
        }

        if (uiGraphic != null)
        {
            Color color = uiGraphic.color;
            color.a = opacity;
            uiGraphic.color = color;
        }

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = opacity;
            spriteRenderer.color = color;
        }
    }
}
