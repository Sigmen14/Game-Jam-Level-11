using System;
using UnityEngine;

/// <summary>
/// Tracks the player's fire reserve and applies its time-based drain.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlameMeter : MonoBehaviour
{
    [Header("Fire reserve")]
    [SerializeField, Min(0f)] private float maximumValue = 100f;
    [SerializeField, Min(0f)] private float initialValue = 75f;

    [Header("Drain rates (per second)")]
    [SerializeField, Min(0f)] private float normalDrainPerSecond = 0.35f;
    [SerializeField, Min(0f)] private float windDrainWithoutProtectionPerSecond = 8f;
    [SerializeField, Min(0f)] private float windDrainWithProtectionPerSecond = 1.5f;

    /// <summary>Current fire reserve, clamped between zero and the configured maximum.</summary>
    public float CurrentValue { get; private set; }

    /// <summary>Current fire reserve as a value between zero and one.</summary>
    public float NormalizedValue =>
        maximumValue > 0f ? Mathf.Clamp01(CurrentValue / maximumValue) : 0f;

    /// <summary>Raised whenever the current fire reserve changes.</summary>
    public event Action<float> ValueChanged;

    private void Awake()
    {
        ResetMeter();
    }

    /// <summary>
    /// Applies the appropriate fire drain for the elapsed time.
    /// Wind drain replaces normal drain while wind is active.
    /// </summary>
    public void Tick(float deltaTime, bool windActive, bool protectedFromWind)
    {
        if (deltaTime <= 0f || CurrentValue <= 0f)
        {
            return;
        }

        float drainRate = normalDrainPerSecond;

        if (windActive)
        {
            drainRate = protectedFromWind
                ? windDrainWithProtectionPerSecond
                : windDrainWithoutProtectionPerSecond;
        }

        SetValue(CurrentValue - drainRate * deltaTime);
    }

    /// <summary>Adds fuel to the reserve without exceeding its maximum.</summary>
    public void AddFuel(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetValue(CurrentValue + amount);
    }

    /// <summary>Restores the configured starting reserve.</summary>
    public void ResetMeter()
    {
        SetValue(Mathf.Clamp(initialValue, 0f, maximumValue));
    }

    private void SetValue(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, maximumValue);

        if (Mathf.Approximately(CurrentValue, clampedValue))
        {
            return;
        }

        CurrentValue = clampedValue;
        ValueChanged?.Invoke(CurrentValue);
    }

    private void OnValidate()
    {
        maximumValue = Mathf.Max(0f, maximumValue);
        initialValue = Mathf.Clamp(initialValue, 0f, maximumValue);
        normalDrainPerSecond = Mathf.Max(0f, normalDrainPerSecond);
        windDrainWithoutProtectionPerSecond = Mathf.Max(0f, windDrainWithoutProtectionPerSecond);
        windDrainWithProtectionPerSecond = Mathf.Max(0f, windDrainWithProtectionPerSecond);
    }
}
