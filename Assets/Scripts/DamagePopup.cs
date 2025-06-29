using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float duration = 1.5f;
    public Vector3 offset = new Vector3(0, 2f, 0);

    [Header("Références")]
    public TextMeshProUGUI textMesh;

    private float elapsed = 0f;
    private Camera mainCam;
    private CanvasGroup canvasGroup;

    private static DamagePopup popupPrefab;

    public static void Show(Vector3 position, int amount)
    {
        if (popupPrefab == null)
            popupPrefab = Resources.Load<DamagePopup>("DamagePopup");

        if (popupPrefab == null)
        {
            Debug.LogError("DamagePopup prefab introuvable dans un dossier Resources.");
            return;
        }

        DamagePopup instance = Instantiate(popupPrefab, position, Quaternion.identity);
        instance.Initialize(amount);
    }

    public void Initialize(int amount)
    {
        textMesh.text = amount.ToString();
        mainCam = Camera.main;
        canvasGroup = GetComponent<CanvasGroup>();

        // Position de départ + offset vertical
        transform.position += offset;
    }

    void Update()
    {
        if (mainCam != null)
        {
            // Toujours face à la caméra
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
        }

        // Animation vers le haut
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Fade out après 'duration'
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            if (canvasGroup.alpha <= 0f)
                Destroy(gameObject);
        }
    }
}
