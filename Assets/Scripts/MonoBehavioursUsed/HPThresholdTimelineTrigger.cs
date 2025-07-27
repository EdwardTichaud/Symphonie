using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
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
        public TimelineAsset timeline;
        [HideInInspector] public bool triggered = false;
    }

    [System.Serializable]
    public class UnitThresholds
    {
        [Tooltip("Unité à surveiller")] public CharacterUnit unit;
        [Tooltip("Liste des déclencheurs associés")] public List<ThresholdData> thresholds = new();
    }

    [Header("Unités et seuils à surveiller")]
    public List<UnitThresholds> units = new();

    private void Update()
    {
        if (units.Count == 0)
            return;

        foreach (var unitData in units)
        {
            if (unitData.unit == null || unitData.thresholds.Count == 0)
                continue;

            // Vérifie que l'unité surveillée est bien présente dans le combat courant
            if (NewBattleManager.Instance == null ||
                !NewBattleManager.Instance.unitsInBattle.Contains(unitData.unit))
                continue;

            foreach (var t in unitData.thresholds)
            {
                if (!t.triggered &&
                    unitData.unit.currentHP <= unitData.unit.Data.baseHP * t.hpRatio)
                {
                    t.triggered = true;
                    // Récupère l'Animator de l'unité pour binder correctement la timeline
                    GameObject animatorGO = unitData.unit.GetComponentInChildren<Animator>()?.gameObject;
                    // Enfile la timeline dans le gestionnaire pour qu'elle soit jouée au bon moment
                    NewBattleManager.Instance.QueueConditionalTimeline(
                        t.timeline,
                        animatorGO,
                        "BattleCamera");
                    break;
                }
            }
        }
    }

}
