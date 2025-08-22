using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MoveToExecutor : MonoBehaviour
{
    private Coroutine running;

    public void ExecuteMove(MoveToConfigSO config)
    {
        if (config == null)
        {
            Debug.LogWarning("[MoveToExecutor] Config SO manquant.");
            return;
        }

        Transform subject = ResolveSubject(config);
        if (subject == null)
        {
            Debug.LogWarning("[MoveToExecutor] Aucun sujet valide.");
            return;
        }

        if (!TryResolveDestination(config, out Vector3 dstWorld))
        {
            Debug.LogWarning("[MoveToExecutor] Impossible de résoudre la destination.");
            return;
        }

        StartMove(subject, dstWorld, config.duration, config.ease, config.unscaledTime, config.useLocalSpace, config.interruptCurrentMove);
    }

    // ---------- Helpers ----------

    private Transform ResolveSubject(MoveToConfigSO cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.subjectToMoveName))
            return transform;

        // recherche relative (depuis la racine)
        var root = transform.root;
        var byPath = root != null ? root.Find(cfg.subjectToMoveName) : null;
        if (byPath != null) return byPath;

        // fallback global
        var go = GameObject.Find(cfg.subjectToMoveName);
        return go != null ? go.transform : null;
    }

    private bool TryResolveDestination(MoveToConfigSO cfg, out Vector3 dstWorld)
    {
        if (cfg.destinationMode == DestinationMode.WorldPosition)
        {
            dstWorld = cfg.worldPosition + cfg.worldOffset;
            return true;
        }

        if (cfg.destinationMode == DestinationMode.TargetByName && !string.IsNullOrWhiteSpace(cfg.targetName))
        {
            var target = GameObject.Find(cfg.targetName);
            if (target != null)
            {
                dstWorld = target.transform.position + cfg.worldOffset;
                return true;
            }
        }

        dstWorld = default;
        return false;
    }

    // ---------- Mouvement ----------

    private void StartMove(Transform subject, Vector3 dstWorld, float duration, AnimationCurve ease, bool unscaled, bool useLocal, bool interrupt)
    {
        if (running != null)
        {
            if (interrupt) StopCoroutine(running);
            else return;
        }
        running = StartCoroutine(MoveRoutine(subject, dstWorld, Mathf.Max(0f, duration), ease, unscaled, useLocal));
    }

    private IEnumerator MoveRoutine(Transform subject, Vector3 dstWorld, float duration, AnimationCurve ease, bool unscaled, bool useLocal)
    {
        Vector3 srcWorld = subject.position;
        Vector3 srcLocal = subject.localPosition;
        Vector3 dstLocal = srcLocal;

        bool doLocal = useLocal;
        if (doLocal)
        {
            var parent = subject.parent;
            if (parent != null) dstLocal = parent.InverseTransformPoint(dstWorld);
            else doLocal = false;
        }

        if (duration <= 0f)
        {
            if (doLocal) subject.localPosition = dstLocal;
            else subject.position = dstWorld;
            running = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            if (ease != null) u = ease.Evaluate(u);

            if (doLocal) subject.localPosition = Vector3.LerpUnclamped(srcLocal, dstLocal, u);
            else subject.position = Vector3.LerpUnclamped(srcWorld, dstWorld, u);

            yield return null;
        }

        if (doLocal) subject.localPosition = dstLocal;
        else subject.position = dstWorld;

        running = null;
    }
}
