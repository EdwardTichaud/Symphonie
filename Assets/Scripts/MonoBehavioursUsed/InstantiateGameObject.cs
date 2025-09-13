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
    /// Surcharge conservée pour compatibilité : instancie par défaut sur le
    /// lanceur de l'action (caster) si aucun paramètre de cible n'est fourni.
    /// </summary>
    /// <param name="gameObjectToInstantiate">Prefab à instancier.</param>
    public void InstantiateFromTimeline(GameObject gameObjectToInstantiate)
    {
        InstantiateFromTimeline(gameObjectToInstantiate, false);
    }

    /// <summary>
    /// Instancie un <see cref="GameObject"/> depuis une Timeline, en choisissant
    /// s'il doit apparaître sur le lanceur de l'action (caster) ou sur la cible
    /// actuellement sélectionnée.
    /// </summary>
    /// <param name="gameObjectToInstantiate">Prefab à instancier.</param>
    /// <param name="spawnOnTarget">
    ///     Si <c>true</c>, l'objet est instancié sur la cible actuelle.
    ///     Sinon, il apparaît sur le lanceur de l'action.
    /// </param>
    public void InstantiateFromTimeline(GameObject gameObjectToInstantiate, bool spawnOnTarget)
    {
        // Détermination de la position d'apparition en fonction du paramètre
        // "spawnOnTarget". On tente d'abord de récupérer les unités impliquées
        // via le NewBattleManager ; si elles sont absentes (par exemple en dehors
        // d'un combat), on se rabat sur le GameObject porteur du script.

        Vector3 spawnPosition = transform.position; // Position par défaut : objet porteur

        if (NewBattleManager.Instance != null)
        {
            // Référence vers le lanceur (caster) et la cible
            var caster = NewBattleManager.Instance.currentCharacterUnit;
            var target = NewBattleManager.Instance.currentTargetCharacter;

            if (spawnOnTarget && target != null)
            {
                // Instanciation au niveau de la cible actuelle
                spawnPosition = target.transform.position;
            }
            else if (!spawnOnTarget && caster != null)
            {
                // Instanciation au niveau du lanceur (par défaut)
                spawnPosition = caster.transform.position;
            }
            else
            {
                // Cas où les références sont manquantes : on utilise le
                // GameObject courant et on log un avertissement pour faciliter
                // le débogage.
                Debug.LogWarning("[InstantiateGameObject] Références 'caster' ou 'target' introuvables. Instanciation sur le porteur de script.");
            }
        }
        else
        {
            // Pas de gestionnaire de combat : contexte non prévu (ex. cinématique).
            Debug.LogWarning("[InstantiateGameObject] NewBattleManager introuvable, instanciation sur le porteur de script.");
        }

        // Instancie finalement le prefab si fourni.
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
