using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la dérive aléatoire puis la reconstruction de plusieurs morceaux.
/// <br/>
/// Ce script doit être placé sur l'objet parent. Seuls les enfants directs des
/// enfants directs (les « morceaux ») sont affectés.
/// Il permet de simuler un pont explosé qui se reconstitue sous les pas du joueur.
/// </summary>
public class RandomDriftReturn : MonoBehaviour
{
    [Header("Déplacement initial")] // Paramètres de la dérive
    [Tooltip("Vitesse de départ des morceaux lors de la dérive.")]
    public float vitesseInitiale = 5f;

    [Tooltip("Taux auquel la vitesse diminue chaque seconde.")]
    public float deceleration = 2f;

    [Tooltip("Distance maximale par rapport à l'origine avant l'arrêt complet.")]
    public float distanceMax = 3f;

    [Header("Rotation des morceaux")]
    [Tooltip("Vitesse angulaire maximale (en degrés/s) pour la dérive aléatoire.")]
    public float vitesseRotationMax = 90f;

    [Header("Retour du joueur")]
    [Tooltip("Distance à laquelle le joueur déclenche la reconstruction.")]
    public float rayonActivation = 2f;

    [Tooltip("Référence optionnelle vers le joueur. Si nul, recherche par tag 'Player'.")]
    public Transform joueur;

    /// <summary>
    /// Informations stockées pour chaque morceau du pont.
    /// </summary>
    private class Morceau
    {
        public Transform tr;          // Référence vers le Transform du morceau
        public Vector3 posOrigine;    // Position de départ
        public Quaternion rotOrigine; // Rotation de départ
        public Vector3 direction;     // Direction aléatoire de dérive
        public float vitesse;         // Vitesse actuelle
        public Vector3 vitesseRotation; // Vitesse de rotation aléatoire
        public bool enRetour;         // Indique si le morceau revient à l'origine
    }

    // Liste de tous les morceaux concernés (les petits-enfants)
    private readonly List<Morceau> morceaux = new List<Morceau>();

    void Start()
    {
        // Récupération de tous les enfants directs des enfants directs
        foreach (Transform enfant in transform)
        {
            foreach (Transform petitEnfant in enfant)
            {
                var m = new Morceau
                {
                    tr = petitEnfant,
                    posOrigine = petitEnfant.position,
                    rotOrigine = petitEnfant.rotation,
                    direction = Random.onUnitSphere.normalized,
                    vitesse = vitesseInitiale,
                    vitesseRotation = new Vector3(
                        Random.Range(-vitesseRotationMax, vitesseRotationMax),
                        Random.Range(-vitesseRotationMax, vitesseRotationMax),
                        Random.Range(-vitesseRotationMax, vitesseRotationMax)
                    ), // Rotation aléatoire sur les 3 axes
                    enRetour = false
                };
                morceaux.Add(m);
            }
        }
    }

    void Update()
    {
        // Recherche du joueur au besoin (effectuée une seule fois)
        if (joueur == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                joueur = obj.transform;
        }

        // Mise à jour de chaque morceau indépendamment
        foreach (Morceau m in morceaux)
        {
            if (!m.enRetour)
            {
                if (m.vitesse > 0f)
                {
                    // Tant qu'il dérive, on applique le déplacement et la rotation
                    Deriver(m);
                }
                else
                {
                    // Si le joueur s'est éloigné, on relance une nouvelle dérive
                    VerifierEloignementJoueur(m);
                }

                // Vérifie en permanence la proximité pour déclencher le retour
                VerifierProximiteJoueur(m);
            }
        }
    }

    /// <summary>
    /// Déplace un morceau dans sa direction en réduisant progressivement sa vitesse.
    /// </summary>
    private void Deriver(Morceau m)
    {
        if (m.vitesse <= 0f)
            return; // Le morceau est déjà immobile

        float step = m.vitesse * Time.deltaTime;
        m.tr.position += m.direction * step; // Application du déplacement

        // Fait tourner le morceau selon sa vitesse angulaire propre
        m.tr.Rotate(m.vitesseRotation * Time.deltaTime, Space.Self);

        // Ralentissement progressif jusqu'à l'arrêt complet
        m.vitesse = Mathf.Max(0f, m.vitesse - deceleration * Time.deltaTime);

        // On évite de dépasser la distance maximale définie
        if (Vector3.Distance(m.posOrigine, m.tr.position) >= distanceMax)
            m.vitesse = 0f;
    }

    /// <summary>
    /// Vérifie si le joueur est assez proche pour déclencher le retour du morceau.
    /// </summary>
    private void VerifierProximiteJoueur(Morceau m)
    {
        if (joueur == null)
            return; // Aucun joueur détecté

        float distanceJoueur = Vector3.Distance(joueur.position, m.posOrigine);
        if (distanceJoueur <= rayonActivation)
            StartCoroutine(Retourner(m));
    }

    /// <summary>
    /// Relance la dérive lorsque le joueur s'est suffisamment éloigné.
    /// </summary>
    private void VerifierEloignementJoueur(Morceau m)
    {
        if (joueur == null)
            return; // Aucun joueur détecté

        float distanceJoueur = Vector3.Distance(joueur.position, m.posOrigine);
        if (distanceJoueur > rayonActivation)
        {
            // Nouvelle direction et vitesse de rotation aléatoires
            m.direction = Random.onUnitSphere.normalized;
            m.vitesseRotation = new Vector3(
                Random.Range(-vitesseRotationMax, vitesseRotationMax),
                Random.Range(-vitesseRotationMax, vitesseRotationMax),
                Random.Range(-vitesseRotationMax, vitesseRotationMax)
            );

            // On réinitialise la vitesse de dérive
            m.vitesse = vitesseInitiale;
        }
    }

    /// <summary>
    /// Coroutine qui ramène progressivement un morceau à sa position et rotation initiales.
    /// </summary>
    private IEnumerator Retourner(Morceau m)
    {
        m.enRetour = true; // Empêche plusieurs lancements

        Vector3 departPos = m.tr.position;
        Quaternion departRot = m.tr.rotation;
        float duree = 1f; // Durée du retour en secondes
        float temps = 0f;

        while (temps < duree)
        {
            temps += Time.deltaTime;
            float t = temps / duree;
            m.tr.position = Vector3.Lerp(departPos, m.posOrigine, t); // Interpolation de la position
            m.tr.rotation = Quaternion.Slerp(departRot, m.rotOrigine, t); // Interpolation de la rotation
            yield return null; // Attend la frame suivante
        }

        // On s'assure que la position et la rotation finales sont exactes
        m.tr.position = m.posOrigine;
        m.tr.rotation = m.rotOrigine;

        // Le morceau est de retour à l'origine et devient immobile
        m.vitesse = 0f;
        m.enRetour = false; // Attente que le joueur s'éloigne pour relancer la dérive
    }
}
