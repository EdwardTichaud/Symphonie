using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant chargé de garantir que le visage de Lucian démarre avec une expression neutre.
/// L'objectif est d'éviter les variations indésirables sur les yeux et la bouche lorsque le modèle est instancié.
/// L'expression neutre correspond à des paupières à demi fermées et à une bouche complètement close.
/// </summary>
[DisallowMultipleComponent]
public class LucianFaceNeutralizer : MonoBehaviour
{
    /// <summary>
    /// Structure décrivant une commande de blend shape à appliquer.
    /// On stocke le nom du blend shape tel qu'il existe dans le mesh ainsi que le poids désiré.
    /// </summary>
    [System.Serializable]
    private struct BlendShapeOverride
    {
        [Tooltip("Nom exact du blend shape à forcer sur le mesh cible.")]
        public string blendShapeName; // Nom du blend shape tel qu'il apparaît dans l'import CC5

        [Tooltip("Poids souhaité pour ce blend shape afin de définir la pose neutre.")]
        [Range(0f, 100f)]
        public float weight; // Valeur en pourcentage envoyée au SkinnedMeshRenderer
    }

    [Header("Configuration du visage neutre")]
    [Tooltip("Liste des blend shapes à verrouiller pour garantir la pose neutre (paupières mi-closes, bouche fermée).")]
    [SerializeField]
    private BlendShapeOverride[] blendShapeOverrides = new BlendShapeOverride[]
    {
        // Les blend shapes d'yeux sont poussés à ~45 pour obtenir un regard détendu sans fermeture complète.
        new BlendShapeOverride { blendShapeName = "Eye_Blink_L", weight = 45f },
        new BlendShapeOverride { blendShapeName = "Eye_Blink_R", weight = 45f },

        // Les contrôles de mâchoire sont remis à zéro pour empêcher toute ouverture de la bouche au démarrage.
        new BlendShapeOverride { blendShapeName = "Jaw_Open", weight = 0f },
        new BlendShapeOverride { blendShapeName = "Jaw_Open_Extreme", weight = 0f },

        // On verrouille les blend shapes de lèvres qui peuvent introduire une ouverture résiduelle.
        new BlendShapeOverride { blendShapeName = "Mouth_Lips_Together_UL", weight = 0f },
        new BlendShapeOverride { blendShapeName = "Mouth_Lips_Together_UR", weight = 0f },
        new BlendShapeOverride { blendShapeName = "Mouth_Lips_Together_DL", weight = 0f },
        new BlendShapeOverride { blendShapeName = "Mouth_Lips_Together_DR", weight = 0f }
    };

    // Buffer réutilisé pour éviter des allocations lorsque l'on récupère les SkinnedMeshRenderer.
    private readonly List<SkinnedMeshRenderer> rendererBuffer = new List<SkinnedMeshRenderer>();

    private void Reset()
    {
        // Reset est appelé dans l'éditeur : on prépare d'emblée la liste des renderers et on applique la pose.
        CacheRenderers();
        ApplyNeutralPose();
    }

    private void Awake()
    {
        // Awake garantit que les références sont prêtes avant que d'autres scripts puissent manipuler les blend shapes.
        CacheRenderers();
        ApplyNeutralPose();
    }

    private void OnEnable()
    {
        // Lors d'une réactivation du prefab (ex : instanciation ou réutilisation d'un pool), on réapplique les valeurs neutres.
        ApplyNeutralPose();
    }

    /// <summary>
    /// Construit ou reconstruit la liste des SkinnedMeshRenderer présents sous le personnage.
    /// On active la recherche en profondeur pour inclure les meshes des yeux, dents, etc.
    /// </summary>
    private void CacheRenderers()
    {
        rendererBuffer.Clear(); // On nettoie la liste pour éviter les doublons.
        GetComponentsInChildren(true, rendererBuffer); // true inclut les objets désactivés qui pourraient porter des meshes de visage.
    }

    /// <summary>
    /// Parcourt tous les SkinnedMeshRenderer détectés et force les blend shapes configurés.
    /// Si un blend shape n'existe pas sur un mesh donné, on l'ignore silencieusement.
    /// </summary>
    private void ApplyNeutralPose()
    {
        if (rendererBuffer.Count == 0)
        {
            // Sécurité : si la liste est vide (premier appel depuis OnEnable par exemple), on regénère la liste.
            CacheRenderers();
        }

        foreach (var renderer in rendererBuffer)
        {
            if (renderer == null)
            {
                // Les GameObjects peuvent être désactivés ou détruits, on saute dans ce cas.
                continue;
            }

            var mesh = renderer.sharedMesh; // sharedMesh pour éviter de dupliquer le mesh en mémoire.
            if (mesh == null)
            {
                // Certains SkinnedMeshRenderer peuvent être configurés sans mesh (ex : placeholders), on les ignore.
                continue;
            }

            foreach (var blendShapeOverride in blendShapeOverrides)
            {
                if (string.IsNullOrWhiteSpace(blendShapeOverride.blendShapeName))
                {
                    // Permet d'avoir des entrées optionnelles dans l'inspecteur sans provoquer d'erreurs.
                    continue;
                }

                int blendShapeIndex = mesh.GetBlendShapeIndex(blendShapeOverride.blendShapeName);
                if (blendShapeIndex >= 0)
                {
                    // Le blend shape est présent sur ce mesh : on applique le poids voulu pour figer l'expression.
                    renderer.SetBlendShapeWeight(blendShapeIndex, blendShapeOverride.weight);
                }
                else
                {
                    // Si le blend shape est absent de ce renderer, on ne fait rien : cela permet de gérer plusieurs meshes (corps, cils...).
                    continue;
                }
            }
        }
    }
}
