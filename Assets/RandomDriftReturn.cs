using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dérive les petits-enfants du GameObject parent jusqu'à une distanceMax
/// en un temps donné, puis les laisse tournoyer doucement. Quand le joueur
/// s'approche, ils reviennent à leur origine en un temps paramétrable.
/// </summary>
public class RandomDriftReturn : MonoBehaviour
{
    [Header("Dérive (position)")]
    [Tooltip("Distance maximale par rapport à l'origine.")]
    public float distanceMax = 3f;

    [Tooltip("Temps (en s) pour atteindre la dérive maximale.")]
    public float tempsAller = 1.5f;

    [Tooltip("Courbe d'interpolation pour la dérive (0→1).")]
    public AnimationCurve courbeAller = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Rotation")]
    [Tooltip("Vitesse angulaire max (en degrés/s) pendant l'allée (dérive active).")]
    public float vitesseRotationMax = 90f;

    [Tooltip("Vitesse angulaire résiduelle (°/s) une fois figé ou revenu.")]
    public float vitesseRotationResiduelle = 5f;

    [Header("Retour (position/rotation)")]
    [Tooltip("Temps (en s) pour revenir à l'origine quand le joueur est proche.")]
    public float tempsRetour = 1.0f;

    [Tooltip("Courbe d'interpolation du retour (0→1).")]
    public AnimationCurve courbeRetour = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Détection joueur")]
    [Tooltip("Distance à laquelle le joueur déclenche la reconstruction (retour).")]
    public float rayonActivation = 2f;

    [Tooltip("Référence optionnelle vers le joueur. Si nul, recherche par tag 'Player'.")]
    public Transform joueur;

    private enum Etat { Aller, IdleMax, Retour }
    private class Morceau
    {
        public Transform tr;
        public Vector3 posOrigine;
        public Quaternion rotOrigine;

        public Vector3 direction;     // direction normalisée vers laquelle on dérive
        public Vector3 rotAxis;       // axe de rotation
        public float rotSpeed;        // °/s pendant l'allée

        public Etat etat;
        public Coroutine routineEnCours;
    }

    private readonly List<Morceau> morceaux = new List<Morceau>();

    void Start()
    {
        // Collecte des petits-enfants
        foreach (Transform enfant in transform)
        {
            foreach (Transform petitEnfant in enfant)
            {
                Vector3 axis = Random.onUnitSphere.normalized;

                var m = new Morceau
                {
                    tr = petitEnfant,
                    posOrigine = petitEnfant.position,
                    rotOrigine = petitEnfant.rotation,
                    direction = Random.onUnitSphere.normalized,
                    rotAxis = axis,
                    rotSpeed = Random.Range(vitesseRotationMax * 0.5f, vitesseRotationMax),
                    etat = Etat.Aller,
                    routineEnCours = null
                };

                // Lance l'allée (dérive vers distanceMax en tempsAller)
                m.routineEnCours = StartCoroutine(RoutineAller(m));
                morceaux.Add(m);
            }
        }
    }

    void Update()
    {
        // Recherche du joueur si besoin
        if (joueur == null)
        {
            var obj = GameObject.FindGameObjectWithTag("Player");
            if (obj) joueur = obj.transform;
        }

        foreach (var m in morceaux)
        {
            // Rotation : rapide pendant l'allée, résiduelle sinon
            float speed = (m.etat == Etat.Aller) ? m.rotSpeed : Mathf.Max(0.01f, vitesseRotationResiduelle);
            m.tr.Rotate(m.rotAxis, speed * Time.deltaTime, Space.Self);

            // Détection joueur pour déclencher le retour
            if (joueur != null && m.etat != Etat.Retour)
            {
                float d = Vector3.Distance(joueur.position, m.posOrigine);
                if (d <= rayonActivation)
                {
                    if (m.routineEnCours != null) StopCoroutine(m.routineEnCours);
                    m.routineEnCours = StartCoroutine(RoutineRetour(m));
                }
            }
        }
    }

    private IEnumerator RoutineAller(Morceau m)
    {
        m.etat = Etat.Aller;

        // Gestion des cas limites
        if (tempsAller <= 0f || distanceMax <= 0f)
        {
            // Téléporte directement au max de la dérive
            m.tr.position = m.posOrigine + m.direction * Mathf.Max(0f, distanceMax);
            m.etat = Etat.IdleMax;
            yield break;
        }

        Vector3 startPos = m.tr.position;
        Quaternion startRot = m.tr.rotation; // on laisse la rotation libre via Update()
        Vector3 targetPos = m.posOrigine + m.direction * distanceMax;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, tempsAller);
            float k = courbeAller.Evaluate(Mathf.Clamp01(t));

            // Interpolation vers la cible de dérive
            m.tr.position = Vector3.LerpUnclamped(startPos, targetPos, k);
            yield return null;
        }

        // Snap final pour précision
        m.tr.position = targetPos;
        m.etat = Etat.IdleMax; // figé en position (rotation résiduelle continue)
        m.routineEnCours = null;
    }

    private IEnumerator RoutineRetour(Morceau m)
    {
        m.etat = Etat.Retour;

        if (tempsRetour <= 0f)
        {
            // Retour instantané
            m.tr.position = m.posOrigine;
            m.tr.rotation = m.rotOrigine;
            m.etat = Etat.IdleMax; // tu peux mettre IdleMax ou basculer sur un autre état si besoin
            m.routineEnCours = null;
            yield break;
        }

        Vector3 startPos = m.tr.position;
        Quaternion startRot = m.tr.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, tempsRetour);
            float k = courbeRetour.Evaluate(Mathf.Clamp01(t));

            m.tr.position = Vector3.LerpUnclamped(startPos, m.posOrigine, k);
            m.tr.rotation = Quaternion.SlerpUnclamped(startRot, m.rotOrigine, k);
            yield return null;
        }

        m.tr.position = m.posOrigine;
        m.tr.rotation = m.rotOrigine;

        // Une fois revenu, on le laisse tournoyer doucement (résiduel)
        m.etat = Etat.IdleMax;
        m.routineEnCours = null;
    }
}
