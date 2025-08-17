using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Dialogue Container")]
public class DialogueContainer : ScriptableObject
{
    // Lignes de dialogue à afficher
    public DialogueLine[] lines;

    // Si vrai, la bulle de dialogue sera placée à une position aléatoire
    // sécurisée afin d'éviter les bords de l'écran
    public bool randomPosition = true;

    // Position personnalisée (en coordonnées d'ancrage UI) utilisée
    // lorsque randomPosition est désactivé
    public Vector3 customPosition;
}
