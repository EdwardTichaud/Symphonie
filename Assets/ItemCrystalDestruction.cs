using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemCrystalDestruction : MonoBehaviour
{
    [System.Serializable]
    public class ManagedObject
    {
        [Header("Référence")]
        public GameObject target;

        [Header("Ordre")]
        public int priority = 0;                 // plus petit = plus tôt

        [Header("Particles")]
        public bool triggerParticles = false;    // déclencher les PS ?
        public float particleDelay = 0f;         // délai avant de jouer les PS

        [Header("Destruction")]
        public bool destroy = false;             // détruire l’objet ?
        public float destroyDelay = 1f;          // délai avant destruction (après particles)

        [Header("Mise à l’échelle (appel manuel)")]
        public bool scaleOnCommand = false;      // inclure cet objet dans ScaleAll()
        public Vector3 targetScale = Vector3.one;
        public float scaleDuration = 0.5f;
        public AnimationCurve scaleEase = AnimationCurve.Linear(0, 0, 1, 1);
        public bool scaleRelative = false;       // si true: targetScale est un multiplicateur
    }

    [Header("Liste d’objets gérés")]
    public List<ManagedObject> entries = new List<ManagedObject>();

    [Header("Audio")]
    [Tooltip("Son joué automatiquement lors de l'apparition du GameObject portant ce script.")]
    [SerializeField] private AudioClipSO spawnSound;

    [Tooltip("Son joué automatiquement lorsque le GameObject portant ce script est détruit.")]
    [SerializeField] private AudioClipSO destructionSound;

    [Tooltip("Source audio locale utilisée si aucun AudioManager n'est disponible.")]
    [SerializeField] private AudioSource localAudioSource;

    [Header("Condition globale (facultative)")]
    public bool useBattleStateCondition = true;

    // On attend tant que currentBattleState ∈ waitStates.
    // Dès qu'il n'est plus dans cette liste, on lance la séquence.
    public List<BattleState> waitStates = new List<BattleState>
    {
        BattleState.SquadUnit_ItemsMenu
    };

    private bool sequenceStarted = false;
    private static bool applicationIsQuitting = false;

    private void Awake()
    {
        // Mise en cache immédiate d'une AudioSource locale (si elle existe) pour éviter des recherches répétées.
        CacheLocalAudioSource();
    }

    private void OnEnable()
    {
        // Ne rien faire en mode Éditeur pour éviter des lectures indésirables lorsqu'on sélectionne l'objet.
        if (!Application.isPlaying)
            return;

        // Lecture du son d'instanciation dès que l'objet devient actif.
        PlayManagedClip(spawnSound, "instanciation", forceWorldPlayback: false);
    }

    private void OnDestroy()
    {
        // Si l'application est en train de se fermer ou si l'on n'est pas en mode Play, on n'essaie pas de jouer de son.
        if (applicationIsQuitting || !Application.isPlaying)
            return;

        // On privilégie une lecture spatialisée (forceWorldPlayback) pour garantir que le son survive à la destruction.
        PlayManagedClip(destructionSound, "destruction", forceWorldPlayback: true);
    }

    private void OnApplicationQuit()
    {
        // Flag global pour éviter de tenter de jouer des sons alors que le mixeur global est déjà détruit.
        applicationIsQuitting = true;
    }

    void Update()
    {
        if (!useBattleStateCondition || sequenceStarted)
            return;

        // Si la liste est vide, on considère qu'on ne "bloque" sur aucun état et on lance immédiatement.
        var current = NewBattleManager.Instance.currentBattleState;
        bool isWaiting = (waitStates != null && waitStates.Count > 0) && waitStates.Contains(current);

        if (!isWaiting)
        {
            sequenceStarted = true;
            // Lance la séquence complète : Particles -> Destructions selon les réglages par entrée
            StartCoroutine(RunSequenceInOrder());
        }
    }

    /// <summary>
    /// Lance uniquement les ParticleSystems pour toutes les entrées (dans l'ordre).
    /// </summary>
    public void TriggerParticlesInOrder()
    {
        StartCoroutine(ParticlesInOrder());
    }

    /// <summary>
    /// Lance uniquement les destructions (dans l'ordre).
    /// </summary>
    public void DestroyInOrder()
    {
        StartCoroutine(DestroyOnlyInOrder());
    }

    /// <summary>
    /// Met à l’échelle tous les objets marqués "scaleOnCommand" avec leurs paramètres locaux.
    /// </summary>
    public void ScaleAll()
    {
        foreach (var e in entries)
        {
            if (e.target == null || !e.scaleOnCommand) continue;
            StartCoroutine(ScaleRoutine(e));
        }
    }

    /// <summary>
    /// Variante : applique la même cible/durée/courbe à tous les objets marqués "scaleOnCommand".
    /// </summary>
    public void ScaleAllUnified(Vector3 targetScale, float duration, AnimationCurve ease, bool relative = false)
    {
        foreach (var e in entries)
        {
            if (e.target == null || !e.scaleOnCommand) continue;

            // on clone temporairement les paramètres de scale
            var temp = new ManagedObject
            {
                target = e.target,
                targetScale = targetScale,
                scaleDuration = duration,
                scaleEase = ease != null ? ease : AnimationCurve.Linear(0, 0, 1, 1),
                scaleRelative = relative
            };
            StartCoroutine(ScaleRoutine(temp));
        }
    }

    // --------- Séquences ---------

    private IEnumerator RunSequenceInOrder()
    {
        // Tri par priorité croissante
        entries.Sort((a, b) => a.priority.CompareTo(b.priority));

        foreach (var e in entries)
        {
            if (e.target == null) continue;

            // 1) Particles (optionnel)
            if (e.triggerParticles)
            {
                if (e.particleDelay > 0f)
                    yield return new WaitForSeconds(e.particleDelay);

                PlayAllParticles(e.target);
            }

            // 2) Destruction (optionnelle)
            if (e.destroy)
            {
                if (e.destroyDelay > 0f)
                    yield return new WaitForSeconds(e.destroyDelay);

                Destroy(e.target);
            }
        }
    }

    private IEnumerator ParticlesInOrder()
    {
        entries.Sort((a, b) => a.priority.CompareTo(b.priority));

        foreach (var e in entries)
        {
            if (e.target == null || !e.triggerParticles) continue;

            if (e.particleDelay > 0f)
                yield return new WaitForSeconds(e.particleDelay);

            PlayAllParticles(e.target);
        }
        yield break;
    }

    private IEnumerator DestroyOnlyInOrder()
    {
        entries.Sort((a, b) => a.priority.CompareTo(b.priority));

        foreach (var e in entries)
        {
            if (e.target == null || !e.destroy) continue;

            if (e.destroyDelay > 0f)
                yield return new WaitForSeconds(e.destroyDelay);

            Destroy(e.target);
        }
        yield break;
    }

    // --------- Helpers ---------

    private void PlayAllParticles(GameObject go)
    {
        // Inclut les enfants (même inactifs si besoin)
        ParticleSystem[] particles = go.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var ps in particles)
        {
            // Si le GameObject du PS est inactif, le Play ne fera rien -> on peut l’activer si nécessaire
            if (!ps.gameObject.activeInHierarchy)
                ps.gameObject.SetActive(true);

            ps.Play(true);
        }
    }

    private IEnumerator ScaleRoutine(ManagedObject e)
    {
        if (e.target == null) yield break;

        Transform t = e.target.transform;
        Vector3 start = t.localScale;
        Vector3 end = e.scaleRelative ? Vector3.Scale(start, e.targetScale) : e.targetScale;

        float d = Mathf.Max(0f, e.scaleDuration);
        if (d <= 0f)
        {
            t.localScale = end;
            yield break;
        }

        float tAccum = 0f;
        AnimationCurve curve = e.scaleEase != null ? e.scaleEase : AnimationCurve.Linear(0, 0, 1, 1);

        while (tAccum < d)
        {
            tAccum += Time.deltaTime;
            float u = Mathf.Clamp01(tAccum / d);
            float k = Mathf.Clamp01(curve.Evaluate(u));
            t.localScale = Vector3.LerpUnclamped(start, end, k);
            yield return null;
        }

        t.localScale = end;
    }

    /// <summary>
    /// Lecture sécurisée d'un <see cref="AudioClipSO"/> en privilégiant l'AudioManager.
    /// </summary>
    /// <param name="clipAsset">Le clip à jouer.</param>
    /// <param name="context">Contexte utilisé uniquement pour faciliter le débogage.</param>
    /// <param name="forceWorldPlayback">
    /// Lorsque vrai, on ignore la source locale pour créer un son dans le monde (utile lors de la destruction).
    /// </param>
    private void PlayManagedClip(AudioClipSO clipAsset, string context, bool forceWorldPlayback)
    {
        // Aucune donnée valide ? On sort immédiatement pour éviter les NullReference.
        if (clipAsset == null || clipAsset.Clip == null)
            return;

        // Passage prioritaire par l'AudioManager pour respecter le mixage et les volumes globaux.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(clipAsset);
            return;
        }

        // Si l'on peut utiliser une source locale (ex : scènes de test), on la privilégie pour éviter la création d'objets temporaires.
        if (!forceWorldPlayback && TryEnsureLocalAudioSource())
        {
            localAudioSource.PlayOneShot(clipAsset.Clip, clipAsset.Volume);
            return;
        }

        // Dernier recours : on crée un son ponctuel dans la scène pour garantir l'audibilité, même si l'objet est détruit.
        AudioSource.PlayClipAtPoint(clipAsset.Clip, transform.position, clipAsset.Volume);
    }

    /// <summary>
    /// Garantit la présence d'une AudioSource locale réutilisable.
    /// </summary>
    /// <returns>True si une AudioSource valide est disponible.</returns>
    private bool TryEnsureLocalAudioSource()
    {
        if (localAudioSource == null)
        {
            CacheLocalAudioSource();
        }

        return localAudioSource != null;
    }

    /// <summary>
    /// Mise en cache de l'AudioSource locale afin de mutualiser son usage.
    /// </summary>
    private void CacheLocalAudioSource()
    {
        if (localAudioSource == null)
        {
            localAudioSource = GetComponent<AudioSource>();
        }
    }
}
