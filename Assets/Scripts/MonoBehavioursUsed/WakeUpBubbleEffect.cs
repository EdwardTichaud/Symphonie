using System.Collections;
using UnityEngine;

/// <summary>
/// Crée une bulle qui grossit puis éclate pour illustrer le réveil.
/// </summary>
public class WakeUpBubbleEffect : MonoBehaviour
{
    public Material bubbleMaterial;
    public float popDuration = 0.5f;

    private void Start()
    {
        StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.transform.SetParent(transform);
        bubble.transform.localPosition = Vector3.zero;
        bubble.transform.localScale = Vector3.zero;
        if (bubbleMaterial != null)
            bubble.GetComponent<Renderer>().material = bubbleMaterial;

        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            float progress = time / popDuration;
            bubble.transform.localScale = Vector3.one * progress;
            yield return null;
        }

        Destroy(bubble);
        Destroy(gameObject);
    }
}
