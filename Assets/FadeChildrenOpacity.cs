using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class FadeChildrenOpacity : MonoBehaviour
{
    public static FadeChildrenOpacity Instance { get; private set; }

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Change l’opacité d’un enfant (Image ou SpriteRenderer) vers la valeur désirée.
    /// </summary>
    /// <param name="childIndex">Indice de l’enfant dans la hiérarchie</param>
    /// <param name="targetAlpha">Opacité cible (0 = transparent, 1 = opaque)</param>
    /// <param name="duration">Durée du fade en secondes</param>
    public void ChangeOpacity(int childIndex, float targetAlpha, float duration)
    {
        if (childIndex < 0 || childIndex >= transform.childCount)
        {
            Debug.LogWarning($"[FadeChildrenOpacity] Index enfant {childIndex} invalide.");
            return;
        }

        GameObject child = transform.GetChild(childIndex).gameObject;

        Image uiImage = child.GetComponent<Image>();
        SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();

        if (uiImage == null && spriteRenderer == null)
        {
            Debug.LogWarning($"[FadeChildrenOpacity] L’enfant {child.name} n’a pas d’Image ni de SpriteRenderer.");
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(uiImage, spriteRenderer, targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(Image uiImage, SpriteRenderer spriteRenderer, float targetAlpha, float duration)
    {
        float t = 0f;
        float startAlpha = 1f;

        if (uiImage != null) startAlpha = uiImage.color.a;
        if (spriteRenderer != null) startAlpha = spriteRenderer.color.a;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, targetAlpha, t / duration);

            if (uiImage != null)
            {
                Color c = uiImage.color;
                c.a = a;
                uiImage.color = c;
            }
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = a;
                spriteRenderer.color = c;
            }

            yield return null;
        }

        // Snap final
        if (uiImage != null)
        {
            Color c = uiImage.color;
            c.a = targetAlpha;
            uiImage.color = c;
        }
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = targetAlpha;
            spriteRenderer.color = c;
        }

        fadeRoutine = null;
    }
}
