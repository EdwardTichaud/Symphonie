using System.Collections;
using UnityEngine;

/// <summary>
/// Gère la fatigue d'une unité. Lorsque la fatigue atteint un seuil, l'unité s'endort temporairement.
/// </summary>
public class FatigueSystem : MonoBehaviour
{
    private CharacterUnit unit;
    public float asleepDuration = 2f;
    private bool isAsleep;
    private Coroutine routine;
    private bool wasDamagedWhileAsleep;
    public bool IsAsleep => isAsleep;
    public bool WasDamagedWhileAsleep => wasDamagedWhileAsleep;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
    }

    public void Sleep(float duration)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(SleepRoutine(duration));
    }

    public void WakeUp()
    {
        if (routine != null)
            StopCoroutine(routine);
        isAsleep = false;
        wasDamagedWhileAsleep = false;
        routine = null;
    }

    public void MarkDamagedWhileAsleep()
    {
        if (isAsleep)
            wasDamagedWhileAsleep = true;
    }

    public void OnActionPerformed(float amount = 1f)
    {
        if (unit == null || unit.Data == null) return;
        if (unit.Data.gameplayType != GameplayType.Fatigue) return;

        unit.currentFatigue = Mathf.Clamp(unit.currentFatigue + amount, unit.Data.baseFatigue, unit.Data.maxFatigue);
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentFatigue);
        if (unit.currentFatigue >= unit.Data.maxFatigue && routine == null)
        {
            routine = StartCoroutine(SleepRoutine(asleepDuration));
        }
    }

    private IEnumerator SleepRoutine(float duration)
    {
        isAsleep = true;
        wasDamagedWhileAsleep = false;
        yield return new WaitForSeconds(duration);
        unit.currentFatigue = unit.Data.baseFatigue;
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentFatigue);
        isAsleep = false;
        routine = null;
    }
}
