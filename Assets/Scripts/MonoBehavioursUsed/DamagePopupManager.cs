using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private GameObject damagePopupPrefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Crée un popup de dégâts au-dessus de la cible donnée.
    /// </summary>
    /// <param name="target">Transform à suivre.</param>
    /// <param name="amount">Montant de dégâts.</param>
    public void ShowDamage(Transform target, int amount)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] Prefab manquant.");
            return;
        }

        // Instancie le prefab comme enfant pour garder une hiérarchie propre
        GameObject popup = Instantiate(damagePopupPrefab, transform);
        popup.GetComponent<DamagePopup>().Initialize(amount, target);
    }
}
