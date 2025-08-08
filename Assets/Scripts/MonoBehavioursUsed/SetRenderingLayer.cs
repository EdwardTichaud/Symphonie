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
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// fournis. Cette méthode factorise la logique utilisée par
    /// <see cref="ApplyToChildren"/> et <see cref="ApplyToAll"/> afin d'éviter
    /// toute duplication de code et de simplifier la maintenance.
    /// </summary>
    /// <param name="renderers">Collection de renderers à traiter.</param>
    /// <returns>Nombre de renderers effectivement modifiés.</returns>
    private int ApplyRenderingLayersTo(Renderer[] renderers)
    {
        // Vérifie qu'au moins une association a été fournie.
        if (mappings == null || mappings.Length == 0)
        {
            Debug.LogWarning("Aucune association LayerMask/RenderingLayer n'a été définie.");
            return 0;
        }

        int count = 0; // Nombre de renderers modifiés

        foreach (var r in renderers)
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

        return count;
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// présents dans les enfants de ce <see cref="GameObject"/>.
    /// </summary>
    public void ApplyToChildren()
    {
        int count = ApplyRenderingLayersTo(GetComponentsInChildren<Renderer>(true));
        Debug.Log($"✅ Rendering Layer(s) appliqué(s) à {count} renderer(s) de la hiérarchie.");
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// actuellement présents dans la scène entière.
    /// </summary>
    public void ApplyToAll()
    {
        int count = ApplyRenderingLayersTo(FindObjectsOfType<Renderer>(true));
        Debug.Log($"🌐 Rendering Layer(s) appliqué(s) à {count} renderer(s) de la scène.");
    }
}
