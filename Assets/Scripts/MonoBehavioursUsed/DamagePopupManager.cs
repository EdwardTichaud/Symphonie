using System.Collections.Generic;
using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    private static DamagePopupManager _instance;

    /// <summary>
    /// Accès global au gestionnaire. Cherche automatiquement une instance
    /// existante dans la scène si aucune n'a encore été assignée.
    /// </summary>
    public static DamagePopupManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DamagePopupManager>(); // Migration vers la nouvelle API de recherche d'objets
            return _instance;
        }
    }

    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private int prewarmCount = 10;

    private readonly Queue<DamagePopup> popupPool = new();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            // Évite les doublons au démarrage
            Destroy(gameObject);
            return;
        }

        // S'assure que l'objet ne possède pas d'échelle étrange héritée d'un prefab
        transform.localScale = Vector3.one;

        PrewarmPool();
    }

    private void PrewarmPool()
    {
        if (damagePopupPrefab == null || prewarmCount <= 0)
            return;

        for (int i = popupPool.Count; i < prewarmCount; i++)
        {
            DamagePopup popup = CreatePopupInstance();
            if (popup == null)
                break;

            popup.gameObject.SetActive(false);
            popupPool.Enqueue(popup);
        }
    }

    private DamagePopup CreatePopupInstance()
    {
        if (damagePopupPrefab == null)
            return null;

        GameObject popupObject = Instantiate(damagePopupPrefab, transform, true);
        DamagePopup popupScript = popupObject.GetComponent<DamagePopup>();
        if (popupScript == null)
        {
            Debug.LogError("[DamagePopupManager] Le prefab ne contient pas de composant DamagePopup.");
            Destroy(popupObject);
            return null;
        }

        popupScript.SetOwner(this);
        popupObject.SetActive(false);
        return popupScript;
    }

    private DamagePopup GetPopup()
    {
        if (popupPool.Count > 0)
            return popupPool.Dequeue();

        return CreatePopupInstance();
    }

    public void ReleasePopup(DamagePopup popup)
    {
        if (popup == null)
            return;

        popup.gameObject.SetActive(false);
        popup.transform.SetParent(transform, true);
        popupPool.Enqueue(popup);
    }

    /// <summary>
    /// Crée un popup de dégâts centré sur l'écran.
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

        DamagePopup popupScript = GetPopup();
        if (popupScript == null)
            return;

        popupScript.transform.SetParent(transform, true);
        popupScript.gameObject.SetActive(true);
        popupScript.SetOwner(this);

        // Initialisation avec la cible initiale (paramètre conservé pour compatibilité)
        // et le montant de dégâts à afficher. Le DamagePopup se chargera ensuite de se
        // positionner en overlay au centre de l'écran.
        popupScript.Initialize(amount, target);
    }
}
