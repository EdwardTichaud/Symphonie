using UnityEngine;

/// <summary>
/// Fait dériver un objet depuis sa position d'origine vers une direction aléatoire
/// en ralentissant progressivement jusqu'à l'arrêt, puis le ramène à sa position
/// et rotation d'origine lorsque le joueur s'en approche.
/// Utilisé pour simuler un pont explosé qui se reconstitue sous les pas du joueur.
/// </summary>
public class RandomDriftReturn : MonoBehaviour
{
    [Header("Déplacement initial")] // paramètres de dérive
    [Tooltip("Vitesse de départ du morceau lors de la dérive.")]
    public float vitesseInitiale = 5f;

    [Tooltip("Taux auquel la vitesse diminue chaque seconde.")]
    public float deceleration = 2f;

    [Tooltip("Distance maximale par rapport à l'origine avant l'arrêt complet.")]
    public float distanceMax = 3f;

    [Header("Retour du joueur")]
    [Tooltip("Distance à laquelle le joueur déclenche la reconstruction.")]
    public float rayonActivation = 2f;

    [Tooltip("Référence optionnelle vers le joueur. Si nul, recherche par tag 'Player'.")]
    public Transform joueur;

    // Données internes mémorisant l'état de départ
    private Vector3 positionOrigine;
    private Quaternion rotationOrigine;

    // Direction choisie aléatoirement pour la dérive
    private Vector3 direction;

    // Vitesse actuelle de l'objet
    private float vitesseCourante;

    // Indique si l'objet est revenu à son état d'origine
    private bool reconstruit = false;

    void Start()
    {
        // Sauvegarde de la position et rotation initiales
        positionOrigine = transform.position;
        rotationOrigine = transform.rotation;

        // Choix d'une direction aléatoire dans l'espace
        direction = Random.onUnitSphere;
        direction.Normalize();

        // Initialisation de la vitesse à la valeur souhaitée
        vitesseCourante = vitesseInitiale;
    }

    void Update()
    {
        if (!reconstruit)
        {
            // Tant que l'objet n'est pas revenu, il dérive puis attend le joueur
            Deriver();
            VerifierProximiteJoueur();
        }
    }

    /// <summary>
    /// Déplace l'objet dans la direction définie en réduisant la vitesse au fil du temps.
    /// </summary>
    private void Deriver()
    {
        if (vitesseCourante <= 0f)
            return; // mouvement déjà stoppé

        float step = vitesseCourante * Time.deltaTime;
        transform.position += direction * step;

        // Ralentissement progressif jusqu'à l'arrêt complet
        vitesseCourante = Mathf.Max(0f, vitesseCourante - deceleration * Time.deltaTime);

        // Empêche l'objet d'aller au-delà de la distance maximale
        if (Vector3.Distance(positionOrigine, transform.position) >= distanceMax)
        {
            vitesseCourante = 0f;
        }
    }

    /// <summary>
    /// Vérifie si le joueur est suffisamment proche pour déclencher le retour.
    /// </summary>
    private void VerifierProximiteJoueur()
    {
        Transform cible = joueur;
        if (cible == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                cible = playerObj.transform;
        }

        if (cible == null)
            return; // aucun joueur trouvé

        float distanceJoueur = Vector3.Distance(cible.position, positionOrigine);
        if (distanceJoueur <= rayonActivation)
            StartCoroutine(Retourner());
    }

    /// <summary>
    /// Coroutine ramenant progressivement l'objet à son état d'origine.
    /// </summary>
    private System.Collections.IEnumerator Retourner()
    {
        reconstruit = true; // évite plusieurs appels

        Vector3 departPos = transform.position;
        Quaternion departRot = transform.rotation;
        float duree = 1f; // durée du retour en secondes
        float temps = 0f;

        while (temps < duree)
        {
            temps += Time.deltaTime;
            float t = temps / duree;
            transform.position = Vector3.Lerp(departPos, positionOrigine, t);
            transform.rotation = Quaternion.Slerp(departRot, rotationOrigine, t);
            yield return null; // attend la frame suivante
        }

        // S'assure que la position et la rotation finales sont exactes
        transform.position = positionOrigine;
        transform.rotation = rotationOrigine;
    }
}
