using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float duration = 1f;
    public Vector3 offset = new Vector3(0, 2f, 0);
    [SerializeField] private float bounceHeight = 0.35f;
    [SerializeField] private float bounceFrequency = 2f;
    [SerializeField] private float bounceScale = 0.2f;

    [Header("Références")]
    public TextMeshProUGUI textMesh; // Référence au texte affichant le montant

    private float elapsed = 0f;        // Temps écoulé depuis l'initialisation pour piloter le fondu
    private float floatOffset = 0f;    // Décalage vertical cumulé appliqué au-dessus de la cible
    private CanvasGroup canvasGroup;   // Permet de faire disparaître progressivement le popup
    private DamagePopupManager owner;
    private Transform target;
    private CharacterUnit targetUnit;
    private Camera battleCamera;
    private Canvas popupCanvas;
    private RectTransform rectTransform;
    private RectTransform textRect;

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
        textRect ??= textMesh.rectTransform;
        textRect.localScale = Vector3.one;

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

        rectTransform ??= GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[DamagePopup] Aucun RectTransform disponible pour positionner le popup.");
            Release();
            return;
        }

        popupCanvas = GetComponent<Canvas>();
        if (popupCanvas == null)
            popupCanvas = GetComponentInParent<Canvas>();

        if (popupCanvas == null)
        {
            Debug.LogWarning("[DamagePopup] Aucun Canvas trouvé pour positionner le popup.");
            Release();
            return;
        }

        if (popupCanvas.gameObject == gameObject)
            popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        if (popupCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            popupCanvas.worldCamera = battleCamera;

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
        if (duration <= 0f)
        {
            Release();
            return;
        }

        // Les effets d'interface utilisent le temps non-scalé pour rester visibles en pause.
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float rise = floatSpeed * duration * t;
        float bounce = Mathf.Sin(t * Mathf.PI * bounceFrequency) * bounceHeight * (1f - t);
        floatOffset = rise + bounce;

        if (textRect != null)
            textRect.localScale = Vector3.one * (1f + bounceScale * Mathf.Sin(t * Mathf.PI));

        // Met à jour la position à chaque frame.
        UpdatePosition();

        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(1f - t);

        if (t >= 1f)
            Release();
    }

    /// <summary>
    /// Calcule la position et l'orientation du popup d'écran.
    /// </summary>
    private void UpdatePosition()
    {
        if (battleCamera == null || target == null || popupCanvas == null)
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
        RectTransform canvasRect = popupCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, popupCanvas.worldCamera, out Vector3 worldPoint))
            rectTransform.position = worldPoint;
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
