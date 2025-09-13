using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fournit un accès rapide aux points d'ancrage caméra d'une <see cref="CharacterUnit"/>.
/// Les références sont définies dans l'inspecteur afin d'éviter toute recherche par nom au runtime.
/// </summary>
public class CameraAnchorProvider : MonoBehaviour
{
    /// <summary>
    /// Association nom->Transform pour les différents points d'accroche caméra.
    /// </summary>
    [System.Serializable]
    public struct AnchorEntry
    {
        [Tooltip("Identifiant logique de l'ancre (ex. Camera_MainMenu)")]
        public string key;
        [Tooltip("Transform correspondant au point d'ancrage")]
        public Transform anchor;
    }

    [Tooltip("Liste des ancres disponibles sur cette unité.")]
    public List<AnchorEntry> anchors = new();

    // Dictionnaire interne pour accéder rapidement aux ancres.
    private readonly Dictionary<string, Transform> anchorMap = new();

    private void Awake()
    {
        anchorMap.Clear();
        foreach (var entry in anchors)
        {
            if (!string.IsNullOrEmpty(entry.key) && entry.anchor != null && !anchorMap.ContainsKey(entry.key))
            {
                anchorMap.Add(entry.key, entry.anchor);
            }
        }
    }

    /// <summary>
    /// Retourne l'ancre associée au nom fourni.
    /// </summary>
    public Transform GetAnchor(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        anchorMap.TryGetValue(name, out var result);
        return result;
    }
}
