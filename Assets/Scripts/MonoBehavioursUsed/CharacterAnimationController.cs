using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralise toutes les interactions avec l'Animator des personnages jouables et non jouables.
/// L'objectif est d'offrir un point d'entrée unique pour piloter les deux layers (corps / visage)
/// à l'aide de paramètres, tout en conservant un système de secours lorsque le contrôleur n'est
/// pas encore configuré pour ce pipeline.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CharacterAnimationController : MonoBehaviour
{
    /// <summary>
    /// Ensemble des états d'animation pilotés par le layer "Body".
    /// Chaque valeur de l'énumération correspond à un entier qui pourra être envoyé
    /// dans l'Animator afin d'alimenter un blend-tree ou un sous-graph.
    /// </summary>
    public enum BodyAnimationState
    {
        None = -1,
        IdleWorld = 0,
        Walk = 1,
        Run = 2,
        JumpStart = 3,
        JumpLoop = 4,
        Landing = 5,
        LandingMoving = 6,
        IdleBattle = 7,
        Turn90 = 8,
        DashBattle = 9,
        RetreatBattle = 10,
        ItemPrepare = 11,
        Death = 12,
        HitFront = 13,
        HitBack = 14,
        HitLeft = 15,
        HitRight = 16
    }

    /// <summary>
    /// États utilisables par le layer "Face". Les valeurs sont volontairement réduites
    /// car la plupart des expressions seront gérées via des blend-shapes.
    /// </summary>
    public enum FaceAnimationState
    {
        Neutral = 0,
        Speaking = 1,
        Focused = 2,
        Hurt = 3
    }

    /// <summary>
    /// Triggers fréquemment exploités par les scripts de gameplay.
    /// Ces triggers permettent de synchroniser des animations ponctuelles
    /// (ex : sortie d'une action, rotation instantanée, etc.).
    /// </summary>
    public enum BodyAnimationTrigger
    {
        None = 0,
        ExitAction = 1,
        Turn = 2
    }

    [Serializable]
    private struct BodyStateFallback
    {
        public BodyAnimationState state;
        public string clipName;
    }

    [Serializable]
    private struct FaceStateFallback
    {
        public FaceAnimationState state;
        public string clipName;
    }

    [Serializable]
    private struct BodyTriggerParameter
    {
        public BodyAnimationTrigger trigger;
        public string parameterName;
    }

    [Header("Paramètres du Layer Body")]
    [Tooltip("Nom du paramètre int qui sélectionne l'état principal du corps.")]
    [SerializeField] private string bodyStateParameter = "BodyState";

    [Tooltip("Nom du paramètre float qui décrit la durée de transition souhaitée.")]
    [SerializeField] private string bodyTransitionDurationParameter = "BodyTransition";

    [Tooltip("Paramètre float permettant d'indiquer la position de départ normalisée (0-1).")]
    [SerializeField] private string bodyNormalizedTimeParameter = "BodyNormalizedTime";

    [Tooltip("Trigger optionnel pour forcer une transition instantanée côté corps.")]
    [SerializeField] private string bodyInstantTransitionParameter = "BodyInstant";

    [Tooltip("Paramètre float servant à transmettre la vitesse actuelle (0 à 1).")]
    [SerializeField] private string bodySpeedParameter = "BodySpeed";

    [Header("Paramètres du Layer Face")]
    [Tooltip("Nom du paramètre int qui sélectionne l'expression faciale active.")]
    [SerializeField] private string faceStateParameter = "FaceState";

    [Tooltip("Paramètre float pour ajuster la durée de transition du visage.")]
    [SerializeField] private string faceTransitionDurationParameter = "FaceTransition";

    [Tooltip("Trigger optionnel pour exiger une mise à jour immédiate du visage.")]
    [SerializeField] private string faceInstantTransitionParameter = "FaceInstant";

    [Header("Triggers connus du layer Body")]
    [SerializeField] private BodyTriggerParameter[] bodyTriggers =
    {
        new BodyTriggerParameter { trigger = BodyAnimationTrigger.ExitAction, parameterName = "exitAction" },
        new BodyTriggerParameter { trigger = BodyAnimationTrigger.Turn, parameterName = "isTurning" }
    };

    [Header("Clips de secours (Body)")]
    [SerializeField] private BodyStateFallback[] bodyFallbackClips =
    {
        new BodyStateFallback { state = BodyAnimationState.IdleWorld, clipName = "Idle_World" },
        new BodyStateFallback { state = BodyAnimationState.Walk, clipName = "Walk_Start" },
        new BodyStateFallback { state = BodyAnimationState.Run, clipName = "Run Start" },
        new BodyStateFallback { state = BodyAnimationState.JumpStart, clipName = "Jump_Start" },
        new BodyStateFallback { state = BodyAnimationState.JumpLoop, clipName = "Jump_Loop" },
        new BodyStateFallback { state = BodyAnimationState.Landing, clipName = "Landing" },
        new BodyStateFallback { state = BodyAnimationState.LandingMoving, clipName = "Landing_OnMove" },
        new BodyStateFallback { state = BodyAnimationState.IdleBattle, clipName = "Idle_Battle" },
        new BodyStateFallback { state = BodyAnimationState.Turn90, clipName = "Turn_90" },
        new BodyStateFallback { state = BodyAnimationState.DashBattle, clipName = "Dash_Battle" },
        new BodyStateFallback { state = BodyAnimationState.RetreatBattle, clipName = "Retreat_Battle" },
        new BodyStateFallback { state = BodyAnimationState.ItemPrepare, clipName = "Item_Prepare" },
        new BodyStateFallback { state = BodyAnimationState.Death, clipName = "Death" },
        new BodyStateFallback { state = BodyAnimationState.HitFront, clipName = "Hit_F" },
        new BodyStateFallback { state = BodyAnimationState.HitBack, clipName = "Hit_B" },
        new BodyStateFallback { state = BodyAnimationState.HitLeft, clipName = "Hit_L" },
        new BodyStateFallback { state = BodyAnimationState.HitRight, clipName = "Hit_R" }
    };

    [Header("Clips de secours (Face)")]
    [SerializeField] private FaceStateFallback[] faceFallbackClips =
    {
        new FaceStateFallback { state = FaceAnimationState.Neutral, clipName = string.Empty },
        new FaceStateFallback { state = FaceAnimationState.Speaking, clipName = string.Empty },
        new FaceStateFallback { state = FaceAnimationState.Focused, clipName = string.Empty },
        new FaceStateFallback { state = FaceAnimationState.Hurt, clipName = string.Empty }
    };

    private Animator cachedAnimator;

    private readonly Dictionary<BodyAnimationState, string> bodyFallbackLookup = new();
    private readonly Dictionary<FaceAnimationState, string> faceFallbackLookup = new();
    private readonly Dictionary<BodyAnimationTrigger, int> bodyTriggerHashes = new();

    private bool bodyStateParameterAvailable;
    private bool bodyTransitionParameterAvailable;
    private bool bodyNormalizedTimeParameterAvailable;
    private bool bodyInstantParameterAvailable;
    private bool bodySpeedParameterAvailable;

    private bool faceStateParameterAvailable;
    private bool faceTransitionParameterAvailable;
    private bool faceInstantParameterAvailable;

    private int bodyStateHash;
    private int bodyTransitionDurationHash;
    private int bodyNormalizedTimeHash;
    private int bodyInstantHash;
    private int bodySpeedHash;

    private int faceStateHash;
    private int faceTransitionDurationHash;
    private int faceInstantHash;

    /// <summary>
    /// Accès sécurisé à l'Animator piloté par ce contrôleur.
    /// </summary>
    public Animator Animator => cachedAnimator;

    void Awake()
    {
        cachedAnimator = GetComponent<Animator>();
        CacheFallbackDictionaries();
        CacheParameterAvailability();
    }

    void OnValidate()
    {
        // Dès que l'on modifie une configuration dans l'inspecteur on régénère les caches.
        if (cachedAnimator == null)
            cachedAnimator = GetComponent<Animator>();

        CacheFallbackDictionaries();
        CacheParameterAvailability();
    }

    /// <summary>
    /// Regénère explicitement les caches lorsque le composant est ajouté dynamiquement.
    /// </summary>
    public void RefreshCachedParameters()
    {
        // Lorsqu'un personnage est instancié pour la prévisualisation depuis l'éditeur,
        // Unity n'exécute pas systématiquement Awake(). Sans cet appel, le cache de
        // l'Animator reste vide et aucun paramètre n'est détecté par le testeur
        // d'animations. On récupère donc explicitement la référence pour garantir que
        // les méthodes de prévisualisation puissent piloter correctement le layer Body.
        if (cachedAnimator == null)
        {
            cachedAnimator = GetComponent<Animator>();
        }

        CacheFallbackDictionaries();
        CacheParameterAvailability();
    }

    private void CacheFallbackDictionaries()
    {
        bodyFallbackLookup.Clear();
        faceFallbackLookup.Clear();

        if (bodyFallbackClips != null)
        {
            foreach (var entry in bodyFallbackClips)
            {
                if (!bodyFallbackLookup.ContainsKey(entry.state))
                    bodyFallbackLookup.Add(entry.state, entry.clipName);
                else
                    bodyFallbackLookup[entry.state] = entry.clipName;
            }
        }

        if (faceFallbackClips != null)
        {
            foreach (var entry in faceFallbackClips)
            {
                if (!faceFallbackLookup.ContainsKey(entry.state))
                    faceFallbackLookup.Add(entry.state, entry.clipName);
                else
                    faceFallbackLookup[entry.state] = entry.clipName;
            }
        }
    }

    private void CacheParameterAvailability()
    {
        bodyStateParameterAvailable = TryCacheParameter(bodyStateParameter, AnimatorControllerParameterType.Int, out bodyStateHash);
        bodyTransitionParameterAvailable = TryCacheParameter(bodyTransitionDurationParameter, AnimatorControllerParameterType.Float, out bodyTransitionDurationHash);
        bodyNormalizedTimeParameterAvailable = TryCacheParameter(bodyNormalizedTimeParameter, AnimatorControllerParameterType.Float, out bodyNormalizedTimeHash);
        bodyInstantParameterAvailable = TryCacheParameter(bodyInstantTransitionParameter, AnimatorControllerParameterType.Trigger, out bodyInstantHash);
        bodySpeedParameterAvailable = TryCacheParameter(bodySpeedParameter, AnimatorControllerParameterType.Float, out bodySpeedHash);

        faceStateParameterAvailable = TryCacheParameter(faceStateParameter, AnimatorControllerParameterType.Int, out faceStateHash);
        faceTransitionParameterAvailable = TryCacheParameter(faceTransitionDurationParameter, AnimatorControllerParameterType.Float, out faceTransitionDurationHash);
        faceInstantParameterAvailable = TryCacheParameter(faceInstantTransitionParameter, AnimatorControllerParameterType.Trigger, out faceInstantHash);

        bodyTriggerHashes.Clear();
        if (bodyTriggers != null)
        {
            foreach (var trigger in bodyTriggers)
            {
                if (trigger.trigger == BodyAnimationTrigger.None)
                    continue;

                if (string.IsNullOrEmpty(trigger.parameterName))
                    continue;

                if (TryCacheParameter(trigger.parameterName, AnimatorControllerParameterType.Trigger, out int hash))
                {
                    bodyTriggerHashes[trigger.trigger] = hash;
                }
            }
        }
    }

    private bool TryCacheParameter(string parameterName, AnimatorControllerParameterType expectedType, out int hash)
    {
        hash = 0;
        if (cachedAnimator == null || string.IsNullOrEmpty(parameterName))
            return false;

        int candidateHash = Animator.StringToHash(parameterName);
        foreach (var parameter in cachedAnimator.parameters)
        {
            if (parameter.nameHash == candidateHash && parameter.type == expectedType)
            {
                hash = candidateHash;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Définit l'état du corps via les paramètres prévus dans l'Animator.
    /// Un système de secours rejoue le clip correspondant si les paramètres ne sont pas configurés.
    /// </summary>
    public void SetBodyState(BodyAnimationState state, float transitionDuration = 0.1f, float normalizedStartTime = 0f, bool forceInstantTransition = false, int layerIndex = 0)
    {
        if (cachedAnimator == null)
            return;

        bool parameterUsed = false;

        if (bodyTransitionParameterAvailable)
        {
            cachedAnimator.SetFloat(bodyTransitionDurationHash, Mathf.Max(0f, transitionDuration));
            parameterUsed = true;
        }

        if (bodyNormalizedTimeParameterAvailable)
        {
            cachedAnimator.SetFloat(bodyNormalizedTimeHash, Mathf.Clamp01(normalizedStartTime));
            parameterUsed = true;
        }

        if (forceInstantTransition && bodyInstantParameterAvailable)
        {
            cachedAnimator.ResetTrigger(bodyInstantHash);
            cachedAnimator.SetTrigger(bodyInstantHash);
            parameterUsed = true;
        }

        if (bodyStateParameterAvailable)
        {
            cachedAnimator.SetInteger(bodyStateHash, (int)state);
            parameterUsed = true;
        }

        if (!parameterUsed)
        {
            PlayFallbackClip(state, transitionDuration, normalizedStartTime, layerIndex);
        }
    }

    /// <summary>
    /// Ajuste une vitesse normalisée pour alimenter un blend tree de locomotion.
    /// </summary>
    public void SetBodySpeed(float normalizedSpeed)
    {
        if (!bodySpeedParameterAvailable || cachedAnimator == null)
            return;

        cachedAnimator.SetFloat(bodySpeedHash, Mathf.Clamp01(normalizedSpeed));
    }

    /// <summary>
    /// Déclenche un trigger associé au layer Body.
    /// </summary>
    public void ActivateBodyTrigger(BodyAnimationTrigger trigger)
    {
        if (trigger == BodyAnimationTrigger.None || cachedAnimator == null)
            return;

        if (bodyTriggerHashes.TryGetValue(trigger, out int hash))
        {
            cachedAnimator.ResetTrigger(hash);
            cachedAnimator.SetTrigger(hash);
        }
    }

    /// <summary>
    /// Définit l'expression faciale en exploitant les paramètres dédiés.
    /// </summary>
    public void SetFaceState(FaceAnimationState state, float transitionDuration = 0.1f, float normalizedStartTime = 0f, bool forceInstantTransition = false, int layerIndex = 1)
    {
        if (cachedAnimator == null)
            return;

        bool parameterUsed = false;

        if (faceTransitionParameterAvailable)
        {
            cachedAnimator.SetFloat(faceTransitionDurationHash, Mathf.Max(0f, transitionDuration));
            parameterUsed = true;
        }

        if (forceInstantTransition && faceInstantParameterAvailable)
        {
            cachedAnimator.ResetTrigger(faceInstantHash);
            cachedAnimator.SetTrigger(faceInstantHash);
            parameterUsed = true;
        }

        if (faceStateParameterAvailable)
        {
            cachedAnimator.SetInteger(faceStateHash, (int)state);
            parameterUsed = true;
        }

        if (!parameterUsed)
        {
            PlayFallbackFaceClip(state, transitionDuration, normalizedStartTime, layerIndex);
        }
    }

    /// <summary>
    /// Force un Rebind complet de l'Animator afin de resynchroniser les paramètres
    /// et de purger tout état transitoire lorsque l'on réinitialise le personnage.
    /// </summary>
    public void ForceAnimatorRebind()
    {
        if (cachedAnimator == null)
            return;

        cachedAnimator.Update(0f);
        cachedAnimator.Rebind();
    }

    /// <summary>
    /// Permet de jouer un clip spécifique en dehors du pipeline paramétré. À n'utiliser
    /// que pour des besoins exceptionnels (tests, animations temporaires...).
    /// </summary>
    public void PlayFallbackClip(BodyAnimationState state, float transitionDuration = 0.1f, float normalizedStartTime = 0f, int layerIndex = 0)
    {
        if (cachedAnimator == null)
            return;

        if (bodyFallbackLookup.TryGetValue(state, out string clipName) && !string.IsNullOrEmpty(clipName))
        {
            cachedAnimator.CrossFade(clipName, Mathf.Max(0f, transitionDuration), layerIndex, Mathf.Clamp01(normalizedStartTime));
        }
    }

    /// <summary>
    /// Joue directement un clip nommé, utile pour des animations très spécifiques qui
    /// n'ont pas encore d'équivalent dans le graphe paramétré.
    /// </summary>
    public void PlayRawClip(string clipName, float transitionDuration = 0.1f, int layerIndex = 0, float normalizedStartTime = 0f)
    {
        if (cachedAnimator == null || string.IsNullOrEmpty(clipName))
            return;

        cachedAnimator.CrossFade(clipName, Mathf.Max(0f, transitionDuration), layerIndex, Mathf.Clamp01(normalizedStartTime));
    }

    private void PlayFallbackFaceClip(FaceAnimationState state, float transitionDuration, float normalizedStartTime, int layerIndex)
    {
        if (cachedAnimator == null)
            return;

        if (faceFallbackLookup.TryGetValue(state, out string clipName) && !string.IsNullOrEmpty(clipName))
        {
            cachedAnimator.CrossFade(clipName, Mathf.Max(0f, transitionDuration), layerIndex, Mathf.Clamp01(normalizedStartTime));
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Action disponible via le menu contextuel du composant pour configurer automatiquement
    /// l'Animator côté éditeur. Cette méthode ajoute tous les paramètres attendus si besoin,
    /// puis regénère les caches afin que le gameplay puisse immédiatement exploiter les valeurs.
    /// </summary>
    [ContextMenu("Animator/Configurer automatiquement les paramètres")]
    private void ConfigureAnimatorParametersInEditor()
    {
        // Nous veillons à disposer d'une référence valide vers l'Animator avant d'aller plus loin.
        if (cachedAnimator == null)
            cachedAnimator = GetComponent<Animator>();

        if (cachedAnimator == null)
        {
            Debug.LogWarning("CharacterAnimationController : aucun Animator trouvé pour configurer les paramètres.", this);
            return;
        }

        var animatorController = ResolveAnimatorControllerAsset();
        if (animatorController == null)
        {
            Debug.LogWarning("CharacterAnimationController : impossible d'accéder à l'AnimatorController pour ajouter les paramètres.", this);
            return;
        }

        // Création sécurisée de chaque paramètre attendu par le pipeline.
        EnsureAnimatorParameter(animatorController, bodyStateParameter, UnityEngine.AnimatorControllerParameterType.Int);
        EnsureAnimatorParameter(animatorController, bodyTransitionDurationParameter, UnityEngine.AnimatorControllerParameterType.Float);
        EnsureAnimatorParameter(animatorController, bodyNormalizedTimeParameter, UnityEngine.AnimatorControllerParameterType.Float);
        EnsureAnimatorParameter(animatorController, bodyInstantTransitionParameter, UnityEngine.AnimatorControllerParameterType.Trigger);
        EnsureAnimatorParameter(animatorController, bodySpeedParameter, UnityEngine.AnimatorControllerParameterType.Float);
        EnsureAnimatorParameter(animatorController, faceStateParameter, UnityEngine.AnimatorControllerParameterType.Int);
        EnsureAnimatorParameter(animatorController, faceTransitionDurationParameter, UnityEngine.AnimatorControllerParameterType.Float);
        EnsureAnimatorParameter(animatorController, faceInstantTransitionParameter, UnityEngine.AnimatorControllerParameterType.Trigger);

        if (bodyTriggers != null)
        {
            foreach (var trigger in bodyTriggers)
            {
                if (trigger.trigger == BodyAnimationTrigger.None || string.IsNullOrEmpty(trigger.parameterName))
                    continue;

                EnsureAnimatorParameter(animatorController, trigger.parameterName, UnityEngine.AnimatorControllerParameterType.Trigger);
            }
        }

        // À la fin du processus, on regénère immédiatement les caches pour que l'aperçu dans
        // l'inspecteur soit cohérent avec les paramètres fraîchement ajoutés.
        CacheParameterAvailability();

        Debug.Log("CharacterAnimationController : configuration des paramètres terminée avec succès.", this);
    }

    /// <summary>
    /// Récupère l'AnimatorController éditable associé au composant (même si un AnimatorOverride est utilisé).
    /// </summary>
    private UnityEditor.Animations.AnimatorController ResolveAnimatorControllerAsset()
    {
        if (cachedAnimator == null)
            return null;

        var runtimeController = cachedAnimator.runtimeAnimatorController;
        if (runtimeController == null)
            return null;

        // On gère également le cas où l'on emploie un AnimatorOverrideController pour personnaliser des clips.
        if (runtimeController is UnityEditor.Animations.AnimatorController controller)
            return controller;

        if (runtimeController is AnimatorOverrideController overrideController)
            return overrideController.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;

        return null;
    }

    /// <summary>
    /// Ajoute un paramètre à l'Animator s'il est absent ou si son type est incorrect.
    /// </summary>
    private void EnsureAnimatorParameter(UnityEditor.Animations.AnimatorController controller, string parameterName, UnityEngine.AnimatorControllerParameterType type)
    {
        if (controller == null || string.IsNullOrEmpty(parameterName))
            return;

        foreach (var parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                if (parameter.type != type)
                {
                    // Si le paramètre existe mais d'un autre type, on le remplace pour éviter un comportement incohérent.
                    controller.RemoveParameter(parameter);
                    controller.AddParameter(parameterName, type);
                }

                return;
            }
        }

        controller.AddParameter(parameterName, type);
    }
#endif
}
