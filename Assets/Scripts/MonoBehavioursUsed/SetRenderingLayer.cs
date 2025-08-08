using System;
using UnityEngine;

/// <summary>
/// Permet d'appliquer automatiquement un <c>Rendering Layer</c>
/// différent à chaque objet de la hiérarchie en fonction de son
/// <see cref="LayerMask"/> Unity.
/// </summary>
[ExecuteAlways]
public class SetRenderingLayer : MonoBehaviour
{
    /// <summary>
    /// Association entre un ou plusieurs <see cref="LayerMask"/> Unity et
    /// l'index du <c>Rendering Layer</c> à appliquer.
    /// </summary>
    [Serializable]
    public struct LayerRenderingPair
    {
        [Tooltip("Couches Unity à cibler")]
        public LayerMask layers;

        [Tooltip("Index du Rendering Layer à appliquer"), Range(0, 31)]
        public int renderingLayerIndex;
    }

    [Tooltip("Liste des associations LayerMask / Rendering Layer.")]
    public LayerRenderingPair[] mappings;

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/> présents
    /// dans les enfants de ce GameObject.
    /// </summary>
    public void ApplyToChildren()
    {
        // Vérifie qu'au moins une association a été fournie.
        if (mappings == null || mappings.Length == 0)
        {
            Debug.LogWarning("Aucune association LayerMask/RenderingLayer n'a été définie.");
            return;
        }

        int count = 0; // Nombre de renderers modifiés

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // Parcours toutes les associations pour trouver celle correspondant à la couche de l'objet
            foreach (var mapping in mappings)
            {
                if (((1 << r.gameObject.layer) & mapping.layers.value) != 0)
                {
                    // Conversion de l'index en masque de rendu et application
                    uint mask = 1u << mapping.renderingLayerIndex;
                    r.renderingLayerMask = mask;
                    count++;
                    break; // On s'arrête dès qu'une correspondance est trouvée
                }
            }
        }

        Debug.Log($"✅ Rendering Layer(s) appliqué(s) à {count} renderer(s) de la hiérarchie.");
    }
}
