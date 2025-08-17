using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Dialogue Container")]
public class DialogueContainer : ScriptableObject
{
    // Lignes de dialogue à afficher
    public DialogueLine[] lines;

    // Si true, on utilise la position personnalisée définie ci-dessous.
    // Si false (case non cochée), la bulle est placée à une position aléatoire
    // en respectant une marge pour éviter les bords de l'écran.
    public bool randomPosition = true;

    // Position personnalisée (en coordonnées d'ancrage UI) utilisée
    // lorsque randomPosition est activé
    public Vector3 customPosition;
}
