using UnityEngine;
using UnityEngine.UI;

public class AlphaFader : MonoBehaviour
{
    public enum FadeTarget { CanvasGroup, SpriteRenderer, Image }

    [Header("Réglages généraux")]
    public FadeTarget targetType = FadeTarget.CanvasGroup;
    public bool fadeIn = true; // true = alpha vers 1, false = alpha vers 0
    public float fadeSpeed = 1f;

    [Header("Composants (selon le type)")]
    public CanvasGroup canvasGroup;
    public SpriteRenderer spriteRenderer;
    public Image uiImage;

    private void Start()
    {
        SetInitialAlpha();
        StartCoroutine(FadeRoutine());
    }

    private void SetInitialAlpha()
    {
        float startAlpha = fadeIn ? 0f : 1f;

        switch (targetType)
        {
            case FadeTarget.CanvasGroup:
                if (canvasGroup != null)
                    canvasGroup.alpha = startAlpha;
                break;
            case FadeTarget.SpriteRenderer:
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = startAlpha;
                    spriteRenderer.color = c;
                }
                break;
            case FadeTarget.Image:
                if (uiImage != null)
                {
                    Color c = uiImage.color;
                    c.a = startAlpha;
                    uiImage.color = c;
                }
                break;
        }
    }

    private System.Collections.IEnumerator FadeRoutine()
    {
        float targetAlpha = fadeIn ? 1f : 0f;

        while (true)
        {
            bool done = false;
            float currentAlpha = GetCurrentAlpha();
            float newAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            SetAlpha(newAlpha);

            if (Mathf.Approximately(newAlpha, targetAlpha))
                done = true;

            if (done)
                yield break;

            yield return null;
        }
    }

    private float GetCurrentAlpha()
    {
        switch (targetType)
        {
            case FadeTarget.CanvasGroup:
                return canvasGroup != null ? canvasGroup.alpha : 1f;
            case FadeTarget.SpriteRenderer:
                return spriteRenderer != null ? spriteRenderer.color.a : 1f;
            case FadeTarget.Image:
                return uiImage != null ? uiImage.color.a : 1f;
        }
        return 1f;
    }

    private void SetAlpha(float alpha)
    {
        switch (targetType)
        {
            case FadeTarget.CanvasGroup:
                if (canvasGroup != null)
                    canvasGroup.alpha = alpha;
                break;
            case FadeTarget.SpriteRenderer:
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = alpha;
                    spriteRenderer.color = c;
                }
                break;
            case FadeTarget.Image:
                if (uiImage != null)
                {
                    Color c = uiImage.color;
                    c.a = alpha;
                    uiImage.color = c;
                }
                break;
        }
    }
}
