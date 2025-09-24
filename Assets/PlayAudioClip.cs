using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Récepteur audio générique destiné aux <see cref="PlayableDirector"/> utilisés
/// dans les timelines. Il permet de relayer les signaux reçus par un
/// <see cref="SignalReceiver"/> vers l'<see cref="AudioManager"/> central afin
/// d'unifier la lecture des effets sonores et des voix.
/// </summary>
/// <remarks>
/// Ce script doit être placé sur le même GameObject que le PlayableDirector
/// contrôlant la Timeline. Les pistes de signaux peuvent ensuite pointer sur
/// ses méthodes publiques pour déclencher des sons synchronisés.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[RequireComponent(typeof(SignalReceiver))]
public class PlayAudioClip : MonoBehaviour
{
    /// <summary>
    /// Canal utilisé pour déterminer quelle famille de sons doit lire le clip.
    /// Les timelines peuvent ainsi distinguer facilement les voix des SFX.
    /// </summary>
    public enum AudioChannel
    {
        SoundEffect = 0,
        Voice = 1,
    }

    [Header("Références")]
    [SerializeField]
    [Tooltip("Référence explicite vers l'AudioManager. Si elle est laissée vide, la recherche automatique sera utilisée lors de l'exécution.")]
    private AudioManager audioManager;

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

    /// <summary>
    /// Tente de mettre en cache une référence vers l'AudioManager existant.
    /// Cette méthode est utilisée par Reset/Awake/OnValidate pour garantir
    /// que le script reste opérationnel même si la scène est dupliquée.
    /// </summary>
    private void CacheAudioManager()
    {
        if (audioManager != null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            audioManager = AudioManager.Instance;
            return;
        }

        audioManager = FindObjectOfType<AudioManager>();
    }

    private void Reset()
    {
        CacheAudioManager();
    }

    private void Awake()
    {
        CacheAudioManager();
    }

    private void OnValidate()
    {
        if (overrideVolume)
        {
            volumeOverride = Mathf.Clamp01(volumeOverride);
        }

        CacheAudioManager();
    }

    /// <summary>
    /// Détermine si la lecture est autorisée dans le contexte actuel.
    /// Empêche par défaut la lecture involontaire lorsqu'on scrube une timeline
    /// dans l'Éditeur. L'utilisateur peut toutefois activer la prévisualisation.
    /// </summary>
    private bool CanPlayNow()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !allowPreviewInEditor)
        {
            return false;
        }
#endif
        return true;
    }

    /// <summary>
    /// Joue un <see cref="AudioClip"/> fourni par une Timeline.
    /// </summary>
    /// <param name="clip">Clip brut assigné dans le marqueur de signal.</param>
    public void PlayClip(AudioClip clip)
    {
        if (!CanPlayNow())
        {
            return;
        }

        AudioManager manager = ResolveAudioManager();
        if (manager == null)
        {
            Debug.LogWarning("[PlayAudioClip] Aucun AudioManager disponible pour lire le clip.");
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
    /// Joue un <see cref="AudioClipSO"/> de la bibliothèque audio du jeu.
    /// Utilise les paramètres de volume/boucle définis dans le ScriptableObject
    /// sauf si un volume personnalisé est activé.
    /// </summary>
    /// <param name="clipAsset">Asset audio enrichi regroupant clip, volume et boucle.</param>
    public void PlayClipAsset(AudioClipSO clipAsset)
    {
        if (!CanPlayNow())
        {
            return;
        }

        AudioManager manager = ResolveAudioManager();
        if (manager == null)
        {
            Debug.LogWarning("[PlayAudioClip] Aucun AudioManager disponible pour lire l'AudioClipSO.");
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
    /// Permet aux signaux Timeline de changer dynamiquement le canal cible
    /// (SFX ou Voice) avant de déclencher la lecture suivante.
    /// </summary>
    /// <param name="channel">Nouvelle valeur d'énumération à utiliser.</param>
    public void SetChannel(AudioChannel channel)
    {
        defaultChannel = channel;
    }

    /// <summary>
    /// Variante acceptant un entier (utile si le SignalReceiver ne peut pas
    /// sérialiser directement l'énumération). Les valeurs invalides sont clampées.
    /// </summary>
    /// <param name="channelIndex">Indice du canal à utiliser.</param>
    public void SetChannelByIndex(int channelIndex)
    {
        int max = System.Enum.GetValues(typeof(AudioChannel)).Length - 1;
        channelIndex = Mathf.Clamp(channelIndex, 0, max);
        defaultChannel = (AudioChannel)channelIndex;
    }

    /// <summary>
    /// Résout la référence à l'AudioManager en tentant une recherche paresseuse.
    /// </summary>
    private AudioManager ResolveAudioManager()
    {
        if (audioManager == null)
        {
            CacheAudioManager();
        }

        return audioManager;
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
                if (useCustomVolume)
                {
                    manager.PlayVoice(clip, volumeToUse, loopRawClips);
                }
                else
                {
                    manager.PlayVoice(clip, loop: loopRawClips);
                }
                break;
            default:
                if (useCustomVolume)
                {
                    manager.PlaySfx(clip, volumeToUse, loopRawClips);
                }
                else
                {
                    manager.PlaySfx(clip, loop: loopRawClips);
                }
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
                if (overrideVolume)
                {
                    manager.PlayVoice(clipAsset, Mathf.Clamp01(volumeOverride));
                }
                else
                {
                    manager.PlayVoice(clipAsset);
                }
                break;
            default:
                if (overrideVolume)
                {
                    manager.PlaySfx(clip, Mathf.Clamp01(volumeOverride), clipAsset.Loop);
                }
                else
                {
                    manager.PlaySfx(clipAsset);
                }
                break;
        }
    }
}
