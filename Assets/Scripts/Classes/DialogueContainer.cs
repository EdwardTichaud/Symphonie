using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Dialogue Container")]
public class DialogueContainer : ScriptableObject
{
    // Lignes de dialogue à afficher
    public DialogueLine[] lines;

    // Si true, la bulle est placée aléatoirement à l'écran
    // en respectant une marge pour éviter les bords de la caméra.
    // Si false (valeur par défaut), la position personnalisée ci-dessous est utilisée.
    public bool randomPosition;

    // Position personnalisée (en coordonnées d'ancrage UI) utilisée
    // lorsque randomPosition est désactivé
    public Vector3 customPosition;
}
