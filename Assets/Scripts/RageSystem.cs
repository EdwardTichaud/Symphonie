using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class RageSystem : MonoBehaviour
{
    private CharacterUnit unit;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
    }

    public void AddRage(int damage)
    {
        if (unit == null || unit.Data == null) return;
        unit.currentRage = Mathf.Clamp(unit.currentRage + damage, unit.Data.baseRage, unit.Data.maxRage);
        if (unit.rageBar != null)
            unit.rageBar.SetValue(unit.currentRage);
    }

    public int CalculateBonusDamage()
    {
        if (unit == null || unit.Data == null) return 0;
        return Mathf.RoundToInt(unit.currentRage * unit.Data.rageDamageMultiplier);
    }
}
