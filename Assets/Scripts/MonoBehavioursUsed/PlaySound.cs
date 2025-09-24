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
    public AudioClipSO soundClip; // Clip par défaut

    [Tooltip("Active la lecture automatique de 'soundClip' au Start.")]
    public bool playAtStart = false; // Contrôle la lecture au Start

    public AudioSource _audioSource; // Référence éventuelle à l'AudioSource locale

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
    /// <param name="clipAsset">Le clip audio à jouer.</param>
    private void PlaySoundClip(AudioClipSO clipAsset)
    {
        if (clipAsset == null || clipAsset.Clip == null)
        {
            Debug.LogWarning("[PlaySound] Aucun clip fourni pour la lecture.");
            return;
        }
        if (AudioManager.Instance != null)
        {
            // Passage par le gestionnaire global afin de respecter les volumes et priorités du mixage.
            AudioManager.Instance.PlaySfx(clipAsset);
            return;
        }

        if (_audioSource != null)
        {
            // Fallback local (scènes de test) : on applique malgré tout le volume conseillé par le designer.
            _audioSource.PlayOneShot(clipAsset.Clip, clipAsset.Volume);
            return;
        }

        Debug.LogWarning("[PlaySound] AudioManager absent et aucune AudioSource trouvée sur l'objet.");
    }

    /// <summary>
    /// Méthode publique destinée à être appelée depuis une Timeline
    /// (via un Signal, Activation Track, etc.).
    /// </summary>
    /// <param name="clipAsset">Le clip audio à jouer.</param>
    public void PlaySoundFromTimeline(AudioClipSO clipAsset)
    {
        PlaySoundClip(clipAsset);
    }
}
