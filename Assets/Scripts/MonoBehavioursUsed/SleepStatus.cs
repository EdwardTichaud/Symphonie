using UnityEngine;

public class SleepStatus : MonoBehaviour
{
    private bool isAsleep;
    public bool IsAsleep => isAsleep;

    private AudioSource audioSource;

    [Header("Effets visuels")]
    [SerializeField] private GameObject sleepPrefab;
    [SerializeField] private GameObject wakeUpPrefab;
    [SerializeField] private Vector3 effectOffset = new Vector3(0, 2f, 0);

    [Header("Effets sonores")]
    [SerializeField] private AudioClipSO sleepClip;
    [SerializeField] private AudioClipSO wakeUpClip;

    private GameObject currentSleepEffect;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void Sleep()
    {
        if (isAsleep)
            return;

        isAsleep = true;

        if (sleepClip != null && sleepClip.Clip != null && audioSource != null)
            audioSource.PlayOneShot(sleepClip.Clip, sleepClip.Volume);

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

        if (wakeUpClip != null && wakeUpClip.Clip != null && audioSource != null)
            audioSource.PlayOneShot(wakeUpClip.Clip, wakeUpClip.Volume);

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
