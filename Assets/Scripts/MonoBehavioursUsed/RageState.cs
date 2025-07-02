using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class RageState : MonoBehaviour
{
    [Header("Effets visuels")]
    public GameObject rageEffectPrefab;
    public GameObject calmEffectPrefab;
    public Vector3 effectOffset = new(0f, 2f, 0f);

    [Header("Animations")]
    public AnimationClip rageAnimation;
    public AnimationClip calmAnimation;

    [Header("Effets sonores")]
    public AudioClip rageClip;
    public AudioClip calmClip;

    [Header("Camera")]
    public OrbitAroundTriggerSO rageCameraPath;

    private AudioSource audioSource;
    private Animator animator;
    private GameObject currentEffect;
    private bool isEnraged;

    private CharacterUnit unit;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    public void OnRageChanged(float currentRage)
    {
        if (!isEnraged && currentRage >= GetMaxRage())
            EnterRage();
        else if (isEnraged && currentRage <= GetBaseRage())
            ExitRage();
    }

    private float GetMaxRage() => unit != null && unit.Data != null ? unit.Data.maxRage : 100f;
    private float GetBaseRage() => unit != null && unit.Data != null ? unit.Data.baseRage : 0f;

    private void EnterRage()
    {
        isEnraged = true;
        if (rageClip != null && audioSource != null)
            audioSource.PlayOneShot(rageClip);
        if (rageAnimation != null && animator != null)
            animator.Play(rageAnimation.name);
        rageCameraPath?.StartOrbit();
        if (rageEffectPrefab != null)
            currentEffect = Instantiate(rageEffectPrefab, transform.position + effectOffset, Quaternion.identity, transform);
    }

    private void ExitRage()
    {
        isEnraged = false;
        if (calmClip != null && audioSource != null)
            audioSource.PlayOneShot(calmClip);
        if (calmAnimation != null && animator != null)
            animator.Play(calmAnimation.name);
        rageCameraPath?.StopOrbit();
        if (currentEffect != null)
        {
            Destroy(currentEffect);
            currentEffect = null;
        }
        if (calmEffectPrefab != null)
            Instantiate(calmEffectPrefab, transform.position + effectOffset, Quaternion.identity);
    }
}
