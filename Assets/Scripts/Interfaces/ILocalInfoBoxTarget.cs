using UnityEngine;

/// <summary>
/// Interface permettant de fournir un décalage personnalisé pour la LocalInfoBox.
/// Tout objet interactif la mettant en œuvre pourra afficher l'invite
/// d'interaction avec un offset défini dans l'inspecteur.
/// </summary>
public interface ILocalInfoBoxTarget
{
    /// <summary>
    /// Décalage à appliquer pour positionner la LocalInfoBox en world space.
    /// </summary>
    Vector3 LocalInfoBoxOffset { get; }
}
