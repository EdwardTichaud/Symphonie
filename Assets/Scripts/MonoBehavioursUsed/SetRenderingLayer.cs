using System;
using System.Linq; // Permet de concaténer facilement les tableaux de composants
using UnityEngine;
using UnityEngine.Rendering; // Nécessaire pour manipuler les Rendering Layer par nom

/// <summary>
/// Permet d'appliquer automatiquement un <c>Rendering Layer</c>
/// différent à chaque objet de la hiérarchie (meshs comme terrains)
/// en fonction de son <see cref="LayerMask"/> Unity.
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

    void Awake()
    {
        // Applique les Rendering Layers aux enfants au démarrage
        // pour s'assurer que tout est bien configuré dès le début.
        ApplyToChildren();
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les composants fournis.
    /// Les terrains ne dérivant pas de <see cref="Renderer"/>, on utilise
    /// un tableau générique de <see cref="Component"/> pour traiter à la fois
    /// les meshs classiques (<c>Renderer</c>) et les terrains (<c>Terrain</c>).
    /// Cette méthode factorise la logique utilisée par
    /// <see cref="ApplyToChildren"/> et <see cref="ApplyToAll"/> afin d'éviter
    /// toute duplication de code et de simplifier la maintenance.
    /// </summary>
    /// <param name="components">Composants à analyser (Renderer et/ou Terrain).</param>
    /// <returns>Nombre de composants effectivement modifiés.</returns>
    private int ApplyRenderingLayersTo(Component[] components)
    {
        // Vérifie qu'au moins une association a été fournie.
        if (mappings == null || mappings.Length == 0)
        {
            Debug.LogWarning("Aucune association LayerMask/RenderingLayer n'a été définie.");
            return 0;
        }

        int count = 0; // Nombre de composants modifiés

        // Parcourt chaque composant à traiter (Renderer ou Terrain)
        foreach (var component in components)
        {
            // Parcours toutes les associations pour trouver celle correspondant à la couche de l'objet
            foreach (var mapping in mappings)
            {
                // Récupération de la couche Unity de l'objet courant
                if (((1 << component.gameObject.layer) & mapping.layers.value) != 0)
                {
                    // Récupère le masque correspondant au nom du Rendering Layer demandé
                    uint mask = RenderingLayerMask.GetMask(mapping.renderingLayerName);

                    if (mask == 0)
                    {
                        // Si aucun masque n'a été trouvé, on avertit et on ne modifie pas cet objet
                        Debug.LogWarning($"Rendering Layer \"{mapping.renderingLayerName}\" introuvable pour {component.gameObject.name}.");
                        break;
                    }

                    // Application du masque de rendu trouvé en fonction du type de composant
                    switch (component)
                    {
                        case Renderer r: // Pour les meshs classiques
                            r.renderingLayerMask = mask;
                            count++;
                            break;
                        case Terrain t: // Pour les terrains Unity
                            t.renderingLayerMask = mask;
                            count++;
                            break;
                    }

                    break; // On s'arrête dès qu'une correspondance est trouvée
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// et <see cref="Terrain"/> présents dans les enfants de ce
    /// <see cref="GameObject"/>.
    /// </summary>
    public void ApplyToChildren()
    {
        // Récupère tous les Renderer et Terrain présents dans la hiérarchie
        var components = GetComponentsInChildren<Renderer>(true)
            .Cast<Component>()
            .Concat(GetComponentsInChildren<Terrain>(true))
            .ToArray();

        int count = ApplyRenderingLayersTo(components);
        Debug.Log($"✅ Rendering Layer(s) appliqué(s) à {count} composant(s) de la hiérarchie.");
    }

    /// <summary>
    /// Applique les <c>Rendering Layer</c> à tous les <see cref="Renderer"/>
    /// et <see cref="Terrain"/> actuellement présents dans la scène entière.
    /// </summary>
    public void ApplyToAll()
    {
        // Récupère tous les Renderer et Terrain présents dans la scène complète
        var components = FindObjectsOfType<Renderer>(true)
            .Cast<Component>()
            .Concat(FindObjectsOfType<Terrain>(true))
            .ToArray();

        int count = ApplyRenderingLayersTo(components);
        Debug.Log($"🌐 Rendering Layer(s) appliqué(s) à {count} composant(s) de la scène.");
    }
}
