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
    private float floatOffset = 0f;    // Décalage vertical cumulé appliqué au-dessus de la cible
    private CanvasGroup canvasGroup;   // Permet de faire disparaître progressivement le popup
    private DamagePopupManager owner;
    private Transform target;
    private CharacterUnit targetUnit;
    private Camera battleCamera;

    public void SetOwner(DamagePopupManager manager)
    {
        owner = manager;
    }

    /// <summary>
    /// Initialise le popup avec un texte et la cible à suivre.
    /// </summary>
    /// <param name="text">Texte à afficher (dégâts, soins, buff...).</param>
    /// <param name="followTarget">Transform de l'unité concernée.</param>
    /// <param name="cameraOverride">Caméra utilisée pour la conversion Monde/Ecran.</param>
    /// <param name="textColor">Couleur du texte.</param>
    public void Initialize(string text, Transform followTarget, Camera cameraOverride, Color textColor)
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh == null)
            {
                Debug.LogError("[DamagePopup] Aucun TextMeshProUGUI trouvé sur le prefab.");
                Release();
                return;
            }
        }

        textMesh.text = text;
        textMesh.color = textColor;

        target = followTarget;
        if (target == null)
        {
            Debug.LogWarning("[DamagePopup] Cible manquante, affichage annulé.");
            Release();
            return;
        }

        targetUnit = target.GetComponent<CharacterUnit>() ?? target.GetComponentInParent<CharacterUnit>();

        battleCamera = cameraOverride != null ? cameraOverride : ResolveBattleCamera();
        if (battleCamera == null)
        {
            Debug.LogWarning("[DamagePopup] Aucune caméra disponible pour positionner le popup.");
            Release();
            return;
        }

        canvasGroup ??= GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        elapsed = 0f;
        floatOffset = 0f;
        canvasGroup.alpha = 1f;

        // Position initiale avant la première mise à jour (indispensable pour appliquer l'offset).
        UpdatePosition();
    }

    void Update()
    {
        // Avancement de l'animation verticale
        // Le popup doit flotter même lorsque le temps de jeu est figé (pause, ralenti).
        floatOffset += floatSpeed * Time.unscaledDeltaTime;

        // Met à jour la position à chaque frame
        UpdatePosition();

        // Fade out après 'duration'
        // Les effets d'interface utilisent le temps non-scalé pour rester visibles en pause.
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            if (canvasGroup != null)
            {
                // Transition progressive vers la transparence afin d'éviter une disparition abrupte.
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            }

            // Lorsque l'opacité a disparu (ou qu'aucun CanvasGroup n'est présent), on détruit le popup.
            if (canvasGroup == null || canvasGroup.alpha <= 0f)
                Release();
        }
    }

    /// <summary>
    /// Calcule la position et l'orientation du popup d'écran.
    /// </summary>
    private void UpdatePosition()
    {
        if (battleCamera == null || target == null)
            return;

        Vector3 anchor = target.position;
        if (targetUnit != null)
        {
            Bounds bounds = targetUnit.GetVisualBounds();
            anchor = bounds.center + Vector3.up * bounds.extents.y;
        }

        Vector3 worldPos = anchor + offset + Vector3.up * floatOffset;
        Vector3 screenPos = battleCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f)
            return;

        screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
        screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);
        transform.position = screenPos;
    }

    private void Release()
    {
        if (owner != null)
            owner.ReleasePopup(this);
        else
            Destroy(gameObject);
    }

    private Camera ResolveBattleCamera()
    {
        GameObject battleCameraGO = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCameraGO != null)
            return battleCameraGO.GetComponent<Camera>();

        return Camera.main;
    }
}
