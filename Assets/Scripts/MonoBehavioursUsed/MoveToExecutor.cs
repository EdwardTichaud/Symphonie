using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MoveToExecutor : MonoBehaviour
{
    private Coroutine runningMove;
    private Coroutine runningRotate;

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

        // ----- Déplacement -----
        if (TryResolveDestination(config, out Vector3 dstWorld))
        {
            StartMove(
                subject,
                dstWorld,
                Mathf.Max(0f, config.duration),
                config.ease,
                config.unscaledTime,
                config.useLocalSpace,
                config.interruptCurrentMove
            );
        }
        else
        {
            Debug.LogWarning("[MoveToExecutor] Impossible de résoudre la destination.");
        }

        // ----- Rotation (optionnelle) -----
        if (config.rotationTargetMode != RotationTargetMode.None && config.rotationDuration >= 0f)
        {
            if (TryResolveLookTarget(config, out Vector3 lookWorld))
            {
                StartRotate(
                    subject,
                    lookWorld,
                    Mathf.Max(0f, config.rotationDuration),
                    config.rotationEase,
                    config.rotationUnscaledTime,
                    config.interruptCurrentRotation,
                    config.rotateAxes,
                    config.rotationEulerOffset,
                    (config.customUp == Vector3.zero ? Vector3.up : config.customUp.normalized)
                );
            }
            else
            {
                Debug.LogWarning("[MoveToExecutor] Look target introuvable.");
            }
        }
    }

    // ---------- Helpers ----------

    private Transform ResolveSubject(MoveToConfigSO cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.subjectToMoveName))
            return transform;

        // recherche relative (depuis la racine du sujet courant)
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

    private bool TryResolveLookTarget(MoveToConfigSO cfg, out Vector3 lookWorld)
    {
        switch (cfg.rotationTargetMode)
        {
            case RotationTargetMode.LookAtWorldPosition:
                lookWorld = cfg.worldPosition; // volontairement sans worldOffset
                return true;

            case RotationTargetMode.LookAtTargetByName:
                if (!string.IsNullOrWhiteSpace(cfg.targetName))
                {
                    var t = GameObject.Find(cfg.targetName);
                    if (t != null)
                    {
                        lookWorld = t.transform.position;
                        return true;
                    }
                }
                break;
        }

        lookWorld = default;
        return false;
    }

    // ---------- Mouvement ----------

    private void StartMove(Transform subject, Vector3 dstWorld, float duration, AnimationCurve ease, bool unscaled, bool useLocal, bool interrupt)
    {
        if (runningMove != null)
        {
            if (interrupt) StopCoroutine(runningMove);
            else return;
        }
        runningMove = StartCoroutine(MoveRoutine(subject, dstWorld, duration, ease, unscaled, useLocal));
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
            runningMove = null;
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

        runningMove = null;
    }

    // ---------- Rotation ----------

    private void StartRotate(
        Transform subject,
        Vector3 lookWorld,
        float duration,
        AnimationCurve ease,
        bool unscaled,
        bool interrupt,
        AxisMask axes,
        Vector3 eulerOffset,
        Vector3 up
    )
    {
        if (runningRotate != null)
        {
            if (interrupt) StopCoroutine(runningRotate);
            else return;
        }

        runningRotate = StartCoroutine(RotateRoutine(subject, lookWorld, duration, ease, unscaled, axes, eulerOffset, up));
    }

    private IEnumerator RotateRoutine(
        Transform subject,
        Vector3 lookWorld,
        float duration,
        AnimationCurve ease,
        bool unscaled,
        AxisMask axes,
        Vector3 eulerOffset,
        Vector3 up
    )
    {
        // Si la cible est très proche, garde la direction actuelle
        Vector3 dir = (lookWorld - subject.position);
        if (dir.sqrMagnitude < 1e-8f) dir = subject.forward;

        Quaternion targetRot = Quaternion.LookRotation(dir, up) * Quaternion.Euler(eulerOffset);

        // Eulers de départ/arrivée + masquage par axe
        Vector3 startEuler = subject.rotation.eulerAngles;
        Vector3 targetEuler = targetRot.eulerAngles;

        if (!axes.X) targetEuler.x = startEuler.x;
        if (!axes.Y) targetEuler.y = startEuler.y;
        if (!axes.Z) targetEuler.z = startEuler.z;

        if (duration <= 0f)
        {
            subject.rotation = Quaternion.Euler(targetEuler);
            runningRotate = null;
            yield break;
        }

        float t = 0f;
        var curve = ease != null ? ease : AnimationCurve.Linear(0, 0, 1, 1);

        while (t < duration)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            u = curve.Evaluate(u);

            float x = Mathf.LerpAngle(startEuler.x, targetEuler.x, u);
            float y = Mathf.LerpAngle(startEuler.y, targetEuler.y, u);
            float z = Mathf.LerpAngle(startEuler.z, targetEuler.z, u);

            subject.rotation = Quaternion.Euler(x, y, z);
            yield return null;
        }

        subject.rotation = Quaternion.Euler(targetEuler);
        runningRotate = null;
    }
}
