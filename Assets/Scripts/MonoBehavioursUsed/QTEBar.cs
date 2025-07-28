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

    /// <summary>
    /// Accès à la zone de validation pour placer les effets de résultat.
    /// </summary>
    public RectTransform ValidationZone => validationZone;

    // Notes directement visibles actuellement
    private readonly List<Image> activeNotes = new();
    // Notes planifiées pour apparaître dans le futur
    private class ScheduledNote
    {
        public Image image;
        public float startTime;
        public float endTime;
        public bool started;
    }
    private readonly List<ScheduledNote> scheduledNotes = new();
    // Durée par défaut pour qu'une note parcoure toute la barre
    private const float defaultTravelDuration = 2f;
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
    /// Planifie l'apparition d'une note qui se déplacera automatiquement
    /// de la droite vers la gauche sur une durée donnée.
    /// </summary>
    /// <param name="icon">Icône à afficher sur la note.</param>
    /// <param name="delay">Temps avant l'apparition (en secondes).</param>
    /// <param name="travelDuration">Temps nécessaire pour atteindre la zone de validation.</param>
    /// <returns>L'image instanciée pour la note.</returns>
    public Image ScheduleNote(Sprite icon, float delay, float travelDuration)
    {
        if (notePrefab == null || barRect == null)
            return null;

        Image note = Instantiate(notePrefab, barRect);
        if (icon != null)
            note.sprite = icon;

        RectTransform rect = note.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(barWidth / 2f, 0f);
        note.enabled = false;

        if (travelDuration <= 0f)
            travelDuration = defaultTravelDuration;

        scheduledNotes.Add(new ScheduledNote
        {
            image = note,
            startTime = Time.unscaledTime + delay,
            endTime = Time.unscaledTime + delay + travelDuration,
            started = false
        });

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

    private void Update()
    {
        if (scheduledNotes.Count == 0)
            return;

        float now = Time.unscaledTime;
        // Liste temporaire pour retirer proprement les notes terminées
        List<ScheduledNote> toRemove = null;

        foreach (var n in scheduledNotes)
        {
            // Ignore les notes dont l'affichage n'a pas encore commencé
            if (now < n.startTime)
                continue;

            if (!n.started)
            {
                n.started = true;
                if (n.image != null)
                    n.image.enabled = true;
            }

            if (n.image == null)
            {
                // L'objet a été détruit ailleurs, on supprime la note de la liste
                (toRemove ??= new List<ScheduledNote>()).Add(n);
                continue;
            }

            float progress = Mathf.Clamp01((now - n.startTime) / (n.endTime - n.startTime));
            UpdateNotePosition(n.image, progress);

            if (progress >= 1f)
                (toRemove ??= new List<ScheduledNote>()).Add(n);
        }

        if (toRemove != null)
        {
            foreach (var rem in toRemove)
            {
                scheduledNotes.Remove(rem);
                activeNotes.Remove(rem.image);
                if (rem.image != null)
                    Destroy(rem.image.gameObject);
            }
        }
    }
}
