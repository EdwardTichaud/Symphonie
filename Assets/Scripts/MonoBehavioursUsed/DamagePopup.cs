using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float duration = 1.5f;
    public Vector3 offset = new Vector3(0, 2f, 0);

    [Header("Références")]
    public TextMeshProUGUI textMesh; // Référence au texte affichant le montant

    private float elapsed = 0f;        // Temps écoulé depuis l'initialisation pour piloter le fondu
    private float floatOffset = 0f;    // Décalage vertical cumulé appliqué au centre de l'écran
    private CanvasGroup canvasGroup;   // Permet de faire disparaître progressivement le popup
    private RectTransform rectTransform; // Accès rapide au RectTransform pour manipuler l'overlay
    private Vector2 baseAnchoredPosition; // Position de référence (en pixels) par rapport au centre du Canvas
    private Vector3 baseWorldPosition;    // Fallback si le popup est utilisé hors Canvas (World Space par exemple)

    /// <summary>
    /// Initialise le popup avec un montant et la cible à suivre.
    /// </summary>
    /// <param name="amount">Montant de dégâts à afficher.</param>
    /// <param name="followTarget">Transform de l'unité concernée (désormais ignoré car l'affichage est centré).</param>
    public void Initialize(int amount, Transform followTarget)
    {
        textMesh.text = amount.ToString();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            // Assure une valeur cohérente même si le prefab n'a pas de CanvasGroup ; dans ce cas on
            // ne fera qu'afficher/supprimer le popup sans fondu.
            Debug.LogWarning("[DamagePopup] Aucun CanvasGroup détecté, le fondu sera désactivé.");
        }

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Mémorise la position initiale afin de pouvoir y revenir si l'on souhaite modifier
            // dynamiquement la mise en page depuis l'éditeur. En pratique, nous centrons le popup
            // sur l'écran pour obtenir un vrai overlay.
            baseAnchoredPosition = Vector2.zero;
            rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(offset.x, offset.y);
            rectTransform.localScale = Vector3.one; // Garantit l'absence de distorsion héritée du parent.
        }
        else
        {
            // Cas de secours si le prefab est encore utilisé dans un contexte "World Space".
            // On se base alors sur la position locale de l'objet pour assurer un centrage relatif.
            baseWorldPosition = Vector3.zero;
            transform.localPosition = baseWorldPosition + new Vector3(offset.x, offset.y, offset.z);
        }

        // Position initiale avant la première mise à jour (indispensable pour appliquer l'offset).
        UpdatePosition();
    }

    void Update()
    {
        // Avancement de l'animation verticale
        floatOffset += floatSpeed * Time.unscaledDeltaTime; // Mouvement indépendant du timeScale pour conserver la lisibilité du popup.

        // Met à jour la position à chaque frame
        UpdatePosition();

        // Fade out après 'duration'
        elapsed += Time.unscaledDeltaTime; // Durée calculée en temps réel afin que le popup disparaisse même en pause.
        if (elapsed >= duration)
        {
            if (canvasGroup != null)
            {
                // Transition progressive vers la transparence afin d'éviter une disparition abrupte.
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            }

            // Lorsque l'opacité a disparu (ou qu'aucun CanvasGroup n'est présent), on détruit le popup.
            if (canvasGroup == null || canvasGroup.alpha <= 0f)
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// Calcule la position et l'orientation du popup d'écran.
    /// </summary>
    private void UpdatePosition()
    {
        if (rectTransform != null)
        {
            // En mode Overlay (Canvas en Screen Space), on applique un décalage vertical progressif
            // afin de donner une impression de mouvement vers le haut tout en restant au centre.
            Vector2 verticalDisplacement = new Vector2(0f, floatOffset);
            Vector2 staticOffset = new Vector2(offset.x, offset.y);
            rectTransform.anchoredPosition = baseAnchoredPosition + staticOffset + verticalDisplacement;
        }
        else
        {
            // Fallback pour les anciens prefabs en World Space : on reproduit un comportement
            // similaire en manipulant la position locale sans dépendre d'une caméra.
            Vector3 worldOffset = new Vector3(offset.x, offset.y + floatOffset, offset.z);
            transform.localPosition = baseWorldPosition + worldOffset;
        }
    }
}
