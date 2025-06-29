using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimationEventsManager : MonoBehaviour
{
    public float windowDelay = 0.2f;
    private bool canInstantiate;
    private CharacterUnit target;
    private MusicalMoveSO move;

    public void TriggerQTE(float windowDelay)
    {
        RhythmQTEManager.Instance?.TriggerQTE(windowDelay);
    }
    
    public void TryToDamage()
    {
        target = NewBattleManager.Instance?.currentTargetCharacter;
        move = NewBattleManager.Instance?.currentMove;
        if (target == null || move == null)
            return;

        if (target.isReadyToParry)
        {
            transform.parent.GetComponent<CharacterUnit>().TakeParry();
        }
        else
        {
            target.TakeDamage(move.power);
        }
    }

    public void TryToHeal(int healAmount)
    {
        target = NewBattleManager.Instance?.currentTargetCharacter;
        if (target != null)
        {
            target.Heal(healAmount);
        }
    }

    public void InstantiateHitEffectOnTarget(GameObject effect)
    {
        Transform targetChest = FindChildRecursive(NewBattleManager.Instance.currentTargetCharacter.transform, "Chest");
        if (effect != null && targetChest != null)
        {
            GameObject instantiatedEffect = Instantiate(effect, targetChest.position, Quaternion.identity);
            Destroy(instantiatedEffect, 3f); // Destroy after 5 seconds to clean up
            Debug.Log("InstantiateHitEffectOnTarget called with effect: " + effect.name + " on " + targetChest);
        }
    }

    public void SlowTime(float slowFactor)
    {
        Time.timeScale = slowFactor;
    }

    public void ResetTime()
    {
        Time.timeScale = 1;
    }

    public void PlayVoice(AudioClip audioClip)
    {
        if (audioClip != null)
        {
            AudioManager.Instance?.PlayVoice(audioClip);
        }
        else
        {
            Debug.LogWarning("PlayVoice called with null audioClip");
        }
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }
}
