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
    public bool reinitWhenZero = false;   // plus utile si le graph gère la mort
    public float zeroThreshold = 0.0001f;

    private float _lastValue = -999f;

    // --- Animation state (Editor + Play) ---
    private bool _animActive = false;
    private float _animStartTime = 0f;
    private float _animDuration = 0f;
    private float _animFrom = 0f;
    private float _animTo = 1f;

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
        if (Application.isPlaying)
        {
            if (_animActive) StepAnimation();
            else Apply(CurrentValue());
        }
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (!Application.isPlaying)
        {
            if (_animActive) StepAnimation();
            else             Apply(CurrentValue());
        }
    }

    private void OnValidate()
    {
        if (!_animActive) Apply(CurrentValue(), force:true);
    }
#endif

    private float CurrentValue()
    {
        if (_animActive)
        {
            // Valeur animée déjà appliquée via StepAnimation; on renvoie _lastValue
            // pour éviter des sauts quand Update/EditorTick rappellent Apply().
            return Mathf.Clamp01(_lastValue > -1f ? _lastValue : progress);
        }

        if (Application.isPlaying && runtimeSlider)
            return Mathf.Clamp01(runtimeSlider.value);

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

        // Répercute la valeur côté UI si on a un slider (utile pendant l’anim en Play)
        if (Application.isPlaying && runtimeSlider)
            runtimeSlider.SetValueWithoutNotify(value);

        if (vfx.HasFloat(progressProperty)) vfx.SetFloat(progressProperty, value);
        if (vfx.HasBool(isIncreasingProperty)) vfx.SetBool(isIncreasingProperty, increasing);

        // Garde la valeur dans le champ public pour que l'inspector reflète l'état
        progress = value;

        // En Editor, rafraîchir l'affichage
#if UNITY_EDITOR
        if (!Application.isPlaying)
            SceneView.RepaintAll();
#endif
    }

    // --- Animation helpers ---
    private float Now()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return (float)EditorApplication.timeSinceStartup;
#endif
        return Time.time;
    }

    private void StepAnimation()
    {
        float t = (_animDuration <= 0f) ? 1f : Mathf.Clamp01((Now() - _animStartTime) / _animDuration);
        float val = Mathf.Lerp(_animFrom, _animTo, t);
        Apply(val, force: true);

        if (t >= 1f)
            _animActive = false;
    }

    /// <summary>
    /// Lance une animation qui fait passer Progress de 0 à 1 sur "duration" secondes.
    /// Fonctionne en Play et en Editor.
    /// </summary>
    public void StartProgressFrom0To1(float duration)
    {
        _animFrom = 0f;
        _animTo = 1f;
        _animDuration = Mathf.Max(0f, duration);
        _animStartTime = Now();
        _animActive = true;

        // Met la valeur initiale immédiatement
        Apply(_animFrom, force: true);

        // En Play, si un slider est branché, on le synchronise
        if (Application.isPlaying && runtimeSlider)
            runtimeSlider.SetValueWithoutNotify(_animFrom);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            SceneView.RepaintAll();
#endif
    }

    // (Optionnel) pour annuler en cours
    public void StopProgressAnimation()
    {
        _animActive = false;
    }
}
