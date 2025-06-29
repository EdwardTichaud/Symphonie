using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public float floatSpeed = 0.5f;
    public float duration = 1f;
    public Color textColor = Color.red;

    private TextMeshProUGUI text;
    private CanvasGroup canvasGroup;
    private float elapsed;

    public static void Show(Vector3 worldPosition, int amount)
    {
        // Crée l'objet principal
        var obj = new GameObject("DamagePopup");
        obj.transform.position = worldPosition;

        // Applique le layer "Battle_UI"
        int uiLayer = LayerMask.NameToLayer("Battle_UI");
        if (uiLayer == -1)
        {
            Debug.LogWarning("Layer 'Battle_UI' not found. Please define it in Project Settings > Tags and Layers.");
            uiLayer = 0; // fallback to Default
        }
        obj.layer = uiLayer;

        // Configure le Canvas en World Space
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var camObj = GameObject.FindGameObjectWithTag("BattleCamera");
        if (camObj != null)
            canvas.worldCamera = camObj.GetComponent<Camera>();

        obj.AddComponent<CanvasRenderer>();
        var group = obj.AddComponent<CanvasGroup>();

        // Ajoute le texte
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        textObj.layer = uiLayer;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = amount.ToString();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 40;

        // Applique l'échelle adaptée au World Space
        obj.transform.localScale = Vector3.one * 0.015f;

        // Initialise le script
        var popup = obj.AddComponent<DamagePopup>();
        popup.text = tmp;
        popup.canvasGroup = group;
    }

    private void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            if (canvasGroup.alpha <= 0f)
                Destroy(gameObject);
        }
    }
}
