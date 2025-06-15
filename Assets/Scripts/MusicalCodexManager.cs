using System.Collections.Generic;
using UnityEngine;

public class MusicalCodexManager : MonoBehaviour
{
    public static MusicalCodexManager Instance { get; private set; }

    [Header("Codex des attaques musicales connues")]
    public List<MusicalMoveSO> knownMoves = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Tente d'ajouter une attaque musicale au Codex.
    /// Retourne true si elle vient d'�tre d�couverte.
    /// </summary>
    public bool TryAddNewMelody(MusicalMoveSO move)
    {
        if (!knownMoves.Contains(move))
        {
            knownMoves.Add(move);

            // Affichage de d�couverte
            ActionUIDisplayManager.Instance.DisplayMelodyDiscovery(move.moveName);

            Debug.Log($"Nouvelle m�lodie d�couverte : {move.moveName}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// V�rifie si cette attaque est d�j� connue.
    /// </summary>
    public bool IsMelodyKnown(MusicalMoveSO move)
    {
        return knownMoves.Contains(move);
    }
}
