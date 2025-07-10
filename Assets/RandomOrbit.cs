using UnityEngine;

public class RandomOrbit : MonoBehaviour
{
    public Transform pivot; // Le point autour duquel on tourne
    public float minSpeed = 10f;
    public float maxSpeed = 50f;
    public float minDistance = 2f;
    public float maxDistance = 5f;

    private float speed;
    private Vector3 orbitOffset;
    private Vector3 rotationAxis;

    void Start()
    {
        if (pivot == null)
        {
            Debug.LogError("Pivot non assigné !");
            enabled = false;
            return;
        }

        // Vitesse de rotation aléatoire
        speed = Random.Range(minSpeed, maxSpeed);

        // Rayon/distance aléatoire
        float distance = Random.Range(minDistance, maxDistance);

        // Position initiale décalée à une certaine distance autour du pivot
        Vector3 randomDirection = Random.onUnitSphere;
        orbitOffset = randomDirection * distance;
        transform.position = pivot.position + orbitOffset;

        // Axe de rotation aléatoire
        rotationAxis = Random.onUnitSphere;
    }

    void Update()
    {
        // Rotation autour du pivot selon un axe arbitraire
        transform.RotateAround(pivot.position, rotationAxis, speed * Time.deltaTime);
    }
}
