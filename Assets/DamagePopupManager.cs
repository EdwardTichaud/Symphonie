using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private DamagePopup damagePopupPrefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ShowDamage(Vector3 position, int amount)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] Prefab manquant.");
            return;
        }

        DamagePopup popup = Instantiate(damagePopupPrefab, position, Quaternion.identity, transform);
        popup.Initialize(amount);
    }
}
