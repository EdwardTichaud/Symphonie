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
        if (canvasGroup == null)
        {
            // Assure une valeur cohérente même si le prefab n'a pas de CanvasGroup ; dans ce cas on
            // ne fera qu'afficher/supprimer le popup sans fondu.
            Debug.LogWarning("[DamagePopup] Aucun CanvasGroup détecté, le fondu sera désactivé.");
        }

        target = followTarget;
        // Position initiale dans le monde avant la première mise à jour
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
    /// Calcule la position et l'orientation en fonction de la cible suivie et de la caméra.
    /// </summary>
    private void UpdatePosition()
    {
        if (mainCam == null || target == null)
            return;

        // Position dans le monde avec l'offset animé. On conserve un léger décalage vertical
        // pour rendre le popup lisible, tout en permettant au texte de flotter.
        Vector3 worldPos = target.position + offset + Vector3.up * floatOffset;

        // Positionne l'objet directement dans l'espace monde (utile si le Canvas est en World Space
        // ou si l'on utilise un TextMeshPro autonome). Cela nous permet ensuite d'appliquer une
        // rotation orientée vers la caméra pour un effet billboard.
        transform.position = worldPos;

        // Oriente le popup vers la caméra active. Le "billboard" est obtenu en regardant la caméra
        // puis en alignant l'axe "up" sur celui du monde pour éviter les rotations inversées.
        Vector3 toCamera = mainCam.transform.position - transform.position;
        Vector3 cameraUp = mainCam.transform.up;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            // Quaternion.LookRotation nécessite une direction non nulle ; on vérifie donc la distance
            // caméra → popup pour éviter les NaN. L'axe "up" de la caméra est conservé pour limiter
            // les rotations parasites lorsque la caméra effectue des roulis.
            transform.rotation = Quaternion.LookRotation(toCamera.normalized, cameraUp);
        }
    }
}
