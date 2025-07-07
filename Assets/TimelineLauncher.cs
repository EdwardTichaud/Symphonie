using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;

public class TimelineLauncher : MonoBehaviour
{
    public static TimelineLauncher Instance { get; private set; }

    [SerializeField] private PlayableDirector director;

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

        foreach (var output in timelineAsset.outputs)
        {
            string trackName = output.streamName;
            System.Type type = output.outputTargetType;

            if (trackName.ToLower().Contains("Caster") && caster != null)
            {
                BindObjectToTrack(output, caster);
            }
            else if (trackName.ToLower().Contains("Camera") && cameraTag != null)
            {
                GameObject cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
                BindObjectToTrack(output, cameraGO);
            }
            else
            {
                Debug.LogWarning($"[TimelineLauncher] Aucun binding pour la track : {trackName}");
            }
        }

        director.Play();
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
}
