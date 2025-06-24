using UnityEngine;

public class BattlefieldManager : MonoBehaviour
{
    public static BattlefieldManager Instance { get; private set; }

    [Header("Zone active (copiée depuis ZoneManager)")]
    public ZoneSO currentZone;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Appelée par ZoneManager quand la zone change.
    /// </summary>
    public void SetCurrentZone(ZoneSO zone)
    {
        if (zone == null)
        {
            Debug.LogWarning("[BattlefieldManager] ZoneSO null !");
            return;
        }

        currentZone = zone;

        ActivateFirstBattlefieldInZone();
    }

    /// <summary>
    /// Active le premier battlefield de la zone active, désactive les autres.
    /// </summary>
    private void ActivateFirstBattlefieldInZone()
    {
        if (currentZone == null || currentZone.battlefields.Count == 0)
        {
            Debug.LogWarning("[BattlefieldManager] Pas de battlefield pour cette zone !");
            return;
        }

        for (int i = 0; i < currentZone.battlefields.Count; i++)
        {
            if (currentZone.battlefields[i] != null)
                currentZone.battlefields[i].SetActive(i == 0);
        }

        Debug.Log($"[BattlefieldManager] Premier battlefield activé pour : {currentZone.zoneName}");
    }

    /// <summary>
    /// Active un battlefield spécifique dans la zone active.
    /// </summary>
    public void SetBattlefield(int index)
    {
        if (currentZone == null || currentZone.battlefields.Count == 0)
        {
            Debug.LogWarning("[BattlefieldManager] Zone invalide ou vide !");
            return;
        }

        if (index < 0 || index >= currentZone.battlefields.Count)
        {
            Debug.LogWarning($"[BattlefieldManager] Index {index} invalide pour {currentZone.zoneName}");
            return;
        }

        for (int i = 0; i < currentZone.battlefields.Count; i++)
        {
            if (currentZone.battlefields[i] != null)
                currentZone.battlefields[i].SetActive(i == index);
        }

        Debug.Log($"[BattlefieldManager] Battlefield #{index} activé pour {currentZone.zoneName}");
    }
}
