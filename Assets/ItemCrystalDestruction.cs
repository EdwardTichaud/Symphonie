using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ConditionalSequenceManager : MonoBehaviour
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

    [Header("Condition globale (facultative)")]
    public bool useBattleStateCondition = true;
    public BattleState waitState = BattleState.SquadUnit_ItemsMenu; // on attend tant que currentBattleState == waitState
    private bool sequenceStarted = false;

    void Update()
    {
        if (!useBattleStateCondition || sequenceStarted)
            return;

        if (NewBattleManager.Instance.currentBattleState != waitState)
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
}
