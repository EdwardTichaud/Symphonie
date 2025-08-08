using System;
using UnityEngine;
using UnityEngine.Rendering; // Nécessaire pour manipuler les Rendering Layer par nom


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
    /// le <c>Rendering Layer</c> à appliquer, désigné par son nom afin de
    /// faciliter la lecture et la maintenance.

    /// </summary>
    [Serializable]
    public struct LayerRenderingPair
    {
        [Tooltip("Couches Unity à cibler")]
        public LayerMask layers;

        /// <summary>
        /// Nom du Rendering Layer à appliquer à la cible.
        /// L'utilisation d'une chaîne évite de mémoriser les indices numériques.
        /// </summary>
        [Tooltip("Nom du Rendering Layer à appliquer")]
        public string renderingLayerName;

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
                    // Récupère le masque correspondant au nom du Rendering Layer demandé
                    uint mask = RenderingLayerMask.GetMask(mapping.renderingLayerName);

                    if (mask == 0)
                    {
                        // Si aucun masque n'a été trouvé, on avertit et on ne modifie pas cet objet
                        Debug.LogWarning($"Rendering Layer \"{mapping.renderingLayerName}\" introuvable pour {r.gameObject.name}.");
                        break;
                    }

                    // Application du masque de rendu trouvé

                    r.renderingLayerMask = mask;
                    count++;
                    break; // On s'arrête dès qu'une correspondance est trouvée
                }
            }
        }

        Debug.Log($"✅ Rendering Layer(s) appliqué(s) à {count} renderer(s) de la hiérarchie.");
    }
}
