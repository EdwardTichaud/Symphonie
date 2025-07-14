using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System.Collections;

public class TimelineLauncher : MonoBehaviour
{
    public static TimelineLauncher Instance { get; private set; }

    [SerializeField] private PlayableDirector director;
    [SerializeField, Tooltip("Durée de transition vers le début de la timeline")]
    private float transitionDuration = 0.5f; // temps de lerp avant lecture
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

        StartCoroutine(PlayTimelineRoutine(timelineAsset, caster, cameraTag));
    }

    private IEnumerator PlayTimelineRoutine(TimelineAsset timelineAsset, GameObject caster, string cameraTag)
    {
        director.playableAsset = timelineAsset;
        // On souhaite que la Timeline se joue une seule fois. Les animations en
        // loop continueront grâce à leur propre réglage de boucle, tandis que
        // les autres resteront sur leur dernière frame.
        director.extrapolationMode = DirectorWrapMode.Hold;

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
            if (trackName.ToLower().Contains("caster") && caster != null)
            {
                BindObjectToTrack(output, caster);
            }
            else if (trackName.ToLower().Contains("camera") && cameraTag != null)
            {
                BindObjectToTrack(output, cameraRoot);
            }
            else
            {
                Debug.LogWarning($"[TimelineLauncher] Aucun binding pour la track : {trackName}");
            }
        }

        // --- Transition en douceur vers la première position de la Timeline ---
        if (cameraRoot != null)
        {
            Transform cam = cameraRoot.transform;
            Vector3 startPos = cam.position;
            Quaternion startRot = cam.rotation;

            // Évaluer la timeline à t=0 pour connaître la position initiale
            Vector3 initPos = startPos;
            Quaternion initRot = startRot;
            double storedTime = director.time;
            director.time = 0;
            director.Evaluate();
            Vector3 targetPos = cam.position;
            Quaternion targetRot = cam.rotation;
            cam.SetPositionAndRotation(initPos, initRot); // Revenir à l'état de départ
            director.time = storedTime;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                cam.position = Vector3.Lerp(initPos, targetPos, t);
                cam.rotation = Quaternion.Slerp(initRot, targetRot, t);
                yield return null;
            }
            cam.SetPositionAndRotation(targetPos, targetRot);
        }

        // On passe par le TimelineManager pour garantir la priorité
        if (TimelineManager.Instance != null)
            TimelineManager.Instance.PlayTimeline(director);
        else
            director.Play();

        if (caster != null && cameraParent != null)
        {
            if (followCoroutine != null)
                StopCoroutine(followCoroutine);
            followCoroutine = StartCoroutine(FollowCaster(cameraParent, caster.transform));
        }

        // Attendre la fin pour laisser le CameraController reprendre la main
        yield return new WaitWhile(() => director != null && director.state == PlayState.Playing);
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
