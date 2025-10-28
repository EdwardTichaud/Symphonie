using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class VFXProgressDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private VisualEffect vfx;
    [SerializeField] private Slider runtimeSlider; // optionnel (Play)

    [Header("VFX property names")]
    [SerializeField] private string progressProperty = "Progress";
    [SerializeField] private string isIncreasingProperty = "IsIncreasing"; // bool

    [Header("Inspector control (Editor)")]
    [Range(0f, 1f)] public float progress = 0f;

    [Header("Zero handling")]
    public bool reinitWhenZero = false;   // inutile désormais, on tue via le graph
    public float zeroThreshold = 0.0001f;

    private float _lastValue = -999f;

    private void Reset()
    {
        if (!vfx) vfx = GetComponent<VisualEffect>();
        if (!runtimeSlider) runtimeSlider = GetComponentInChildren<Slider>();
    }

    private void OnEnable()
    {
        Apply(CurrentValue(), force: true);
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
#endif
    }
    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    private void Update()
    {
        if (Application.isPlaying) Apply(CurrentValue());
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (!Application.isPlaying) Apply(CurrentValue());
    }
    private void OnValidate()
    {
        Apply(CurrentValue(), force:true);
    }
#endif

    private float CurrentValue()
    {
        if (Application.isPlaying && runtimeSlider) return Mathf.Clamp01(runtimeSlider.value);
        return Mathf.Clamp01(progress);
    }

    private void Apply(float value, bool force = false)
    {
        if (!vfx) return;

        value = Mathf.Clamp01(value);
        float delta = (_lastValue < -1f) ? 0f : (value - _lastValue);
        bool increasing = delta > 0.00001f;

        if (!force && Mathf.Approximately(value, _lastValue)) return;
        _lastValue = value;

        if (vfx.HasFloat(progressProperty)) vfx.SetFloat(progressProperty, value);
        if (vfx.HasBool(isIncreasingProperty)) vfx.SetBool(isIncreasingProperty, increasing);
    }
}
