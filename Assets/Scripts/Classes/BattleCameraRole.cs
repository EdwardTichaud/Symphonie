using System;

/// <summary>
/// Liste les rôles cinématiques proposés pendant les combats.
/// Chaque valeur correspond à une ambiance visuelle précise
/// inspirée de Clair Obscur: Expedition 33.
/// </summary>
[Serializable]
public enum BattleCameraRole
{
    /// <summary>
    /// Valeur neutre : aucune caméra particulière, la vue par défaut est utilisée.
    /// </summary>
    None = 0,

    /// <summary>
    /// Plan d'attente utilisé pour les menus ou l'introduction (respiration légère du cadre).
    /// </summary>
    MainMenuIdle = 1,

    /// <summary>
    /// Plan épaule du lanceur vers la cible pour souligner l'intention de l'attaque.
    /// </summary>
    OverShoulderCasterToTarget = 2,

    /// <summary>
    /// Plan serré sur le lanceur (push caméra) pour les phases d'incantation.
    /// </summary>
    ClosePushCaster = 3,

    /// <summary>
    /// Plan réaction sur la cible afin de mettre en avant l'impact.
    /// </summary>
    TargetReaction = 4,

    /// <summary>
    /// Plan large englobant lanceur et cible via un cadrage de groupe.
    /// </summary>
    WideEstablish = 5,

    /// <summary>
    /// Plan travelling suivant un projectile ou un effet en vol.
    /// </summary>
    ProjectileFlyby = 6,

    /// <summary>
    /// Plan final héroïque utilisé lors des victoires ou fin d'affrontement.
    /// </summary>
    Victory = 7
}
