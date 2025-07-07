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
            else if (trackName.ToLower().Contains("camera") && cameraTag != null)
            {
                BindObjectToTrack(output, cameraRoot);
            }
            else
            {
                Debug.LogWarning($"[TimelineLauncher] Aucun binding pour la track : {trackName}");
            }
        }

        director.Play();

        if (caster != null && cameraParent != null)
        {
            if (followCoroutine != null)
                StopCoroutine(followCoroutine);
            followCoroutine = StartCoroutine(FollowCaster(cameraParent, caster.transform));
        }
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
}
