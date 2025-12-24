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
    [SerializeField] private Camera battleCamera;
    [SerializeField] private int prewarmCount = 10;

    [Header("Couleurs")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color buffColor = new Color(0.4f, 0.75f, 1f, 1f);
    [SerializeField] private Color debuffColor = new Color(1f, 0.6f, 0.2f, 1f);
    [SerializeField] private Color statusColor = new Color(1f, 0.85f, 0.45f, 1f);

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

        ResolveBattleCamera();
        PrewarmPool();
    }

    private void ResolveBattleCamera()
    {
        if (battleCamera != null)
            return;

        GameObject battleCameraGO = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCameraGO != null)
            battleCamera = battleCameraGO.GetComponent<Camera>();

        if (battleCamera == null)
            battleCamera = Camera.main;
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
    /// Crée un popup de dégâts au-dessus de la cible.
    /// </summary>
    /// <param name="target">Transform à suivre.</param>
    /// <param name="amount">Montant de dégâts.</param>
    public void ShowDamage(Transform target, int amount)
    {
        if (amount <= 0)
            return;

        ShowPopup(target, amount.ToString(), damageColor);
    }

    public void ShowHeal(Transform target, int amount)
    {
        int rounded = Mathf.RoundToInt(Mathf.Abs(amount));
        if (rounded <= 0)
            return;

        ShowPopup(target, "+" + rounded, healColor);
    }

    public void ShowBuff(Transform target, BuffStatType stat, int amount, bool isPercentage)
    {
        if (amount == 0 || stat == BuffStatType.None)
            return;

        string value = Mathf.Abs(amount).ToString();
        if (isPercentage)
            value += "%";

        string text = $"+{value} {FormatStatName(stat)}";
        ShowPopup(target, text, buffColor);
    }

    public void ShowDebuff(Transform target, DebuffStatType stat, int amount, bool isPercentage)
    {
        if (amount == 0 || stat == DebuffStatType.None)
            return;

        string value = Mathf.Abs(amount).ToString();
        if (isPercentage)
            value += "%";

        string text = $"-{value} {FormatStatName((BuffStatType)stat)}";
        ShowPopup(target, text, debuffColor);
    }

    public void ShowStatus(Transform target, string label, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        ShowPopup(target, label, isPositive ? buffColor : statusColor);
    }

    private void ShowPopup(Transform target, string text, Color color)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] Prefab manquant.");
            return;
        }

        if (target == null)
            return;

        ResolveBattleCamera();

        DamagePopup popupScript = GetPopup();
        if (popupScript == null)
            return;

        popupScript.transform.SetParent(transform, true);
        popupScript.gameObject.SetActive(true);
        popupScript.SetOwner(this);
        popupScript.Initialize(text, target, battleCamera, color);
    }

    private static string FormatStatName(BuffStatType stat)
    {
        return stat switch
        {
            BuffStatType.Strength => "Force",
            BuffStatType.Defense => "Defense",
            BuffStatType.Initiative => "Initiative",
            BuffStatType.MaxHP => "PV max",
            _ => "Stat"
        };
    }
}
