using System;
using UnityEngine;

/// <summary>
/// Runs the repeating warning and wind-gust cycle.
/// Start the cycle explicitly with SetRunning(true), for example when gameplay begins.
/// </summary>
[DisallowMultipleComponent]
public sealed class WindController : MonoBehaviour
{
    [Header("Wind cycle")]
    [SerializeField, Min(0f)] private float intervalBetweenGusts = 12f;
    [SerializeField, Min(0f)] private float warningDuration = 1.2f;
    [SerializeField, Min(0f)] private float gustDuration = 2f;

    public bool IsWarning { get; private set; }
    public bool IsWindActive { get; private set; }

    /// <summary>Invoked whenever the warning state changes.</summary>
    public event Action<bool> WarningStateChanged;

    /// <summary>Invoked whenever the active wind state changes.</summary>
    public event Action<bool> WindActiveStateChanged;

    private enum CyclePhase
    {
        Waiting,
        Warning,
        Gust
    }

    private const int MaxPhaseTransitionsPerFrame = 8;

    private CyclePhase _phase;
    private float _timeRemaining;
    private bool _isRunning;

    private void Awake()
    {
        ResetCycle();
    }

    private void Update()
    {
        if (!_isRunning)
        {
            return;
        }

        // Carry unused frame time over to the next phase to keep the cycle accurate.
        // The transition limit also protects against zero-length Inspector values.
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

            if (timeToProcess <= 0f)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Starts or stops the cycle. Stopping clears both states and resets the wait
    /// until the next gust; starting then begins a fresh cycle.
    /// </summary>
    public void SetRunning(bool isRunning)
    {
        if (_isRunning == isRunning)
        {
            return;
        }

        _isRunning = isRunning;

        if (!_isRunning)
        {
            ResetCycle();
        }
    }

    /// <summary>
    /// Resets the cycle to the initial wait without changing whether it is running.
    /// </summary>
    public void ResetCycle()
    {
        _phase = CyclePhase.Waiting;
        _timeRemaining = Mathf.Max(0f, intervalBetweenGusts);

        SetWarning(false);
        SetWindActive(false);
    }

    private void AdvancePhase()
    {
        switch (_phase)
        {
            case CyclePhase.Waiting:
                _phase = CyclePhase.Warning;
                _timeRemaining = Mathf.Max(0f, warningDuration);
                SetWarning(true);
                break;

            case CyclePhase.Warning:
                _phase = CyclePhase.Gust;
                _timeRemaining = Mathf.Max(0f, gustDuration);
                SetWarning(false);
                SetWindActive(true);
                break;

            case CyclePhase.Gust:
                _phase = CyclePhase.Waiting;
                _timeRemaining = Mathf.Max(0f, intervalBetweenGusts);
                SetWindActive(false);
                break;
        }
    }

    private void SetWarning(bool isWarning)
    {
        if (IsWarning == isWarning)
        {
            return;
        }

        IsWarning = isWarning;
        WarningStateChanged?.Invoke(IsWarning);
    }

    private void SetWindActive(bool isWindActive)
    {
        if (IsWindActive == isWindActive)
        {
            return;
        }

        IsWindActive = isWindActive;
        WindActiveStateChanged?.Invoke(IsWindActive);
    }
}
