using UnityEngine;

/// <summary>
/// Simple hovering behaviour used to give static meshes a floating feel.
/// The script nudges the object up/down and optionally adds a light yaw sway.
/// Attach it to any transform that should float in place.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class LevitationBehaviour : MonoBehaviour
{
    [Header("Vertical Oscillation")]
    public bool Enabled = true;

    [SerializeField, Tooltip("Maximum vertical offset applied during the oscillation (in meters).")]
    private float amplitude = 0.25f;

    [SerializeField, Tooltip("Number of oscillations per second.")]
    private float frequency = 0.25f;

    [SerializeField, Tooltip("Axis along which the object should oscillate.")]
    private Vector3 oscillationAxis = Vector3.up;

    [SerializeField, Tooltip("Apply the oscillation relative to the local position (otherwise uses world space).")]
    private bool useLocalSpace = true;

    [Header("Rotation Sway")]
    [SerializeField, Tooltip("Degrees of yaw sway applied during the hover. Set to 0 to disable.")]
    private float yawAmplitude = 5f;

    [SerializeField, Tooltip("Speed multiplier for the yaw sway.")]
    private float yawSpeed = 0.5f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float phaseOffset;

    private void Awake()
    {
        CacheBasePose();
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        CacheBasePose();
    }

    private void CacheBasePose()
    {
        basePosition = useLocalSpace ? transform.localPosition : transform.position;
        baseRotation = transform.localRotation;
    }

    private void Update()
    {
        if(!Enabled)
            return;

        if (Mathf.Approximately(amplitude, 0f) || oscillationAxis.sqrMagnitude <= 0.0001f)
            return;

        Vector3 axis = oscillationAxis.normalized;
        float time = (Time.time + phaseOffset) * Mathf.PI * 2f * frequency;
        float offsetValue = Mathf.Sin(time) * amplitude;
        Vector3 offset = axis * offsetValue;

        if (useLocalSpace)
            transform.localPosition = basePosition + offset;
        else
            transform.position = basePosition + offset;

        if (yawAmplitude > 0.01f && yawSpeed > 0.001f)
        {
            float yaw = Mathf.Sin((Time.time + phaseOffset) * yawSpeed) * yawAmplitude;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void OnDisable()
    {
        if (useLocalSpace)
            transform.localPosition = basePosition;
        else
            transform.position = basePosition;

        transform.localRotation = baseRotation;
    }
}
