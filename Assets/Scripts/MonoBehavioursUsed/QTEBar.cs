using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Gère une barre de QTE où les icônes défilent de droite à gauche
/// à la manière d'un jeu musical type Guitar Hero.
/// </summary>
public class QTEBar : MonoBehaviour
{
    [Header("Références visuelles")]
    [SerializeField] private RectTransform barRect;       // Image représentant la barre
    [SerializeField] private RectTransform validationZone; // Zone de validation à l'extrémité
    [SerializeField] private Image notePrefab;             // Préfab d'icône à instancier pour chaque QTE

    private readonly List<Image> activeNotes = new();
    private float barWidth;

    private void Awake()
    {
        // Largeur utilisée pour calculer la position des icônes
        if (barRect != null)
            barWidth = barRect.rect.width;
    }

    /// <summary>
    /// Crée une nouvelle icône de QTE et la place à droite de la barre.
    /// </summary>
    /// <param name="icon">Icône à afficher sur la note.</param>
    /// <returns>Image instanciée pour la note.</returns>
    public Image CreateNote(Sprite icon)
    {
        if (notePrefab == null || barRect == null)
            return null;

        Image note = Instantiate(notePrefab, barRect);
        if (icon != null)
            note.sprite = icon;

        RectTransform rect = note.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(barWidth / 2f, 0f);
        activeNotes.Add(note);
        return note;
    }

    /// <summary>
    /// Met à jour la position de la note le long de la barre.
    /// </summary>
    /// <param name="note">Image à déplacer.</param>
    /// <param name="t">Progression normalisée (0 à 1).</param>
    public void UpdateNotePosition(Image note, float t)
    {
        if (note == null || barRect == null)
            return;

        RectTransform rect = note.GetComponent<RectTransform>();
        float startX = barWidth / 2f;
        float endX = -barWidth / 2f;
        rect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);
    }

    /// <summary>
    /// Indique si la note donnée se situe dans la zone de validation.
    /// </summary>
    public bool IsNoteInValidationZone(Image note)
    {
        if (note == null || validationZone == null)
            return false;

        RectTransform noteRect = note.GetComponent<RectTransform>();
        float zoneMin = validationZone.anchoredPosition.x - validationZone.rect.width / 2f;
        float zoneMax = validationZone.anchoredPosition.x + validationZone.rect.width / 2f;
        float x = noteRect.anchoredPosition.x;
        return x >= zoneMin && x <= zoneMax;
    }
}
