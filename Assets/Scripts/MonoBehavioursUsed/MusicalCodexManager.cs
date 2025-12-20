using System.Collections.Generic;
using UnityEngine;

public class MusicalCodexManager : MonoBehaviour
{
    public static MusicalCodexManager Instance { get; private set; }

    [Header("Codex des attaques musicales connues")]
    public List<MusicalMoveSO> knownMoves = new();

    [Header("Répertoire des attaques disponibles")]
    [Tooltip("Liste de référence utilisée pour restaurer les attaques connues lors du chargement.")]
    [SerializeField] private List<MusicalMoveSO> registeredMoves = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Applique l'état chargé si une sauvegarde a déjà été lue.
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            GameManager.Instance.gameData.ApplyKnownMovesTo(this);
    }

    /// <summary>
        /// Tente d'ajouter une attaque musicale au Codex.
        /// Retourne true si elle vient d'être découverte.
        /// </summary>
        public bool TryAddNewMelody(MusicalMoveSO move)
        {
            if (!knownMoves.Contains(move))
            {
                knownMoves.Add(move);
                Debug.Log($"Nouvelle mélodie découverte : {move.moveName}");
                return true;
            }
            return false;
        }

    /// <summary>
    /// Vérifie si cette attaque est déjà connue.
    /// </summary>
    public bool IsMelodyKnown(MusicalMoveSO move)
    {
        return knownMoves.Contains(move);
    }

    /// <summary>
    /// Construit la liste des IDs de mélodies connues pour la sauvegarde.
    /// </summary>
    public List<string> BuildKnownMoveIds()
    {
        var ids = new HashSet<string>();
        foreach (var move in knownMoves)
        {
            string id = ResolveMoveId(move);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return new List<string>(ids);
    }

    /// <summary>
    /// Applique l'état du codex à partir d'une liste d'IDs sauvegardés.
    /// </summary>
    public void ApplyKnownMoveIds(IEnumerable<string> ids)
    {
        var idSet = new HashSet<string>(ids ?? System.Array.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
        var lookup = BuildMoveLookup();

        knownMoves.Clear();
        foreach (var id in idSet)
        {
            if (string.IsNullOrEmpty(id))
                continue;

            if (lookup.TryGetValue(id, out var move))
                knownMoves.Add(move);
            else
                Debug.LogWarning($"[MusicalCodex] Impossible de restaurer la mélodie '{id}' (non référencée).");
        }
    }

    private Dictionary<string, MusicalMoveSO> BuildMoveLookup()
    {
        var lookup = new Dictionary<string, MusicalMoveSO>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var move in GetMoveRegistry())
        {
            string id = ResolveMoveId(move);
            if (string.IsNullOrEmpty(id))
                continue;

            if (!lookup.ContainsKey(id))
                lookup.Add(id, move);
        }

        return lookup;
    }

    private IEnumerable<MusicalMoveSO> GetMoveRegistry()
    {
        var unique = new HashSet<MusicalMoveSO>();

        if (registeredMoves != null)
        {
            foreach (var move in registeredMoves)
            {
                if (move != null && unique.Add(move))
                    yield return move;
            }
        }

        if (knownMoves != null)
        {
            foreach (var move in knownMoves)
            {
                if (move != null && unique.Add(move))
                    yield return move;
            }
        }
    }

    private static string ResolveMoveId(MusicalMoveSO move)
    {
        if (move == null)
            return string.Empty;

        // L'asset name est utilisé comme ID stable tant qu'il ne change pas.
        return move.name;
    }
}
