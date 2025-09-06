using UnityEngine;

/// <summary>
/// ScriptableObject utilisé pour définir manuellement jusqu'à trois ennemis
/// à invoquer dans un combat déclenché depuis une Timeline. Cette approche
/// permet d'éviter de passer plusieurs paramètres à une méthode de signal,
/// limitation courante dans l'éditeur de Timelines.
/// </summary>
[CreateAssetMenu(fileName = "TimelineEnemies", menuName = "Symphonie/Timeline Enemies Set")]
public class TimelineEnemiesSO : ScriptableObject
{
    [Header("Ennemi principal"), Tooltip("Premier ennemi obligatoire pour le combat")] 
    public CharacterData enemy1; // Ennemi qui doit toujours être défini

    [Header("Ennemis optionnels"), Tooltip("Second ennemi facultatif")] 
    public CharacterData enemy2; // Peut rester null si non utilisé

    [Tooltip("Troisième ennemi facultatif")] 
    public CharacterData enemy3; // Peut rester null pour un combat à deux ennemis
}
