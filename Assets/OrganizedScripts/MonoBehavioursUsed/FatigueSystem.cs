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
    public bool IsAsleep => isAsleep;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
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
            routine = StartCoroutine(SleepRoutine());
        }
    }

    private IEnumerator SleepRoutine()
    {
        isAsleep = true;
        yield return new WaitForSeconds(asleepDuration);
        unit.currentFatigue = unit.Data.baseFatigue;
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentFatigue);
        isAsleep = false;
        routine = null;
    }
}
