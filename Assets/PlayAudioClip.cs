using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[RequireComponent(typeof(SignalReceiver))]
public class PlayAudioClip : MonoBehaviour
{
    /// <summary>
    /// Canal utilisé pour déterminer quelle famille de sons doit lire le clip.
    /// </summary>
    public enum AudioChannel
    {
        SoundEffect = 0,
        Voice = 1,
    }

    [Header("Paramètres par défaut")]
    [SerializeField]
    [Tooltip("Canal privilégié lorsque la Timeline demande la lecture d'un clip.")]
    private AudioChannel defaultChannel = AudioChannel.SoundEffect;

    [SerializeField]
    [Tooltip("Active un volume personnalisé pour les clips fournis directement (AudioClip ou AudioClipSO).")]
    private bool overrideVolume = false;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Valeur de volume utilisée lorsque 'Override Volume' est activé.")]
    private float volumeOverride = 0.8f;

    [SerializeField]
    [Tooltip("Permet de boucler les AudioClip standards déclenchés par la Timeline. Les AudioClipSO utilisent leur propre configuration de boucle.")]
    private bool loopRawClips = false;

    [SerializeField]
    [Tooltip("Autorise la lecture lorsque la Timeline est prévisualisée dans l'Éditeur (en dehors du Play Mode).")]
    private bool allowPreviewInEditor = false;

    private void OnValidate()
    {
        if (overrideVolume)
            volumeOverride = Mathf.Clamp01(volumeOverride);
    }

    /// <summary>
    /// Empêche par défaut la lecture involontaire en scrub dans l'Éditeur.
    /// </summary>
    private bool CanPlayNow()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !allowPreviewInEditor)
            return false;
#endif
        return true;
    }

    /// <summary>
    /// Joue un AudioClip brut fourni par une Timeline.
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (!CanPlayNow())
            return;

        var manager = AudioManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[PlayAudioClip] Aucun AudioManager.Instance disponible pour lire le clip.");
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning("[PlayAudioClip] Clip audio null reçu depuis la Timeline.");
            return;
        }

        float? customVolume = overrideVolume ? Mathf.Clamp01(volumeOverride) : null;
        PlayRawClip(manager, clip, customVolume);
    }

    /// <summary>
    /// Joue un AudioClipSO (bibliothèque audio).
    /// </summary>
    public void PlayClipAsset(AudioClipSO clipAsset)
    {
        if (!CanPlayNow())
            return;

        var manager = AudioManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[PlayAudioClip] Aucun AudioManager.Instance disponible pour lire l'AudioClipSO.");
            return;
        }

        if (clipAsset == null)
        {
            Debug.LogWarning("[PlayAudioClip] AudioClipSO null reçu depuis la Timeline.");
            return;
        }

        PlayAssetClip(manager, clipAsset);
    }

    /// <summary>
    /// Change dynamiquement le canal cible (SFX/Voice).
    /// </summary>
    public void SetChannel(AudioChannel channel)
    {
        defaultChannel = channel;
    }

    /// <summary>
    /// Variante via index entier (utile pour certains receivers).
    /// </summary>
    public void SetChannelByIndex(int channelIndex)
    {
        int max = System.Enum.GetValues(typeof(AudioChannel)).Length - 1;
        channelIndex = Mathf.Clamp(channelIndex, 0, max);
        defaultChannel = (AudioChannel)channelIndex;
    }

    /// <summary>
    /// Logique de lecture pour les clips Unity bruts.
    /// </summary>
    private void PlayRawClip(AudioManager manager, AudioClip clip, float? customVolume)
    {
        float volumeToUse = customVolume.HasValue ? customVolume.Value : 0f;
        bool useCustomVolume = customVolume.HasValue;

        switch (defaultChannel)
        {
            case AudioChannel.Voice:
                if (useCustomVolume) manager.PlayVoice(clip, volumeToUse, loopRawClips);
                else manager.PlayVoice(clip, loop: loopRawClips);
                break;

            default:
                if (useCustomVolume) manager.PlaySfx(clip, volumeToUse, loopRawClips);
                else manager.PlaySfx(clip, loop: loopRawClips);
                break;
        }
    }

    /// <summary>
    /// Logique de lecture pour les ScriptableObjects dédiés.
    /// </summary>
    private void PlayAssetClip(AudioManager manager, AudioClipSO clipAsset)
    {
        AudioClip clip = clipAsset.Clip;
        if (clip == null)
        {
            Debug.LogWarning("[PlayAudioClip] AudioClipSO fourni sans AudioClip concret.");
            return;
        }

        switch (defaultChannel)
        {
            case AudioChannel.Voice:
                if (overrideVolume) manager.PlayVoice(clipAsset, Mathf.Clamp01(volumeOverride));
                else manager.PlayVoice(clipAsset);
                break;

            default:
                if (overrideVolume) manager.PlaySfx(clip, Mathf.Clamp01(volumeOverride), clipAsset.Loop);
                else manager.PlaySfx(clipAsset);
                break;
        }
    }
}
