using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("Zone courante")]
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
    /// Définit la nouvelle ZoneSO courante et synchronise le BattlefieldManager.
    /// </summary>
    public void SetCurrentZone(ZoneSO newZone)
    {
        if (newZone == null)
        {
            Debug.LogWarning("[ZoneManager] Nouvelle zone null !");
            return;
        }

        if (newZone == currentZone)
            return; // Pas besoin de changer

        currentZone = newZone;

        // Notifie le BattlefieldsManager
        BattlefieldManager.Instance.SetCurrentZone(newZone);

        Debug.Log($"[ZoneManager] Nouvelle zone courante : {newZone.zoneName}");

        ZoneNameDisplay.Instance.ShowCurrentZoneInfo();
    }
}
