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
    /// Lance un combat directement depuis une Timeline en spécifiant
    /// manuellement les ennemis à affronter. Cette méthode contourne la
    /// détection automatique du joueur et permet de contrôler totalement
    /// les combats déclenchés par des cinématiques.
    /// </summary>
    /// <param name="enemy1">Premier ennemi à invoquer.</param>
    /// <param name="enemy2">Second ennemi optionnel.</param>
    /// <param name="enemy3">Troisième ennemi optionnel.</param>
    public void StartTimelineBattle(CharacterData enemy1, CharacterData enemy2 = null, CharacterData enemy3 = null)
    {
        // Rassemble tous les ennemis fournis dans une liste afin de les
        // transmettre au gestionnaire de combat.
        List<CharacterData> enemies = new();
        if (enemy1 != null) enemies.Add(enemy1);
        if (enemy2 != null) enemies.Add(enemy2);
        if (enemy3 != null) enemies.Add(enemy3);

        // Si aucun ennemi n'est renseigné, on annule la procédure et on loggue un avertissement.
        if (enemies.Count == 0)
        {
            Debug.LogWarning("[EventSignalReceiver] Aucun ennemi spécifié pour le combat déclenché par Timeline.");
            return;
        }

        // Copie des ennemis dans le NewBattleManager afin qu'ils soient utilisés lors de l'initialisation du combat.
        if (NewBattleManager.Instance != null)
        {
            NewBattleManager.Instance.enemyTemplates.Clear();
            NewBattleManager.Instance.enemyTemplates.AddRange(enemies);
        }
        else
        {
            Debug.LogWarning("[EventSignalReceiver] NewBattleManager introuvable, lancement du combat annulé.");
            return;
        }

        // Désactive la détection automatique pour éviter tout conflit pendant la transition.
        PlayerDetection pd = FindFirstObjectByType<PlayerDetection>();
        if (pd != null)
        {
            pd.detectionOn = false;
            pd.detectedEnemies.Clear();
        }

        // Démarre la transition de combat avec les ennemis spécifiés.
        BattleTransitionManager.Instance.StartCombatTransition();
    }
}
