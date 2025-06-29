using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public float floatSpeed = 1f;
    public float duration = 1.5f;
    public Color textColor = Color.red;

    private TextMeshProUGUI text;
    private CanvasGroup canvasGroup;
    private float elapsed;

    public static void Show(Vector3 worldPosition, int amount)
    {
        var obj = new GameObject("DamagePopup");
        obj.transform.position = worldPosition;

        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var camObj = GameObject.FindGameObjectWithTag("BattleCamera");
        if (camObj != null)
            canvas.worldCamera = camObj.GetComponent<Camera>();

        obj.AddComponent<CanvasRenderer>();
        var group = obj.AddComponent<CanvasGroup>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = amount.ToString();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 40;

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
