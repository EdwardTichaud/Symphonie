using UnityEngine;

[ExecuteAlways]
public class SetRenderingLayer : MonoBehaviour
{
    [HideInInspector]
    public int renderingLayerIndex = 0;

    // Couches Unity auxquelles appliquer ce Rendering Layer
    public LayerMask targetLayers = ~0; // Par défaut : toutes les couches

    public void ApplyToChildren()
    {
        uint mask = 1u << renderingLayerIndex;

        int count = 0;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // Vérifie si la couche de l'objet correspond à l'un des LayerMask sélectionnés
            if (((1 << r.gameObject.layer) & targetLayers.value) != 0)
            {
                r.renderingLayerMask = mask;
                count++;
            }
        }

        Debug.Log($"✅ Rendering Layer '{renderingLayerIndex}' appliqué à {count} renderer(s) correspondant aux LayerMask sélectionnés.");
    }
}
