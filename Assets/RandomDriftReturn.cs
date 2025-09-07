using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleDriftReturn_ConstantDirection : MonoBehaviour
{
    public enum ModeSelection
    {
        ChildrenOnly,  // seulement les enfants directs
        Descendants    // toute la descendance (limitable par profondeurMax)
    }

    [Header("Sélection des cibles")]
    [Tooltip("Choix de la portée: enfants directs ou toute la descendance.")]
    public ModeSelection modeDeSelection = ModeSelection.ChildrenOnly;

    [Tooltip("Profondeur max quand 'Descendants' est choisi. 1 = enfants directs, 2 = petits-enfants, etc. 0 = illimité.")]
    [Min(0)] public int profondeurMax = 0;

    [Header("Joueur")]
    [Tooltip("Si non renseigné, recherche par tag 'Player'.")]
    public Transform joueur;

    [Tooltip("Distance où le joueur est considéré 'proche' (déclenche le retour).")]
    public float rayonProche = 3f;

    [Tooltip("Distance où le joueur est considéré 'loin' (autorise la dérive). Doit être > rayonProche.")]
    public float rayonLoin = 4f;

    [Header("Dérive")]
    [Tooltip("Vitesse de dérive (m/s) quand le joueur est loin.")]
    public float driftSpeed = 0.2f;

    [Tooltip("Distance max de dérive depuis la position d'origine. Mettre 50 pour ton cas.")]
    public float maxDriftDistance = 50f;

    [Header("Retour")]
    [Tooltip("Durée du retour (s) quand le joueur est proche. Mettre 0.5 pour ton cas.")]
    public float dureeRetour = 0.5f;

    [Header("Rotation (optionnel)")]
    [Tooltip("Tournis léger (°/s) pour l'effet d'espace. 0 pour désactiver.")]
    public float rotationResiduelleDegPerSec = 10f;

    [Tooltip("Si true, les objets continuent de tourner même à l'origine. Sinon ils restent immobiles à l'origine.")]
    public bool rotateInPlace = true;

    private enum Etat { Derive, ArretMax, Retour, IdleAOrigine }

    private class Morceau
    {
        public Transform tr;
        public Vector3 posOrigine;
        public Quaternion rotOrigine;

        public Vector3 directionFixe;   // Fixée une fois pour toutes
        public Vector3 rotAxis;         // Axe de tournis

        public Etat etat;
        public Coroutine routine;
    }

    private readonly List<Morceau> morceaux = new List<Morceau>();

    void OnValidate()
    {
        if (rayonLoin < rayonProche) rayonLoin = rayonProche + 0.5f;
        if (maxDriftDistance < 0f) maxDriftDistance = 0f;
        if (dureeRetour < 0f) dureeRetour = 0f;
        if (driftSpeed < 0f) driftSpeed = 0f;
        if (rotationResiduelleDegPerSec < 0f) rotationResiduelleDegPerSec = 0f;
        if (modeDeSelection == ModeSelection.ChildrenOnly) profondeurMax = 1; // cohérent: enfants directs = profondeur 1
    }

    void Start()
    {
        ReconstruireMorceaux();
    }

    void Update()
    {
        // Trouve le joueur si besoin
        if (joueur == null)
        {
            var obj = GameObject.FindGameObjectWithTag("Player");
            if (obj) joueur = obj.transform;
        }

        foreach (var m in morceaux)
        {
            // Rotation autorisée en dérive/arrêtMax, et éventuellement à l'origine
            bool canRotate = (m.etat == Etat.Derive || m.etat == Etat.ArretMax) ||
                             (rotateInPlace && m.etat == Etat.IdleAOrigine);

            if (canRotate && rotationResiduelleDegPerSec > 0f)
                m.tr.Rotate(m.rotAxis, rotationResiduelleDegPerSec * Time.deltaTime, Space.Self);

            float distJ = joueur ? Vector3.Distance(joueur.position, m.posOrigine) : float.PositiveInfinity;

            switch (m.etat)
            {
                case Etat.Derive:
                    if (distJ <= rayonProche)
                    {
                        BasculerRoutine(m, RoutineRetour(m));
                        break;
                    }
                    m.tr.position += m.directionFixe * driftSpeed * Time.deltaTime;

                    Vector3 offset = m.tr.position - m.posOrigine;
                    float dist = offset.magnitude;
                    if (dist >= maxDriftDistance)
                    {
                        m.tr.position = m.posOrigine + m.directionFixe * maxDriftDistance;
                        m.etat = Etat.ArretMax;
                    }
                    break;

                case Etat.ArretMax:
                    if (distJ <= rayonProche)
                        BasculerRoutine(m, RoutineRetour(m));
                    break;

                case Etat.IdleAOrigine:
                    if (distJ >= rayonLoin)
                        m.etat = Etat.Derive; // repart en dérive (même direction)
                    break;

                case Etat.Retour:
                    // géré par la coroutine
                    break;
            }
        }
    }

    private void BasculerRoutine(Morceau m, IEnumerator routine)
    {
        if (m.routine != null) StopCoroutine(m.routine);
        m.routine = StartCoroutine(routine);
    }

    private IEnumerator RoutineRetour(Morceau m)
    {
        m.etat = Etat.Retour;

        if (dureeRetour <= 0f)
        {
            m.tr.position = m.posOrigine;
            m.tr.rotation = m.rotOrigine;
            m.etat = Etat.IdleAOrigine;
            m.routine = null;
            yield break;
        }

        Vector3 startPos = m.tr.position;
        Quaternion startRot = m.tr.rotation;

        float t = 0f;
        float inv = 1f / Mathf.Max(0.0001f, dureeRetour);

        while (t < 1f)
        {
            t += Time.deltaTime * inv;
            float k = Mathf.SmoothStep(0f, 1f, t);

            m.tr.position = Vector3.LerpUnclamped(startPos, m.posOrigine, k);
            m.tr.rotation = Quaternion.SlerpUnclamped(startRot, m.rotOrigine, k);
            yield return null;
        }

        m.tr.position = m.posOrigine;
        m.tr.rotation = m.rotOrigine;

        m.etat = Etat.IdleAOrigine;
        m.routine = null;
    }

    // ======== Construction de la liste des cibles ========

    [ContextMenu("Reconstruire la liste")]
    public void ReconstruireMorceaux()
    {
        morceaux.Clear();

        if (modeDeSelection == ModeSelection.ChildrenOnly)
        {
            // enfants directs uniquement
            foreach (Transform enfant in transform)
                AjouterMorceau(enfant);
        }
        else // Descendants
        {
            if (profondeurMax <= 0)
            {
                // illimité
                foreach (var tr in EnumererDescendanceIllimitee(transform))
                    AjouterMorceau(tr);
            }
            else
            {
                // limité à profondeurMax
                foreach (var tr in EnumererDescendanceLimitee(transform, profondeurMax))
                    AjouterMorceau(tr);
            }
        }
    }

    private void AjouterMorceau(Transform tr)
    {
        if (tr == transform) return; // on ignore le parent lui-même
        var m = new Morceau
        {
            tr = tr,
            posOrigine = tr.position,
            rotOrigine = tr.rotation,
            directionFixe = Random.onUnitSphere.normalized,
            rotAxis = Random.onUnitSphere.normalized,
            etat = Etat.Derive,
            routine = null
        };
        morceaux.Add(m);
    }

    // Descendance illimitée (DFS)
    private IEnumerable<Transform> EnumererDescendanceIllimitee(Transform root)
    {
        foreach (Transform child in root)
        {
            yield return child;
            foreach (var sub in EnumererDescendanceIllimitee(child))
                yield return sub;
        }
    }

    // Descendance limitée en profondeur (BFS)
    // profondeur = 1 => enfants directs ; 2 => petits-enfants, etc.
    private IEnumerable<Transform> EnumererDescendanceLimitee(Transform root, int profondeur)
    {
        var queue = new Queue<(Transform t, int d)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (t, d) = queue.Dequeue();
            if (d >= profondeur) continue;

            foreach (Transform child in t)
            {
                yield return child;
                queue.Enqueue((child, d + 1));
            }
        }
    }
}
