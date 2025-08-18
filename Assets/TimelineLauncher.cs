using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System.Collections;

public class TimelineLauncher : MonoBehaviour
{
    public static TimelineLauncher Instance { get; private set; }

    [SerializeField] private PlayableDirector director;
    private Coroutine followCoroutine;
    public bool IsTimelineActive => director != null &&
        (director.state == PlayState.Playing || director.state == PlayState.Paused);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Lance une timeline en associant dynamiquement des GameObjects aux tracks, en fonction des noms de track.
    /// </summary>
    public void PlayTimeline(TimelineAsset timelineAsset, GameObject caster, string cameraTag)
    {
        if (timelineAsset == null || director == null)
        {
            Debug.LogError("[TimelineLauncher] TimelineAsset ou Director manquant !");
            return;
        }

        director.playableAsset = timelineAsset;

        GameObject cameraGO = null;
        Transform cameraParent = null;
        GameObject cameraRoot = null;
        if (!string.IsNullOrEmpty(cameraTag))
        {
            cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
            cameraParent = cameraGO != null ? cameraGO.transform.parent : null;
            cameraRoot = cameraParent != null ? cameraParent.gameObject : cameraGO;

            if (caster != null && cameraParent != null)
            {
                cameraParent.position = caster.transform.position;
                cameraParent.rotation = caster.transform.rotation;
            }
        }

        foreach (var output in timelineAsset.outputs)
        {
            string trackName = output.streamName;
            System.Type type = output.outputTargetType;

            if (trackName.ToLower().Contains("caster") && caster != null)
            {
                BindObjectToTrack(output, caster);
            }
            else if (trackName.ToLower().Contains("camera"))
            {
                // Si aucune caméra n'est spécifiée, on relie la track Camera au PNJ (caster).
                // Cela permet aux timelines déclenchées pendant un dialogue de viser directement le PNJ.
                if (cameraRoot != null)
                {
                    BindObjectToTrack(output, cameraRoot);
                }
                else if (caster != null)
                {
                    BindObjectToTrack(output, caster);
                }
                else
                {
                    Debug.LogWarning($"[TimelineLauncher] Aucun GameObject trouvé pour la track camera : {trackName}");
                }
            }
            else if (type != null && typeof(Component).IsAssignableFrom(type) && type.Name.Contains("SignalReceiver"))
            {
                // Récupère le SignalReceiver présent sur le même GameObject que le PlayableDirector
                Component receiver = director.GetComponent(type);
                if (receiver != null)
                {
                    director.SetGenericBinding(output.sourceObject, receiver);
                }
                else
                {
                    Debug.LogWarning($"[TimelineLauncher] {type.Name} manquant sur {director.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[TimelineLauncher] Aucun binding pour la track : {trackName}");
            }
        }

        // Établit également le binding pour le MarkerTrack afin que les signaux
        // placés directement sur la timeline sans track dédiée soient reçus.
        SignalReceiver markerReceiver = director.GetComponent<SignalReceiver>();
        if (markerReceiver != null && timelineAsset.markerTrack != null)
        {
            // Associe le MarkerTrack au SignalReceiver présent sur le PlayableDirector
            director.SetGenericBinding(timelineAsset.markerTrack, markerReceiver);
        }

        director.Play();

        if (caster != null && cameraParent != null)
        {
            if (followCoroutine != null)
                StopCoroutine(followCoroutine);
            followCoroutine = StartCoroutine(FollowCaster(cameraParent, caster.transform));
        }
    }

    /// <summary>
    /// Joue une timeline en ciblant automatiquement le PNJ actuellement en interaction.
    /// Utilisé depuis les timelines de dialogue pour lancer des timelines d'item ou de MusicalMove
    /// tout en conservant le PNJ comme cible pour les tracks "Caster" et "Camera".
    /// </summary>
    /// <param name="timelineAsset">Timeline à jouer.</param>
    public void PlayTimelineOnCurrentNPC(TimelineAsset timelineAsset)
    {
        // Récupère le PNJ en cours d'interaction via l'InteractionManager.
        GameObject npc = InteractionManager.Instance != null ? InteractionManager.Instance.currentInteractable : null;

        if (npc == null)
        {
            // Avertit si aucun PNJ n'est en interaction lorsque la méthode est appelée.
            Debug.LogWarning("[TimelineLauncher] Aucun PNJ courant pour jouer la timeline.");
            return;
        }

        // Appelle PlayTimeline sans tag de caméra : la track "Camera" sera donc reliée au PNJ.
        PlayTimeline(timelineAsset, npc, null);
    }

    private void BindObjectToTrack(PlayableBinding output, GameObject go)
    {
        if (output.outputTargetType == typeof(Animator))
        {
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
                director.SetGenericBinding(output.sourceObject, animator);
            else
                Debug.LogWarning($"[TimelineLauncher] Animator manquant sur {go.name}");
        }
        else
        {
            director.SetGenericBinding(output.sourceObject, go);
        }
    }

    private IEnumerator FollowCaster(Transform cameraParent, Transform caster)
    {
        while (director != null && director.state == PlayState.Playing)
        {
            if (cameraParent != null && caster != null)
            {
                cameraParent.position = caster.position;
                cameraParent.rotation = caster.rotation;
            }
            yield return null;
        }
    }

    public void StopTimeline()
    {
        if (director != null && (director.state == PlayState.Playing || director.state == PlayState.Paused))
        {
            director.Stop();
        }
    }
}
