using UnityEngine;

[ExecuteAlways]
public class OrbitAround : MonoBehaviour
{
    [Header("Cible et paramètres")]
    public Transform target;
    public float distance = 5f;
    public float speed = 30f;      // degrés par seconde

    [Header("Axes d'orbite")]
    public bool orbitX = false;          // tourne autour de l'axe X (plan YZ)
    public bool orbitY = true;           // tourne autour de l'axe Y (plan XZ)
    public bool orbitZ = false;          // tourne autour de l'axe Z (plan XY)

    void Start()
    {
        if (target != null)
        {
            // position initiale à la bonne distance
            Vector3 dir = (transform.position - target.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            transform.position = target.position + dir * distance;
        }
    }

    void Update()
    {
        if (target == null) return;

        // construit l'axe d'orbite
        Vector3 axis = new Vector3(
            orbitX ? 1f : 0f,
            orbitY ? 1f : 0f,
            orbitZ ? 1f : 0f
        );

        if (axis == Vector3.zero) return;
        axis.Normalize();

        // effectue la rotation
        float angle = speed * Time.deltaTime;
        transform.RotateAround(target.position, axis, angle);

        // recale la distance exacte (évite la petite dérive par incréments)
        Vector3 offset = transform.position - target.position;
        transform.position = target.position + offset.normalized * distance;
    }
}
