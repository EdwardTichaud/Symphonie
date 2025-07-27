using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

/// <summary>
/// Surveille les PV d'une unité et déclenche une Timeline lorsque certains seuils sont atteints.
/// Permet par exemple de lancer des cinématiques au cours du combat.
/// </summary>
public class HPThresholdTimelineTrigger : MonoBehaviour
{
    [System.Serializable]
    public class ThresholdData
    {
        [Tooltip("Seuil de PV en pourcentage (0-1). La timeline se déclenche lorsque les PV sont inférieurs ou égaux à ce ratio.")]
        public float hpRatio = 0.5f;
        [Tooltip("Timeline à jouer lorsque le seuil est atteint.")]
        public PlayableDirector timeline;
        [HideInInspector] public bool triggered = false;
    }

    [Header("Unité à surveiller")] public CharacterUnit targetUnit;
    [Header("Liste des déclencheurs")]
    public List<ThresholdData> thresholds = new();

    private void Awake()
    {
        if (targetUnit == null)
            targetUnit = GetComponent<CharacterUnit>();
    }

    private void Update()
    {
        if (targetUnit == null || thresholds.Count == 0)
            return;

        foreach (var t in thresholds)
        {
            if (!t.triggered && targetUnit.currentHP <= targetUnit.Data.baseHP * t.hpRatio)
            {
                t.triggered = true;
                // On enfile la timeline dans le gestionnaire de combat pour qu'elle se joue au prochain tour
                if (NewBattleManager.Instance != null)
                    NewBattleManager.Instance.QueueConditionalTimeline(t.timeline);
                break;
            }
        }
    }

}
