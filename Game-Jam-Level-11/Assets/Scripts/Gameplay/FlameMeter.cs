using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Owns the flame resource, its passive drain, wind damage and the player's 2D light.
/// Successful fuel pickups also advance the session-long flame growth stage.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlameMeter : MonoBehaviour
{
    public enum GrowthStage
    {
        SmallSpark = 0,
        SteadySpark = 1,
        LastFlame = 2
    }

    [Header("Flame")]
    [SerializeField, Min(1f)] private float maxFlame = 100f;
    [SerializeField, Min(0f)] private float startingFlame = 75f;
    [SerializeField, Min(0f)] private float passiveDrainPerSecond = 0.58f;

    [Header("Wind damage")]
    [SerializeField, Min(0f)] private float windDamagePerSecond = 8f;
    [SerializeField, Min(0f)] private float protectedWindDamagePerSecond = 0.9f;

    [Header("Flame growth")]
    [SerializeField, Min(1)] private int steadySparkPickupThreshold = 2;
    [SerializeField, Min(2)] private int lastFlamePickupThreshold = 4;
    [SerializeField, Range(0.5f, 1f)] private float steadySparkWindDamageMultiplier = 0.9f;
    [SerializeField, Range(0.5f, 1f)] private float lastFlameWindDamageMultiplier = 0.82f;

    [Header("Growth light response")]
    [SerializeField, Min(1f)] private float steadySparkLightRadiusMultiplier = 1.06f;
    [SerializeField, Min(1f)] private float lastFlameLightRadiusMultiplier = 1.12f;
    [SerializeField, Min(1f)] private float steadySparkLightIntensityMultiplier = 1.08f;
    [SerializeField, Min(1f)] private float lastFlameLightIntensityMultiplier = 1.16f;

    [Header("Light response")]
    [SerializeField] private Light2D flameLight;
    [SerializeField, Min(0f)] private float minimumLightRadius = 2.2f;
    [SerializeField, Min(0f)] private float maximumLightRadius = 8.5f;
    [SerializeField, Min(0f)] private float minimumLightIntensity = 0.45f;
    [SerializeField, Min(0f)] private float maximumLightIntensity = 1.6f;

    [Header("Gameplay references")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private WindController windController;

    public event Action<float, float> ValueChanged;
    public event Action FlameDepleted;
    public event Action<GrowthStage> GrowthStageChanged;

    public float Current { get; private set; }
    public float Maximum => maxFlame;
    public float NormalizedValue => maxFlame > 0f ? Current / maxFlame : 0f;
    public bool IsRunning => _isRunning;
    public bool IsInsideBeaconShelter => _beaconShelters.Count > 0;
    public int SuccessfulFuelPickupCount { get; private set; }
    public GrowthStage CurrentGrowthStage { get; private set; } = GrowthStage.SmallSpark;

    /// <summary>Only unprotected gust damage is reduced by flame growth; Space and cover keep their existing rules.</summary>
    public float WindDamageMultiplier => CurrentGrowthStage == GrowthStage.LastFlame
        ? lastFlameWindDamageMultiplier
        : CurrentGrowthStage == GrowthStage.SteadySpark
            ? steadySparkWindDamageMultiplier
            : 1f;

    private bool _isRunning;
    private bool _depletionReported;
    private readonly HashSet<BeaconController> _beaconShelters = new HashSet<BeaconController>();

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
            // Keep the established Space protection value unchanged. Growth only
            // softens the exposed-wind penalty, so the Grey Wind remains dangerous.
            // An active beacon blocks Grey Wind damage while its shelter covers the player.
            // The ordinary passive flame drain above remains unchanged.
            if (!IsInsideBeaconShelter)
            {
                drain += protectedByPlayer
                    ? protectedWindDamagePerSecond
                    : windDamagePerSecond * WindDamageMultiplier;
            }
        }

        ChangeFlame(-drain * Time.deltaTime);
    }

    public void BindGameplaySystems(PlayerController player, WindController wind)
    {
        playerController = player;
        windController = wind;
    }

    public void SetRunning(bool isRunning)
    {
        _isRunning = isRunning;
    }

    /// <summary>
    /// A new meter/session starts as a small spark. GameManager already calls this
    /// during StartGame/Retry, so the session counter resets with the flame resource.
    /// </summary>
    public void ResetMeter()
    {
        GrowthStage previousStage = CurrentGrowthStage;
        SuccessfulFuelPickupCount = 0;
        CurrentGrowthStage = GrowthStage.SmallSpark;
        _isRunning = false;
        _depletionReported = false;
        _beaconShelters.Clear();
        Current = Mathf.Clamp(startingFlame, 0f, maxFlame);
        UpdateLight();
        ValueChanged?.Invoke(Current, maxFlame);

        if (previousStage != CurrentGrowthStage)
            GrowthStageChanged?.Invoke(CurrentGrowthStage);
    }

    /// <summary>Spends fuel only when the full beacon activation cost is available.</summary>
    public bool TryConsumeFuel(float amount)
    {
        if (amount <= 0f || Current < amount || Current <= 0f)
            return false;

        ChangeFlame(-amount);
        return true;
    }

    /// <summary>Tracks active beacon shelters, including overlapping shelter areas.</summary>
    public void SetBeaconShelter(BeaconController beacon, bool isInside)
    {
        if (beacon == null)
            return;

        if (isInside)
            _beaconShelters.Add(beacon);
        else
            _beaconShelters.Remove(beacon);
    }

    public void ApplyDamage(float amount)
    {
        if (amount > 0f && Current > 0f)
            ChangeFlame(-amount);
    }

    public void AddFuel(float amount)
    {
        if (amount <= 0f || Current <= 0f)
        {
            return;
        }

        ChangeFlame(amount);
    }

    /// <summary>
    /// Called by the existing FuelPickup only after AddFuel actually increases Current.
    /// Counting pickups here, rather than fuel reserve, prevents stage regression while
    /// fuel is consumed and ignores touches that cannot restore any fuel.
    /// </summary>
    public void RegisterSuccessfulFuelPickup()
    {
        if (Current <= 0f)
            return;

        GrowthStage previousStage = CurrentGrowthStage;
        SuccessfulFuelPickupCount++;
        CurrentGrowthStage = ResolveGrowthStage(SuccessfulFuelPickupCount);

        UpdateLight();

        if (previousStage != CurrentGrowthStage)
            GrowthStageChanged?.Invoke(CurrentGrowthStage);
    }

    private GrowthStage ResolveGrowthStage(int successfulPickups)
    {
        if (successfulPickups >= lastFlamePickupThreshold)
            return GrowthStage.LastFlame;

        if (successfulPickups >= steadySparkPickupThreshold)
            return GrowthStage.SteadySpark;

        return GrowthStage.SmallSpark;
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
        float intensity = Mathf.Lerp(minimumLightIntensity, maximumLightIntensity, normalized);

        if (CurrentGrowthStage == GrowthStage.SteadySpark)
        {
            radius *= steadySparkLightRadiusMultiplier;
            intensity *= steadySparkLightIntensityMultiplier;
        }
        else if (CurrentGrowthStage == GrowthStage.LastFlame)
        {
            radius *= lastFlameLightRadiusMultiplier;
            intensity *= lastFlameLightIntensityMultiplier;
        }

        flameLight.pointLightOuterRadius = radius;
        flameLight.pointLightInnerRadius = radius * 0.22f;
        flameLight.intensity = intensity;
    }

    private void OnValidate()
    {
        maxFlame = Mathf.Max(1f, maxFlame);
        startingFlame = Mathf.Clamp(startingFlame, 0f, maxFlame);
        steadySparkPickupThreshold = Mathf.Max(1, steadySparkPickupThreshold);
        lastFlamePickupThreshold = Mathf.Max(steadySparkPickupThreshold + 1, lastFlamePickupThreshold);
        steadySparkWindDamageMultiplier = Mathf.Clamp(steadySparkWindDamageMultiplier, 0.5f, 1f);
        lastFlameWindDamageMultiplier = Mathf.Clamp(lastFlameWindDamageMultiplier, 0.5f, steadySparkWindDamageMultiplier);
        steadySparkLightRadiusMultiplier = Mathf.Max(1f, steadySparkLightRadiusMultiplier);
        lastFlameLightRadiusMultiplier = Mathf.Max(steadySparkLightRadiusMultiplier, lastFlameLightRadiusMultiplier);
        steadySparkLightIntensityMultiplier = Mathf.Max(1f, steadySparkLightIntensityMultiplier);
        lastFlameLightIntensityMultiplier = Mathf.Max(steadySparkLightIntensityMultiplier, lastFlameLightIntensityMultiplier);
        maximumLightRadius = Mathf.Max(minimumLightRadius, maximumLightRadius);
        maximumLightIntensity = Mathf.Max(minimumLightIntensity, maximumLightIntensity);
    }
}
