using UnityEngine;

/// <summary>
/// Permet de jouer un son soit automatiquement au démarrage,
/// soit sur demande (notamment depuis une Timeline).
/// Utilise l'<see cref="AudioManager"/> s'il est disponible pour garder
/// une gestion centralisée du volume, sinon se rabat sur une
/// <see cref="AudioSource"/> locale.
/// </summary>
public class PlaySound : MonoBehaviour
{
    [Tooltip("Clip joué automatiquement au démarrage si playAtStart est activé.")]
    public AudioClip soundClip; // Clip par défaut

    [Tooltip("Active la lecture automatique de 'soundClip' au Start.")]
    public bool playAtStart = false; // Contrôle la lecture au Start

    private AudioSource _audioSource; // Référence éventuelle à l'AudioSource locale

    private void Awake()
    {
        // Mémorise l'AudioSource pour éviter de l'obtenir à chaque lecture
        _audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // Joue éventuellement le clip par défaut au démarrage
        if (playAtStart && soundClip != null)
        {
            PlaySoundClip(soundClip);
        }
    }

    /// <summary>
    /// Joue un clip audio en utilisant l'AudioManager s'il existe,
    /// sinon via l'AudioSource locale. Cette méthode est privée pour
    /// centraliser la logique de lecture.
    /// </summary>
    /// <param name="clip">Le clip audio à jouer.</param>
    private void PlaySoundClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[PlaySound] Aucun clip fourni pour la lecture.");
            return;
        }

        // Priorité à l'AudioManager pour respecter les volumes globaux
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(clip);
        }
        // Repli sur une AudioSource locale si disponible
        else if (_audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("[PlaySound] AudioManager absent et aucune AudioSource trouvée sur l'objet.");
        }
    }

    /// <summary>
    /// Méthode publique destinée à être appelée depuis une Timeline
    /// (via un Signal, Activation Track, etc.).
    /// </summary>
    /// <param name="clip">Le clip audio à jouer.</param>
    public void PlaySoundFromTimeline(AudioClip clip)
    {
        PlaySoundClip(clip);
    }
}
