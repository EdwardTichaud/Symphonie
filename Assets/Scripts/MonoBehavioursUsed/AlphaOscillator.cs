using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class AlphaOscillator : MonoBehaviour
{
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    public float speed = 1f;
    public bool autoStart = true;

    private float t;
    private bool isOscillating = false;

    private SpriteRenderer spriteRenderer;
    private Renderer meshRenderer;
    private Image uiImage;
    private TMP_Text tmpText;

    private MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        meshRenderer = GetComponent<Renderer>();
        uiImage = GetComponent<Image>();
        tmpText = GetComponent<TMP_Text>();

        if (meshRenderer != null)
            propertyBlock = new MaterialPropertyBlock();

        if (autoStart)
            StartOscillation();
    }

    public void StartOscillation()
    {
        isOscillating = true;
        t = 0f;
    }

    public void StopOscillation()
    {
        isOscillating = false;
    }

    void Update()
    {
        if (!isOscillating) return;

        t += Time.deltaTime * speed;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(t) + 1f) / 2f);

        ApplyAlpha(alpha);
    }

    void ApplyAlpha(float a)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = a;
            spriteRenderer.color = c;
        }
        else if (uiImage != null)
        {
            Color c = uiImage.color;
            c.a = a;
            uiImage.color = c;
        }
        else if (tmpText != null)
        {
            Color c = tmpText.color;
            c.a = a;
            tmpText.color = c;
        }
        else if (meshRenderer != null)
        {
            meshRenderer.GetPropertyBlock(propertyBlock);
            Color c = propertyBlock.GetColor("_Color");
            c.a = a;
            propertyBlock.SetColor("_Color", c);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
