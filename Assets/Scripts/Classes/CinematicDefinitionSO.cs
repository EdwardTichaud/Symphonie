using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CreateAssetMenu(fileName = "CinematicDefinition", menuName = "Symphonie/Cinematics/Cinematic Definition")]
public class CinematicDefinitionSO : ScriptableObject
{
    [Header("Playable")]
    [Tooltip("TimelineAsset a jouer via TimelineManager si aucun prefab n'est fourni.")]
    public TimelineAsset timelineAsset;

    [Tooltip("Prefab contenant un PlayableDirector configure (bindings, SignalReceiver, etc.).")]
    public PlayableDirector directorPrefab;

    [Tooltip("Detruit l'instance du prefab apres la lecture.")]
    public bool destroyAfterPlay = true;

    [Header("Bindings (TimelineAsset uniquement)")]
    [Tooltip("Tag utilise comme caster pour les timelines joue es via TimelineManager.")]
    public string casterTag;

    [Tooltip("Tag de la camera a utiliser pour les timelines jouees via TimelineManager.")]
    public string cameraTag = "WorldCamera";

    [Header("Lecture")]
    public bool withFade = true;
    public bool interruptMusic = true;
    public bool allowSkip = true;
    public bool autoRestore = true;
    public bool requiresWorldCamera = true;

    public bool HasPlayable => timelineAsset != null || directorPrefab != null;
}
