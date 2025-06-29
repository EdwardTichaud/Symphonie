using UnityEngine;
using TMPro;

public class DamagePopupBehaviour : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;
    public float duration = 1.5f;
    public Vector3 offset = new Vector3(0, 2f, 0);

    [Header("R�f�rences")]
    public TextMeshProUGUI textMesh;

    private float elapsed = 0f;
    private Camera mainCam;
    private CanvasGroup canvasGroup;

    public void Initialize(int amount)
    {
        textMesh.text = amount.ToString();
        mainCam = Camera.main;
        canvasGroup = GetComponent<CanvasGroup>();

        // Position de d�part + offset vertical
        transform.position += offset;
    }

    void Update()
    {
        if (mainCam != null)
        {
            // Toujours face � la cam�ra
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
        }

        // Animation vers le haut
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Fade out apr�s 'duration'
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            if (canvasGroup.alpha <= 0f)
                Destroy(gameObject);
        }
    }
}
