using UnityEngine;
using System.Collections.Generic;

public class BattlefieldManager : MonoBehaviour
{
    public static BattlefieldManager Instance { get; private set; }

    [Header("Zone active (copiée depuis ZoneManager)")]
    public ZoneSO currentZone;

    // Références instanciées des battlefields courants pour éviter la création
    // pendant la transition de combat
    private readonly List<GameObject> instantiatedBattlefields = new();

    // Parent dans la hiérarchie pour accueillir les battlefields instanciés
    [SerializeField] private Transform battlefieldParent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Tente de trouver le parent des battlefields si non assigné dans
        // l'inspecteur pour garantir la cohérence en jeu.
        if (battlefieldParent == null)
            battlefieldParent = GameObject
                .Find("BattleScene/Battlefields")?.transform;
    }

    /// <summary>
    /// Appelée par ZoneManager quand la zone change.
    /// </summary>
    public void SetCurrentZone(ZoneSO zone)
    {
        if (zone == null)
        {
            Debug.LogWarning("[BattlefieldManager] ZoneSO null !");
            return;
        }

        currentZone = zone;

        // Détruit les battlefields précédemment instanciés pour libérer la mémoire
        foreach (var bf in instantiatedBattlefields)
            if (bf != null)
                Destroy(bf);
        instantiatedBattlefields.Clear();

        if (battlefieldParent == null)
            battlefieldParent = GameObject
                .Find("BattleScene/Battlefields")?.transform;

        // Instancie tous les battlefields dès le changement de zone pour éviter
        // un chargement brutal lors de l'entrée en combat
        foreach (var prefab in currentZone.battlefields)
        {
            if (prefab == null) continue;

            var instance = Instantiate(prefab, battlefieldParent.position,
                Quaternion.identity, battlefieldParent);
            instance.SetActive(false);
            instantiatedBattlefields.Add(instance);
        }

        ActivateFirstBattlefieldInZone();
    }

    /// <summary>
    /// Active le premier battlefield de la zone active, désactive les autres.
    /// </summary>
    private void ActivateFirstBattlefieldInZone()
    {
        if (currentZone == null || currentZone.battlefields.Count == 0)
        {
            Debug.LogWarning("[BattlefieldManager] Pas de battlefield pour cette zone !");
            return;
        }

        for (int i = 0; i < instantiatedBattlefields.Count; i++)
        {
            if (instantiatedBattlefields[i] != null)
                instantiatedBattlefields[i].SetActive(i == 0);
        }

        Debug.Log($"[BattlefieldManager] Premier battlefield activé pour : {currentZone.zoneName}");
    }

    /// <summary>
    /// Active un battlefield spécifique dans la zone active.
    /// </summary>
    public void SetBattlefield(int index)
    {
        if (currentZone == null || instantiatedBattlefields.Count == 0)
        {
            Debug.LogWarning("[BattlefieldManager] Zone invalide ou vide !");
            return;
        }

        if (index < 0 || index >= instantiatedBattlefields.Count)
        {
            Debug.LogWarning($"[BattlefieldManager] Index {index} invalide pour {currentZone.zoneName}");
            return;
        }

        for (int i = 0; i < instantiatedBattlefields.Count; i++)
        {
            if (instantiatedBattlefields[i] != null)
                instantiatedBattlefields[i].SetActive(i == index);
        }

        Debug.Log($"[BattlefieldManager] Battlefield #{index} activé pour {currentZone.zoneName}");
    }

    /// <summary>
    /// Réinstancie tous les battlefields de la zone pour revenir à un état
    /// propre, puis ne conserve que celui correspondant à l'indice fourni.
    /// Cette méthode est pensée pour être appelée en fin de combat afin de
    /// libérer la mémoire des champs non utilisés sans impacter les performances.
    /// </summary>
    /// <param name="index">Indice du battlefield à conserver actif.</param>
    public void RebuildBattlefieldsKeeping(int index)
    {
        if (currentZone == null)
        {
            Debug.LogWarning("[BattlefieldManager] Aucune zone active, impossible de réinstancier les battlefields.");
            return;
        }

        // On détruit les anciennes instances pour repartir sur des versions neuves
        foreach (var bf in instantiatedBattlefields)
            if (bf != null)
                Destroy(bf);
        instantiatedBattlefields.Clear();

        if (battlefieldParent == null)
            battlefieldParent = GameObject
                .Find("BattleScene/Battlefields")?.transform;

        // On recrée chaque battlefield de la zone mais on ne garde en mémoire
        // que celui utilisé lors du combat pour limiter la consommation.
        for (int i = 0; i < currentZone.battlefields.Count; i++)
        {
            var prefab = currentZone.battlefields[i];
            if (prefab == null) continue;

            var instance = Instantiate(prefab, battlefieldParent.position,
                Quaternion.identity, battlefieldParent);

            if (i == index)
            {
                // On conserve l'instance et on l'active immédiatement
                instance.SetActive(true);
                instantiatedBattlefields.Add(instance);
            }
            else
            {
                // Les autres ne sont pas nécessaires et sont détruits aussitôt
                Destroy(instance);
            }
        }

        Debug.Log($"[BattlefieldManager] Battlefields réinstanciés, conservation de l'index {index}.");
    }
}
