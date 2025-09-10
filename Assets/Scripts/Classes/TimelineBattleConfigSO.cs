using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// ScriptableObject centralisant les informations nécessaires pour
/// déclencher un combat depuis une Timeline :
///   - jusqu'à trois ennemis à affronter
///   - les cinématiques à jouer après une victoire ou une défaite
/// Cette approche évite d'avoir à passer de multiples paramètres dans les
/// signaux de Timeline, qui ne supportent qu'un seul argument.
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

    [Header("Timelines de fin"), Tooltip("Ciné à jouer si le joueur gagne")]
    public TimelineAsset victoryTimeline; // Timeline à jouer après la victoire

    [Tooltip("Ciné à jouer si le joueur perd")]
    public TimelineAsset defeatTimeline; // Timeline à jouer après la défaite
}
