using UnityEngine;

[ExecuteAlways]
public class SetRenderingLayer : MonoBehaviour
{
    [HideInInspector]
    public int renderingLayerIndex = 0;

    public void ApplyToChildren()
    {
        uint mask = 1u << renderingLayerIndex;

        int count = 0;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.renderingLayerMask = mask;
            count++;
        }

        Debug.Log($"✅ Rendering Layer '{renderingLayerIndex}' appliqué à {count} renderer(s).");
    }
}
