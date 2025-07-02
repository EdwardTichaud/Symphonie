using UnityEngine;

public class SleepStatus : MonoBehaviour
{
    private bool isAsleep;
    public bool IsAsleep => isAsleep;

    [Header("Effets visuels")]
    [SerializeField] private GameObject sleepPrefab;
    [SerializeField] private GameObject wakeUpPrefab;
    [SerializeField] private Vector3 effectOffset = new Vector3(0, 2f, 0);

    private GameObject currentSleepEffect;

    public void Sleep()
    {
        if (isAsleep)
            return;

        isAsleep = true;

        if (sleepPrefab != null && currentSleepEffect == null)
        {
            currentSleepEffect = Instantiate(sleepPrefab, transform);
            currentSleepEffect.transform.localPosition = effectOffset;
        }
    }

    public void WakeUp()
    {
        if (!isAsleep)
            return;

        isAsleep = false;

        if (currentSleepEffect != null)
        {
            Destroy(currentSleepEffect);
            currentSleepEffect = null;
        }

        if (wakeUpPrefab != null)
        {
            var effect = Instantiate(wakeUpPrefab, transform.position + effectOffset, Quaternion.identity);
            Destroy(effect, 3f);
        }
    }

    public void OnDamageTaken()
    {
        if (isAsleep)
            WakeUp();
    }
}
