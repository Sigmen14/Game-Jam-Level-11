using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Owns the flame resource, its passive drain, wind damage and the player's 2D light.
/// All references point to objects already placed in the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlameMeter : MonoBehaviour
{
    [Header("Flame")]
    [SerializeField, Min(1f)] private float maxFlame = 100f;
    [SerializeField, Min(0f)] private float startingFlame = 75f;
    [SerializeField, Min(0f)] private float passiveDrainPerSecond = 0.35f;
    [SerializeField, Min(0f)] private float fuelRestoreAmount = 25f;

    [Header("Wind damage")]
    [SerializeField, Min(0f)] private float windDamagePerSecond = 8f;
    [SerializeField, Min(0f)] private float protectedWindDamagePerSecond = 1.5f;

    [Header("Light response")]
    [SerializeField] private Light2D flameLight;
    [SerializeField, Min(0f)] private float minimumLightRadius = 2.2f;
    [SerializeField, Min(0f)] private float maximumLightRadius = 7.5f;
    [SerializeField, Min(0f)] private float minimumLightIntensity = 0.45f;
    [SerializeField, Min(0f)] private float maximumLightIntensity = 1.6f;

    [Header("Gameplay references")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private WindController windController;

    public event Action<float, float> ValueChanged;
    public event Action FlameDepleted;

    public float Current { get; private set; }
    public float Maximum => maxFlame;
    public float NormalizedValue => maxFlame > 0f ? Current / maxFlame : 0f;
    public bool IsRunning => _isRunning;

    private bool _isRunning;
    private bool _depletionReported;

    private void Awake()
    {
        ResetMeter();
    }

    private void Update()
    {
        if (!_isRunning || Current <= 0f)
        {
            return;
        }

        float drain = passiveDrainPerSecond;

        if (windController != null && windController.IsWindActive &&
            (playerController == null || !playerController.IsInCover))
        {
            bool protectedByPlayer = playerController != null && playerController.IsProtecting;
            drain += protectedByPlayer
                ? protectedWindDamagePerSecond
                : windDamagePerSecond;
        }

        ChangeFlame(-drain * Time.deltaTime);
    }

    public void SetRunning(bool isRunning)
    {
        _isRunning = isRunning;
    }

    public void ResetMeter()
    {
        _isRunning = false;
        _depletionReported = false;
        Current = Mathf.Clamp(startingFlame, 0f, maxFlame);
        UpdateLight();
        ValueChanged?.Invoke(Current, maxFlame);
    }

    public void AddFuel(float amount)
    {
        if (amount <= 0f || Current <= 0f)
        {
            return;
        }

        ChangeFlame(amount);
    }

    public void AddConfiguredFuel()
    {
        AddFuel(fuelRestoreAmount);
    }

    private void ChangeFlame(float delta)
    {
        float previous = Current;
        Current = Mathf.Clamp(Current + delta, 0f, maxFlame);

        if (!Mathf.Approximately(previous, Current))
        {
            UpdateLight();
            ValueChanged?.Invoke(Current, maxFlame);
        }

        if (Current <= 0f && !_depletionReported)
        {
            _depletionReported = true;
            _isRunning = false;
            FlameDepleted?.Invoke();
        }
    }

    private void UpdateLight()
    {
        if (flameLight == null)
        {
            return;
        }

        float normalized = NormalizedValue;
        float radius = Mathf.Lerp(minimumLightRadius, maximumLightRadius, normalized);
        flameLight.pointLightOuterRadius = radius;
        flameLight.pointLightInnerRadius = radius * 0.22f;
        flameLight.intensity = Mathf.Lerp(minimumLightIntensity, maximumLightIntensity, normalized);
    }

    private void OnValidate()
    {
        maxFlame = Mathf.Max(1f, maxFlame);
        startingFlame = Mathf.Clamp(startingFlame, 0f, maxFlame);
        maximumLightRadius = Mathf.Max(minimumLightRadius, maximumLightRadius);
        maximumLightIntensity = Mathf.Max(minimumLightIntensity, maximumLightIntensity);
    }
}
