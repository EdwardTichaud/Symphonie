using UnityEngine;

/// <summary>
/// Utilitaire simple permettant d'instancier un prefab soit au démarrage,
/// soit sur demande depuis une Timeline.
/// </summary>
public class InstantiateGameObject : MonoBehaviour
{
    [Tooltip("Si vrai, le prefab est instancié dès Start().")]
    public bool instantiateAtStart = false;

    [Tooltip("Prefab à instancier.")]
    public GameObject gameObjectToInstantiate;

    void Start()
    {
        // Instancie automatiquement l'objet au lancement si l'option est activée.
        if (instantiateAtStart)
        {
            Instantiate(gameObjectToInstantiate, transform.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// Instancie un GameObject depuis une timeline ou un autre système externe.
    /// </summary>
    /// <param name="gameObjectToInstantiate">Prefab à instancier.</param>
    public void InstantiateFromTimeline(GameObject gameObjectToInstantiate)
    {
        // Vérifie les références avant d'instancier pour éviter les NullReferenceException
        Vector3 spawnPosition; // Position d'apparition du VFX

        if (NewBattleManager.Instance != null && NewBattleManager.Instance.currentCharacterUnit != null)
        {
            // Positionne le VFX sur l'unité actuellement active
            spawnPosition = NewBattleManager.Instance.currentCharacterUnit.transform.position;
        }
        else
        {
            // En cas d'absence du gestionnaire ou de l'unité, on le place sur le lanceur de timeline
            spawnPosition = transform.position;
            Debug.LogWarning("[InstantiateGameObject] NewBattleManager ou currentCharacterUnit introuvable, instanciation au niveau du TimelineManager.");
        }

        if (gameObjectToInstantiate != null)
        {
            Instantiate(gameObjectToInstantiate, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[InstantiateGameObject] Aucun GameObject fourni pour l'instanciation.");
        }
    }
}
