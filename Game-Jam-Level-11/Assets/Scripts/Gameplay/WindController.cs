using System;
using UnityEngine;

/// <summary>
/// Runs the repeating warning and wind-gust cycle.
/// Gust direction and strength are chosen before the warning so the player can react.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindController : MonoBehaviour
{
    [Header("Wind cycle")]
    [SerializeField, Min(6f)] private float intervalBetweenGusts = 15f;
    [SerializeField, Min(0f)] private float intervalVariation = 4f;
    [SerializeField, Min(1f)] private float warningDuration = 2.2f;
    [SerializeField, Min(0.5f)] private float gustDuration = 2.4f;
    [SerializeField, Min(0f)] private float gustDurationVariation = 0.65f;

    [Header("Gust strength")]
    [SerializeField, Min(0.1f)] private float minimumGustStrength = 1.15f;
    [SerializeField, Min(0.1f)] private float maximumGustStrength = 1.85f;
    [SerializeField, Min(0.1f)] private float strongGustThreshold = 1.55f;

    [Header("Wind direction")]
    [SerializeField] private Vector2 windDirection = Vector2.right;
    [SerializeField] private bool randomizeWindDirection = true;
    [SerializeField, Range(0f, 180f)] private float directionVariationDegrees = 180f;

    public bool IsWarning { get; private set; }
    public bool IsWindActive { get; private set; }
    public Vector2 WindDirection => _currentWindDirection.sqrMagnitude > 0.0001f
        ? _currentWindDirection.normalized
        : Vector2.right;
    public float CurrentGustStrength { get; private set; }
    public bool IsStrongGust => CurrentGustStrength >= strongGustThreshold;

    public string DirectionArrow
    {
        get
        {
            float angle = Mathf.Repeat(
                Mathf.Atan2(WindDirection.y, WindDirection.x) * Mathf.Rad2Deg, 360f);
            int sector = Mathf.RoundToInt(angle / 45f) % 8;
            switch (sector)
            {
                case 0: return "→";
                case 1: return "↗";
                case 2: return "↑";
                case 3: return "↖";
                case 4: return "←";
                case 5: return "↙";
                case 6: return "↓";
                default: return "↘";
            }
        }
    }

    public string StatusMessage
    {
        get
        {
            if (IsWarning)
            {
                return (IsStrongGust ? "СИЛЬНЫЙ ВЕТЕР ПОДНИМАЕТСЯ " : "ВЕТЕР ПОДНИМАЕТСЯ ") +
                       DirectionArrow + " · ПРИГОТОВЬТЕСЬ";
            }

            if (IsWindActive)
            {
                string label = IsStrongGust ? "СИЛЬНЫЙ ПОРЫВ" : "ПОРЫВ СЕРОГО ВЕТРА";
                return label + " " + DirectionArrow + " · УКРОЙТЕСЬ ИЛИ УДЕРЖИВАЙТЕ ПРОБЕЛ";
            }

            return string.Empty;
        }
    }

    public event Action<bool> WarningStateChanged;
    public event Action<bool> WindActiveStateChanged;

    private enum CyclePhase { Waiting, Warning, Gust }
    private const int MaxPhaseTransitionsPerFrame = 8;
    private CyclePhase _phase;
    private float _timeRemaining;
    private float _plannedGustDuration;
    private bool _isRunning;
    private Vector2 _currentWindDirection = Vector2.right;

    private void Awake()
    {
        ResetCycle();
    }

    private void Update()
    {
        if (!_isRunning) return;

        float timeToProcess = Time.deltaTime;
        for (int i = 0; i < MaxPhaseTransitionsPerFrame; i++)
        {
            if (_timeRemaining > timeToProcess)
            {
                _timeRemaining -= timeToProcess;
                return;
            }

            timeToProcess -= _timeRemaining;
            AdvancePhase();
            if (timeToProcess <= 0f) return;
        }
    }

    public void SetRunning(bool isRunning)
    {
        if (_isRunning == isRunning) return;
        _isRunning = isRunning;
        ResetCycle();
    }

    public void ResetCycle()
    {
        _phase = CyclePhase.Waiting;
        _timeRemaining = GetNextWaitDuration();
        _plannedGustDuration = 0f;
        CurrentGustStrength = 0f;
        _currentWindDirection = GetBaseDirection();
        SetWarning(false);
        SetWindActive(false);
    }

    private void AdvancePhase()
    {
        switch (_phase)
        {
            case CyclePhase.Waiting:
                PrepareNextGust();
                _phase = CyclePhase.Warning;
                _timeRemaining = Mathf.Max(1f, warningDuration);
                SetWarning(true);
                break;

            case CyclePhase.Warning:
                _phase = CyclePhase.Gust;
                _timeRemaining = _plannedGustDuration;
                SetWarning(false);
                SetWindActive(true);
                break;

            case CyclePhase.Gust:
                _phase = CyclePhase.Waiting;
                SetWindActive(false);
                CurrentGustStrength = 0f;
                _currentWindDirection = GetBaseDirection();
                _timeRemaining = GetNextWaitDuration();
                break;
        }
    }

    private void PrepareNextGust()
    {
        CurrentGustStrength = UnityEngine.Random.Range(minimumGustStrength, maximumGustStrength);
        _plannedGustDuration = Mathf.Max(
            0.6f,
            gustDuration + UnityEngine.Random.Range(-gustDurationVariation, gustDurationVariation));

        Vector2 baseDirection = GetBaseDirection();
        float angle = randomizeWindDirection
            ? UnityEngine.Random.Range(-directionVariationDegrees, directionVariationDegrees)
            : 0f;
        _currentWindDirection =
            ((Vector2)(Quaternion.Euler(0f, 0f, angle) * baseDirection)).normalized;
    }

    private float GetNextWaitDuration()
    {
        return Mathf.Max(
            6f,
            intervalBetweenGusts + UnityEngine.Random.Range(-intervalVariation, intervalVariation));
    }

    private Vector2 GetBaseDirection()
    {
        return windDirection.sqrMagnitude > 0.0001f
            ? windDirection.normalized
            : Vector2.right;
    }

    private void SetWarning(bool value)
    {
        if (IsWarning == value) return;
        IsWarning = value;
        WarningStateChanged?.Invoke(IsWarning);
    }

    private void SetWindActive(bool value)
    {
        if (IsWindActive == value) return;
        IsWindActive = value;
        WindActiveStateChanged?.Invoke(IsWindActive);
    }

    private void OnValidate()
    {
        intervalBetweenGusts = Mathf.Max(6f, intervalBetweenGusts);
        intervalVariation = Mathf.Clamp(intervalVariation, 0f, intervalBetweenGusts - 6f);
        warningDuration = Mathf.Max(1f, warningDuration);
        gustDuration = Mathf.Max(0.5f, gustDuration);
        gustDurationVariation = Mathf.Clamp(gustDurationVariation, 0f, gustDuration - 0.5f);
        minimumGustStrength = Mathf.Max(0.1f, minimumGustStrength);
        maximumGustStrength = Mathf.Max(minimumGustStrength, maximumGustStrength);
        strongGustThreshold = Mathf.Clamp(strongGustThreshold, minimumGustStrength, maximumGustStrength);
        directionVariationDegrees = Mathf.Clamp(directionVariationDegrees, 0f, 180f);
        if (windDirection.sqrMagnitude < 0.0001f)
            windDirection = Vector2.right;
        else
            windDirection.Normalize();
    }
}
