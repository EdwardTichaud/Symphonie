using System;
using UnityEngine;

/// <summary>
///     Composant de compatibilité conservé pour les scènes existantes.
///     Toute la logique de dissonance est désormais gérée par <see cref="AwakeState"/>.
///     Ce script délègue donc chacune de ses opérations au nouveau système centralisé.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AwakeState))]
[AddComponentMenu("Symphonie/States/DissonantState (Legacy)")]
[Obsolete("Utiliser AwakeState directement : ce composant ne sert plus que de passerelle.")]
public class DissonantState : MonoBehaviour
{
    private AwakeState awakeState;

    /// <summary>
    ///     Récupère la référence vers le nouveau gestionnaire dès l'initialisation.
    ///     En cas d'oubli dans une scène, un message clair est affiché pour aider l'équipe.
    /// </summary>
    private void Awake()
    {
        awakeState = GetComponent<AwakeState>();
        if (awakeState == null)
            Debug.LogError("AwakeState manquant : le système de dissonance ne pourra pas fonctionner.", this);
    }

    /// <summary>Indique si l'unité est actuellement dissonante.</summary>
    public bool IsDissonant => awakeState != null && awakeState.IsDissonant;

    /// <summary>Délègue l'entrée en dissonance au nouveau système.</summary>
    public void EnterDissonant()
    {
        awakeState?.EnterDissonant();
    }

    /// <summary>Délègue la sortie de dissonance.</summary>
    public void ExitDissonant()
    {
        awakeState?.ExitDissonant();
    }
}
