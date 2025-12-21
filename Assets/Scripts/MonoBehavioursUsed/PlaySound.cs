using UnityEngine;

/// <summary>
/// Permet de jouer un son soit automatiquement au démarrage,
/// soit sur demande (notamment depuis une Timeline).
/// Utilise l'<see cref="AudioManager"/> pour garder
/// une gestion centralisée du volume.
/// </summary>
public class PlaySound : MonoBehaviour
{
    [Tooltip("Clip joué automatiquement au démarrage si playAtStart est activé.")]
    public AudioClipSO soundClip; // Clip par défaut

    [Tooltip("Active la lecture automatique de 'soundClip' au Start.")]
    public bool playAtStart = false; // Contrôle la lecture au Start

    private void Start()
    {
        // Joue éventuellement le clip par défaut au démarrage
        if (playAtStart && soundClip != null)
        {
            PlaySoundClip(soundClip);
        }
    }

    /// <summary>
    /// Joue un clip audio via l'AudioManager.
    /// </summary>
    /// <param name="clipAsset">Le clip audio à jouer.</param>
    private void PlaySoundClip(AudioClipSO clipAsset)
    {
        if (clipAsset == null || clipAsset.Clip == null)
        {
            Debug.LogWarning("[PlaySound] Aucun clip fourni pour la lecture.");
            return;
        }
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[PlaySound] AudioManager absent, impossible de jouer le clip.");
            return;
        }

        if (clipAsset.type == AudioClipSO.AudioClipType.Music)
            AudioManager.Instance.PlayMusicOverride(clipAsset);
        else
            AudioManager.Instance.PlaySfx(clipAsset);
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
