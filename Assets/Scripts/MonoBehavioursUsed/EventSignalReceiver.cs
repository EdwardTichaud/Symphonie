using UnityEngine;
using System.Collections.Generic; // Permet l'utilisation de listes génériques pour gérer les ennemis

/// <summary>
/// Récepteur générique pour déclencher des évènements depuis une Timeline.
/// L'attribut <see cref="ExecuteAlways"/> permet de prévisualiser ces
/// évènements directement dans l'Éditeur sans lancer le Play Mode,
/// garantissant que tous les signaux sont pris en compte lors de la
/// construction des cinématiques.
/// </summary>
[ExecuteAlways]
public class EventSignalReceiver : MonoBehaviour
{
    [Header("Player Move Settings")]
    public float moveSpeed = 1.8f;

    private Transform playerTransform;

    private Vector3 moveDirection = Vector3.zero;

    private void Start()
    {
        // Méthode exécutée également dans l'Éditeur grâce à ExecuteAlways.
        // On met en cache la référence au joueur pour que les signaux
        // puissent le manipuler pendant la prévisualisation.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        // En prévisualisation, les signaux Timeline peuvent définir un
        // vecteur de déplacement pour tester les trajectoires sans lancer le jeu.
        if (playerTransform != null && moveDirection != Vector3.zero)
        {
            // Déplace dans l'espace local du joueur (avant, arrière, côté)
            Vector3 localMove = playerTransform.TransformDirection(moveDirection);
            playerTransform.position += localMove * moveSpeed * Time.deltaTime;
        }
    }

    public void MoveForward()
    {
        moveDirection = Vector3.forward;
    }

    public void MoveBack()
    {
        moveDirection = Vector3.back;
    }

    public void MoveRightSide()
    {
        moveDirection = Vector3.right;
    }

    public void MoveLeftSide()
    {
        moveDirection = Vector3.left;
    }

    public void StopMove()
    {
        moveDirection = Vector3.zero;
    }

    //public void StartCameraSequence(CameraPath path, Transform referenceTransform = null, Transform startFrom = null, bool forceLookAt = false, Transform lookAtTarget = null, bool alignImmediately = true)
    //{
    //    if (path == null)
    //    {
    //        Debug.LogWarning("[EventSignalReceiver] CameraPath non fourni !");
    //        return;
    //    }

    //    if (CameraController.IsAnyPathPlaying)
    //    {
    //        Debug.Log("[EventSignalReceiver] CameraPath déjà en cours - séquence ignorée.");
    //        return;
    //    }

    //    //CameraController.Instance.StartPathFollow(
    //    //    path,
    //    //    referenceTransform,
    //    //    startFrom,
    //    //    forceLook: forceLookAt,
    //    //    targetToLook: lookAtTarget,
    //    //    alignImmediately: alignImmediately
    //    //);
    //}

    public void ExpandPlayerDetection()
    {
        if (playerTransform != null)
        {
            PlayerDetection playerDetection = playerTransform.GetComponentInChildren<PlayerDetection>();
            if (playerDetection != null)
            {
                playerDetection.currentDetectionRadius += 10f;
            }
            else
            {
                Debug.LogWarning("[EventSignalReceiver] PlayerDetection non trouvé sur le joueur !");
            }
        }
        else
        {
            Debug.LogWarning("[EventSignalReceiver] Joueur non trouvé !");
        }
    }

    /// <summary>
    /// Lance un combat défini par une Timeline en relayant la requête
    /// au <see cref="BattleTransitionManager"/> central. Cette méthode
    /// évite d'ajouter des scripts spécifiques pour chaque Timeline.
    /// </summary>
    /// <param name="enemies">ScriptableObject contenant la liste des ennemis à invoquer.</param>
    public void StartTimelineBattle(TimelineEnemiesSO enemies)
    {
        // Vérifie que l'asset d'ennemis est bien renseigné afin d'empêcher
        // les appels silencieux depuis l'Éditeur.
        if (enemies == null)
        {
            Debug.LogWarning("[EventSignalReceiver] Aucun TimelineEnemiesSO fourni.");
            return;
        }

        // S'assure que le gestionnaire de transition de combat est présent.
        if (BattleTransitionManager.Instance == null)
        {
            Debug.LogWarning("[EventSignalReceiver] BattleTransitionManager introuvable.");
            return;
        }

        // Transmet la demande au gestionnaire, qui se chargera de la suite
        // (chargement de la scène de combat, initialisation des ennemis, etc.).
        BattleTransitionManager.Instance.StartTimelineBattle(enemies);
    }
}
