using UnityEngine;

/// <summary>
///     Composant attaché à une unité marquée par « Pour qui sonne le glas ».
///     Il relie la victime à son protecteur et gère à la fois la redirection
///     des dégâts et l'affichage visuel de la marque.
/// </summary>
public class LoyaltyMark : MonoBehaviour
{
    /// <summary>Référence de l'unité qui encaisse les dégâts à la place de la cible.</summary>
    public CharacterUnit protector;

    // Décalage vertical appliqué au visuel pour flotter légèrement au-dessus
    // de la tête de l'unité. Valeur ajustable à l'initialisation.
    private float visualOffset = DefaultVisualOffset;

    // Mise en cache de l'unité porteuse : on évite ainsi des GetComponent
    // répétés lors des recalculs de position.
    private CharacterUnit owner;

    // Instance runtime du prefab de marque, instanciée lors de l'application
    // de l'effet. Elle est détruite automatiquement quand le composant est retiré.
    private GameObject visualInstance;

    // Référence du prefab actuellement utilisé, utile pour savoir si l'on doit
    // ré-instancier un nouveau visuel (par exemple si le designer change le FX).
    private GameObject currentPrefab;

    // Échelle initiale du prefab pour respecter le calibrage artistique lors des repositionnements.
    private Vector3 initialVisualScale = Vector3.one;

    // Valeur de repli utilisée quand aucun volume visuel n'est disponible.
    private const float DefaultHeightFallback = 1.5f;

    // Décalage par défaut utilisé si le move n'en fournit pas un explicitement.
    private const float DefaultVisualOffset = 0.5f;

    private void Awake()
    {
        // Mise en cache de l'unité porteuse pour accélérer tous les calculs ultérieurs.
        owner = GetComponent<CharacterUnit>();
    }

    private void LateUpdate()
    {
        // Maintient le visuel aligné au sommet du personnage si la taille change
        // (animations, variations d'échelle temporaires, etc.).
        UpdateVisualTransform();
    }

    /// <summary>
    ///     Enregistre le protecteur et prépare éventuellement le visuel associé.
    /// </summary>
    /// <param name="unit">Unité qui prendra les dégâts à la place de la cible.</param>
    /// <param name="markPrefab">Prefab instancié au-dessus de la victime.</param>
    /// <param name="verticalOffset">Décalage supplémentaire appliqué au visuel.</param>
    public void SetProtector(CharacterUnit unit, GameObject markPrefab = null, float verticalOffset = DefaultVisualOffset)
    {
        protector = unit;
        visualOffset = Mathf.Max(0f, verticalOffset);

        if (markPrefab == null)
        {
            // Aucun prefab fourni : on supprime l'éventuel visuel existant pour éviter
            // de laisser une icône obsolète au-dessus de la cible.
            RemoveVisual();
            return;
        }

        EnsureVisual(markPrefab);
    }

    /// <summary>
    ///     Redirige les dégâts vers le protecteur si les conditions sont réunies.
    /// </summary>
    /// <param name="amount">Montant de dégâts initialement destiné à la victime.</param>
    /// <returns>Vrai si la redirection a bien été effectuée.</returns>
    public bool RedirectDamage(float amount)
    {
        // Aucun protecteur valide ou protecteur déjà hors combat : on ne redirige pas
        if (protector == null || protector.currentHP <= 0)
            return false;

        // On inflige la moitié des dégâts au protecteur sans déclencher à nouveau
        // la redirection pour éviter une récursion infinie.
        protector.TakeDamage(amount * 0.5f, transform, false);
        return true;
    }

    private void EnsureVisual(GameObject markPrefab)
    {
        // Si le prefab a changé, on supprime la version précédente avant d'en instancier une nouvelle.
        if (visualInstance != null && currentPrefab != markPrefab)
        {
            RemoveVisual();
        }

        if (visualInstance == null)
        {
            // Instancie le prefab comme enfant de l'unité afin qu'il suive automatiquement ses déplacements.
            visualInstance = Instantiate(markPrefab, transform);
            visualInstance.name = $"{markPrefab.name}_Runtime";
            currentPrefab = markPrefab;
            initialVisualScale = visualInstance.transform.localScale;
        }

        UpdateVisualTransform();
    }

    private void UpdateVisualTransform()
    {
        if (visualInstance == null)
            return;

        // Calcule la position idéale en se basant sur les bounds visuels de l'unité.
        Vector3 anchorPosition = ComputeAnchorPosition();
        Transform visualTransform = visualInstance.transform;

        visualTransform.position = anchorPosition;
        visualTransform.rotation = Quaternion.identity; // On garde le visuel orienté de façon neutre.
        visualTransform.localScale = initialVisualScale; // Préserve l'échelle configurée sur le prefab.
    }

    private Vector3 ComputeAnchorPosition()
    {
        // Si l'unité possède un volume visuel fiable, on place la marque juste au-dessus de son sommet.
        if (owner != null)
        {
            Bounds bounds = owner.GetVisualBounds();
            float topY = bounds.center.y + bounds.extents.y;
            return new Vector3(transform.position.x, topY + visualOffset, transform.position.z);
        }

        // Fallback : on part de la position de base de l'unité.
        return transform.position + Vector3.up * (DefaultHeightFallback + visualOffset);
    }

    private void RemoveVisual()
    {
        if (visualInstance == null)
            return;

        Destroy(visualInstance);
        visualInstance = null;
        currentPrefab = null;
        initialVisualScale = Vector3.one;
    }

    private void OnDestroy()
    {
        // Nettoie l'éventuel visuel généré dynamiquement pour éviter les GameObjects orphelins.
        RemoveVisual();
    }
}
