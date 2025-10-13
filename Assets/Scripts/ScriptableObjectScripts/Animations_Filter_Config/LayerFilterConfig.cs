using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LayerFilterConfig", menuName = "Symphonie/Animation/Layer Filter Config", order = 10)]
public class LayerFilterConfig : ScriptableObject
{
    [Tooltip("Règles de génération par layer/slot. L'ordre ici = ordre de création.")]
    public List<LayerRule> layers = new List<LayerRule>();

    [Serializable]
    public class LayerRule
    {
        [Header("Identification")]
        [Tooltip("Nom logique de la couche (documentaire).")]
        public string layerName = "UpperBody_ArmsWeapons";

        [Tooltip("Préfixe ajouté au nom d'origine du clip.")]
        public string prefix = "UpperBody_AW_";

        [Header("Filtrage par chemins (case-insensitive, Path.Contains)")]
        [Tooltip("Au moins un fragment doit matcher pour INCLURE le binding. Si vide: includeAllIfEmpty décide.")]
        public List<string> includePathFragments = new List<string>();

        [Tooltip("Si un fragment matche, le binding est EXCLU.")]
        public List<string> excludePathFragments = new List<string>();

        [Header("Blendshapes")]
        [Tooltip("Si activé: on NE GARDE QUE les blendshapes (propertyName commence par 'blendShape.').")]
        public bool keepOnlyBlendshapes = false;

        [Tooltip("Si activé: on SUPPRIME toutes les courbes de blendshapes.")]
        public bool dropAllBlendshapes = false;

        [Tooltip("Limiter les blendshapes à certains meshes (paths). Laisse vide pour tous.")]
        public List<string> allowedBlendshapeMeshPaths = new List<string>();

        [Header("Autres options")]
        [Tooltip("Si 'includePathFragments' est vide: tout est inclus (true) ou rien (false).")]
        public bool includeAllIfEmpty = false;

        [Tooltip("Copier les Animation Events depuis le clip source.")]
        public bool copyAnimationEvents = true;

        [Tooltip("Mettre le frameRate du clip cible = frameRate du source.")]
        public bool copyFrameRate = true;
    }
}
