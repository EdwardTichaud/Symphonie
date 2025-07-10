using UnityEngine;

[ExecuteAlways]
public class OrbitWithScale : MonoBehaviour
{
    [Header("Pivot & Distance")]
    public Transform pivot;
    public float minDistance = 2f;
    public float maxDistance = 6f;

    [Header("Rotation")]
    public float minSpeed = 10f;
    public float maxSpeed = 40f;

    [Header("Scale")]
    public float scaleFactor = 0.3f; // Coefficient multiplicateur (taille = distance * scaleFactor)

    private float speed;
    private Vector3 rotationAxis;

    void Start()
    {
        if (pivot == null)
        {
            Debug.LogError("Pivot non assigné !");
            enabled = false;
            return;
        }

        // Définir la distance d'orbite aléatoire
        float distance = Random.Range(minDistance, maxDistance);

        // Définir la position autour du pivot
        Vector3 direction = Random.onUnitSphere;
        transform.position = pivot.position + direction * distance;

        // Calcul de la rotation aléatoire
        rotationAxis = Random.onUnitSphere;
        speed = Random.Range(minSpeed, maxSpeed);

        // Appliquer la taille en fonction de la distance
        float size = distance * scaleFactor;
        transform.localScale = new Vector3(size, size, size);
    }

    void Update()
    {
        transform.RotateAround(pivot.position, rotationAxis, speed * Time.deltaTime);
    }
}
