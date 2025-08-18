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
        Transform cameraParent = null;   // Parent direct de la caméra pour récupérer son Animator
        if (!string.IsNullOrEmpty(cameraTag))
        {
            cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
            cameraParent = cameraGO != null ? cameraGO.transform.parent : null;
            if (caster != null && cameraParent != null)
            {
                // Replace le parent de la caméra sur le PNJ afin que l'animation suive correctement le mouvement
                cameraParent.position = caster.transform.position;
                cameraParent.rotation = caster.transform.rotation;
            }
        }

        foreach (var output in timelineAsset.outputs)
        {
            string trackName = output.streamName;
            System.Type type = output.outputTargetType;

            // Pour les timelines de PNJ, le track d'animation s'appelle "PNJ" au lieu de "Caster"
            if ((trackName.ToLower().Contains("caster") || trackName.ToLower().Contains("pnj")) && caster != null)
            {
                BindObjectToTrack(output, caster);
            }
            else if (trackName.ToLower().Contains("camera"))
            {
                // L'animation de la caméra doit utiliser l'Animator situé sur le parent de la WorldCamera.
                // On tente donc de récupérer cet Animator explicitement.
                if (cameraGO != null)
                {
                    Animator camAnimator = cameraParent != null
                        ? cameraParent.GetComponent<Animator>()
                        : cameraGO.GetComponent<Animator>();

                    if (camAnimator != null)
                    {
                        director.SetGenericBinding(output.sourceObject, camAnimator);
                    }
                    else
                    {
                        Debug.LogWarning($"[TimelineLauncher] Animator manquant pour la caméra {cameraTag}");
                    }
                }
                else if (caster != null)
                {
                    // Aucun tag de caméra fourni : on se rabat sur le PNJ.
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

        // Informe le TimelineManager afin que la caméra sache qu'une Timeline prend la priorité
        // et qu'aucun autre contrôleur (comme le CameraController) ne vienne interférer.
        if (TimelineManager.Instance != null)
        {
            // Le TimelineManager gère l'état global et stoppe les autres timelines si besoin
            TimelineManager.Instance.PlayTimeline(director);
        }
        else
        {
            // En dernier recours on lance directement la Timeline
            director.Play();
        }

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
    /// tout en conservant le PNJ comme cible pour les tracks "PNJ" et "Camera".
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

        // Utilise la WorldCamera : la track "Camera" ira chercher l'Animator du parent de la WorldCamera.
        PlayTimeline(timelineAsset, npc, "WorldCamera");
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
