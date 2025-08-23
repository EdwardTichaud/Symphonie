using UnityEngine;

[ExecuteAlways]
public class OrbitAround : MonoBehaviour
{
    [Header("Activation")]
    public bool isActive;

    [Header("Objet à déplacer (si vide = ce GameObject)")]
    public Transform objectToOrbit;

    [Header("Cible et paramètres")]
    public Transform target;
    public float distance = 5f;
    public float speed = 30f; // degrés/sec

    [Header("Hauteur absolue (monde)")]
    [Tooltip("Si activé, force la hauteur monde à target.y + heightOffset.")]
    public bool useHeight = false;
    public float heightOffset = 0f;

    [Header("Axes d'orbite")]
    public bool orbitX = false; // axe X -> plan YZ
    public bool orbitY = true;  // axe Y -> plan XZ (classique)
    public bool orbitZ = false; // axe Z -> plan XY

    void Start()
    {
        if (objectToOrbit == null) objectToOrbit = transform;
        if (target == null || objectToOrbit == null) return;

        Vector3 axis = GetAxis();
        if (axis == Vector3.zero) axis = Vector3.up;

        // Choix d’une direction initiale perpendiculaire à l’axe
        Vector3 dir = objectToOrbit.position - target.position;
        dir = ProjectOnPlane(dir, axis);
        if (dir.sqrMagnitude < 1e-6f)
        {
            // Si on est exactement sur la cible, on fabrique un vecteur perpendiculaire à l’axe
            dir = Vector3.Cross(axis, Vector3.forward);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.Cross(axis, Vector3.right);
        }

        dir = dir.normalized * Mathf.Max(0.0001f, distance);
        Vector3 pos = target.position + dir;

        // Hauteur absolue monde optionnelle
        if (useHeight) pos.y = target.position.y + heightOffset;

        objectToOrbit.position = pos;
    }

    public void ActivateOrbit()
    {
        isActive = true;
    }

    public void DeactivateOrbit()
    {
        isActive = false;
    }

    void Update()
    {
        if (isActive)
        {
            if (target == null || objectToOrbit == null) return;

            Vector3 axis = GetAxis();
            if (axis == Vector3.zero) return;

            // Rotation autour de l’axe
            float angle = speed * Time.deltaTime;
            objectToOrbit.RotateAround(target.position, axis, angle);

            // 1) Contraindre au cercle : projection sur le plan ⟂ axe + rayon = distance
            Vector3 r = objectToOrbit.position - target.position;
            Vector3 rPlan = ProjectOnPlane(r, axis);

            // Si on est tombé pile sur l’axe, on se remet sur un rayon valide
            if (rPlan.sqrMagnitude < 1e-10f)
            {
                rPlan = Vector3.Cross(axis, Vector3.forward);
                if (rPlan.sqrMagnitude < 1e-6f) rPlan = Vector3.Cross(axis, Vector3.right);
            }

            rPlan = rPlan.normalized * Mathf.Max(0.0001f, distance);
            Vector3 newPos = target.position + rPlan;

            // 2) Hauteur absolue monde (ne modifie pas le rayon projeté)
            if (useHeight)
                newPos.y = target.position.y + heightOffset;

            objectToOrbit.position = newPos;
        }
    }

    // Somme des axes cochés → normalisée
    private Vector3 GetAxis()
    {
        Vector3 axis = new Vector3(
            orbitX ? 1f : 0f,
            orbitY ? 1f : 0f,
            orbitZ ? 1f : 0f
        );
        return axis.sqrMagnitude > 0f ? axis.normalized : Vector3.zero;
    }

    // Projection d’un vecteur sur le plan perpendiculaire à 'normal'
    private static Vector3 ProjectOnPlane(Vector3 v, Vector3 normal)
    {
        return v - Vector3.Dot(v, normal) * normal;
    }
}
