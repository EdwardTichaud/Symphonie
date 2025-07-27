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

    private float elapsed = 0f;      // Temps écoulé depuis l'initialisation
    private float floatOffset = 0f;   // Décalage vertical cumulé
    private Camera mainCam;           // Caméra principale utilisée pour la conversion
    private CanvasGroup canvasGroup;  // Permet de faire disparaître progressivement le popup
    private Transform target;         // Unité suivie

    /// <summary>
    /// Initialise le popup avec un montant et la cible à suivre.
    /// </summary>
    /// <param name="amount">Montant de dégâts à afficher.</param>
    /// <param name="followTarget">Transform de l'unité concernée.</param>
    public void Initialize(int amount, Transform followTarget)
    {
        textMesh.text = amount.ToString();
        mainCam = Camera.main;
        canvasGroup = GetComponent<CanvasGroup>();

        target = followTarget;
        // Position initiale sur l'écran
        UpdatePosition();
    }

    void Update()
    {
        // Avancement de l'animation verticale
        floatOffset += floatSpeed * Time.deltaTime;

        // Met à jour la position à chaque frame
        UpdatePosition();

        // Fade out après 'duration'
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            if (canvasGroup.alpha <= 0f)
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// Calcule la position à l'écran en fonction de la cible suivie.
    /// </summary>
    private void UpdatePosition()
    {
        if (mainCam == null || target == null)
            return;

        // Position dans le monde avec l'offset animé
        Vector3 worldPos = target.position + offset + Vector3.up * floatOffset;

        // Conversion monde → écran
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

        // Clamp pour rester visible même hors cadre
        screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
        screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);

        // Affecte directement la position de la RectTransform (Canvas en Screen Space)
        transform.position = screenPos;
    }
}
