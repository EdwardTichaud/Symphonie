using UnityEngine;

/// <summary>
/// ScriptableObject générique encapsulant un AudioClip et ses paramètres par défaut.
/// Utilisé pour harmoniser la configuration des sons dans tout le projet.
/// </summary>
[CreateAssetMenu(fileName = "AudioClipSO", menuName = "Symphonie/Audio Clip", order = 0)]
public class AudioClipSO : ScriptableObject
{
    public enum AudioClipType
    {
        SoundEffect,
        Music,
        Ambient,
        VoiceOver
    }

    public AudioClipType type;

    [Header("Clip de référence")]
    [Tooltip("Clip audio à jouer.")]
    public AudioClip audioClip;

    public string title;
    public string compositor;

    [Header("Paramètres de lecture par défaut")]
    [Range(0f, 1f)]
    [Tooltip("Volume appliqué par défaut lorsque ce clip est joué.")]
    public float volume = 0.8f;

    [Tooltip("Lecture en boucle activée par défaut pour ce clip.")]
    public bool loop = false;

    /// <summary>
    /// Retourne le volume sécurisé (compris entre 0 et 1).
    /// </summary>
    public float Volume => Mathf.Clamp01(volume);

    /// <summary>
    /// Retourne le clip à jouer. Peut être null si non assigné.
    /// </summary>
    public AudioClip Clip => audioClip;

    /// <summary>
    /// Indique si le clip doit boucler par défaut.
    /// </summary>
    public bool Loop => loop;

    /// <summary>
    /// Durée du clip en secondes (0 si aucun clip n'est assigné).
    /// Cette propriété est utilisée pour synchroniser les temps d'attente
    /// avec les indices sonores dans les différents systèmes de combat.
    /// </summary>
    public float Length => audioClip != null ? audioClip.length : 0f;
}
