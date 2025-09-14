using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System;

/// <summary>
/// Gestionnaire centralisant les <see cref="TimelineAsset"/> jouées durant les combats.
/// Désormais seule la timeline du lanceur est utilisée, la caméra étant gérée par ailleurs.
/// </summary>
public class BattleTimelineManager : MonoBehaviour
{
    /// <summary>Instance statique accessible depuis les autres scripts.</summary>
    public static BattleTimelineManager Instance { get; private set; }

    /// <summary>Director chargé des animations du lanceur.</summary>
    private PlayableDirector directorCaster;

    private void Awake()
    {
        // Mise en place classique du singleton.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche du PlayableDirector dédié au caster.
        Transform casterChild = transform.Find("PlayableDirector_Caster");
        directorCaster = casterChild != null ? casterChild.GetComponent<PlayableDirector>() : null;

        if (directorCaster == null)
            Debug.LogError("[BattleTimelineManager] PlayableDirector_Caster introuvable.");
    }

    /// <summary>
    /// Aligne l'origine de la caméra de combat sur la cible donnée.
    /// </summary>
    /// <param name="cameraTarget">Nouvelle ancre pour la caméra.</param>
    /// <param name="cameraTag">Tag de la caméra à repositionner.</param>
    /// <param name="fixedRotation">Rotation imposée, ou null pour reprendre celle de la cible.</param>
    public void AlignCameraToTarget(GameObject cameraTarget, string cameraTag, Quaternion? fixedRotation = null)
    {
        if (cameraTarget == null || string.IsNullOrEmpty(cameraTag))
            return;

        // On récupère le parent direct (BattleCamera_Origin) pour déplacer l'ensemble du rig.
        GameObject cameraGO = GameObject.FindGameObjectWithTag(cameraTag);
        Transform cameraParent = cameraGO != null ? cameraGO.transform.parent : null;

        if (cameraParent != null)
        {
            cameraParent.position = cameraTarget.transform.position;
            cameraParent.rotation = fixedRotation ?? cameraTarget.transform.rotation;
        }
    }

    /// <summary>
    /// Joue uniquement la timeline du lanceur. Aucune piste caméra n'est prise en compte.
    /// </summary>
    /// <param name="timeline">Timeline à exécuter.</param>
    /// <param name="caster">Objet servant de référence pour les bindings.</param>
    public void PlayCasterTimeline(TimelineAsset timeline, GameObject caster)
    {
        if (timeline == null || directorCaster == null)
            return;

        directorCaster.playableAsset = timeline;

        // Parcourt toutes les pistes pour relier dynamiquement les bons objets.
        foreach (var output in timeline.outputs)
        {
            Type type = output.outputTargetType;
            string lower = output.streamName.ToLower();

            // Les pistes caméra sont ignorées : la caméra n'est plus contrôlée par timeline.
            if (lower.Contains("camera"))
                continue;

            // Les pistes de signaux sont reliées au SignalReceiver du PlayableDirector.
            if (type != null && typeof(Component).IsAssignableFrom(type) && type.Name.Contains("SignalReceiver"))
            {
                Component receiver = directorCaster.GetComponent(type);
                if (receiver != null)
                {
                    directorCaster.SetGenericBinding(output.sourceObject, receiver);
                }
                else
                {
                    Debug.LogWarning($"[BattleTimelineManager] {type.Name} manquant sur {directorCaster.gameObject.name} pour la timeline.");
                }
                continue;
            }

            // Pistes d'animation : on tente de lier un Animator du lanceur.
            if (lower.Contains("caster") || lower.Contains("pnj"))
            {
                if (caster == null)
                    continue;

                var animator = caster.GetComponentInChildren<Animator>();
                if (animator != null)
                    directorCaster.SetGenericBinding(output.sourceObject, animator);
                else
                    Debug.LogWarning("[BattleTimelineManager] Animator manquant sur le caster pour la timeline.");
            }
            else if (caster != null)
            {
                // Autres pistes : on relie simplement le GameObject du lanceur.
                directorCaster.SetGenericBinding(output.sourceObject, caster);
            }
        }

        directorCaster.time = 0;
        directorCaster.Play();
    }

    /// <summary>Arrête la timeline du lanceur si elle est en cours.</summary>
    public void StopCasterTimeline()
    {
        if (directorCaster != null && directorCaster.state == PlayState.Playing)
            directorCaster.Stop();
    }

    /// <summary>Indique si le director du lanceur joue actuellement une timeline.</summary>
    public bool IsCasterTimelinePlaying =>
        directorCaster != null && directorCaster.state == PlayState.Playing;
}

