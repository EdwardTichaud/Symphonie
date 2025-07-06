using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraBreathing : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.05f;
    [SerializeField] private float frequency = 1f;

    private Vector3 baseLocalPosition;
    private Vector3 lastWorldPosition;

    void Start()
    {
        baseLocalPosition = transform.localPosition;
        lastWorldPosition = transform.position;
    }

    void Update()
    {
        // Détermine si la caméra s'est déplacée depuis la frame précédente
        bool moved = (transform.position - lastWorldPosition).sqrMagnitude > 1e-6f;
        lastWorldPosition = transform.position;

        if (moved)
        {
            // Réinitialise la position de base si la caméra a bougé
            baseLocalPosition = transform.localPosition;
            transform.localPosition = baseLocalPosition;
        }
        else
        {
            float offset = Mathf.Sin(Time.time * frequency) * amplitude;
            transform.localPosition = baseLocalPosition + Vector3.up * offset;
        }
    }
}
