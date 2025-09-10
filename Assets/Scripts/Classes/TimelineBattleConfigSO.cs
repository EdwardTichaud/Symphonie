using UnityEngine;
using UnityEngine.Timeline; // Nécessaire pour référencer les TimelineAsset

/// <summary>
/// ScriptableObject regroupant la configuration complète d'un combat lancé
/// depuis une Timeline. Il permet de définir les ennemis à invoquer mais
/// également les timelines à jouer après la victoire ou la défaite.
/// </summary>
[CreateAssetMenu(fileName = "TimelineBattleConfig", menuName = "Symphonie/Timeline Battle Config")]
public class TimelineBattleConfigSO : ScriptableObject
{
    [Header("Ennemi principal"), Tooltip("Premier ennemi obligatoire pour le combat")]
    public CharacterData enemy1; // Ennemi qui doit toujours être défini

    [Header("Ennemis optionnels"), Tooltip("Second ennemi facultatif")] 
    public CharacterData enemy2; // Peut rester null si non utilisé

    [Tooltip("Troisième ennemi facultatif")]
    public CharacterData enemy3; // Peut rester null pour un combat à deux ennemis

    [Header("Timelines post-combat")]
    [Tooltip("Timeline à jouer en cas de victoire du joueur. Peut rester vide.")]
    public TimelineAsset victoryTimeline; // Optionnelle

    [Tooltip("Timeline à jouer en cas de défaite du joueur. Laisser vide pour un Game Over.")]
    public TimelineAsset defeatTimeline; // Null => Game Over
}
