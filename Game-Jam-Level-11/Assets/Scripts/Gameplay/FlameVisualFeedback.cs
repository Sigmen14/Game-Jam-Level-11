using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Animates the existing player flame visual from the real FlameMeter, wind and cover states.
/// Visual-only: it never changes fuel, damage, movement or match rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlameVisualFeedback : MonoBehaviour
{
    [Header("Gameplay references")]
    [SerializeField] private FlameMeter flameMeter;
    [SerializeField] private WindController windController;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private FuelPickup[] fuelPickups = new FuelPickup[0];

    [Header("Existing visual references")]
    [SerializeField] private SpriteRenderer flameRenderer;
    [SerializeField] private Light2D flameLight;
    [SerializeField] private ParticleSystem sparkParticles;

    [Header("Fuel thresholds")]
    [SerializeField, Range(0f, 1f)] private float highFuelThreshold = 0.65f;
    [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.30f;

    [Header("Flame size")]
    [SerializeField, Min(0.1f)] private float highFuelScale = 1.18f;
    [SerializeField, Min(0.1f)] private float mediumFuelScale = 0.92f;
    [SerializeField, Min(0.1f)] private float lowFuelScale = 0.62f;
    [SerializeField, Min(0.1f)] private float pickupScaleBoost = 1.32f;
    [SerializeField, Min(0f)] private float visualSmoothSpeed = 9f;

    [Header("Flame colours")]
    [SerializeField] private Color highFuelColor = new Color(1f, 0.86f, 0.52f, 1f);
    [SerializeField] private Color mediumFuelColor = new Color(1f, 0.57f, 0.20f, 1f);
    [SerializeField] private Color lowFuelColor = new Color(0.92f, 0.27f, 0.10f, 1f);
    [SerializeField, Range(0.05f, 1f)] private float lowFuelMinimumAlpha = 0.42f;

    [Header("Flicker and sway")]
    [SerializeField, Min(0f)] private float swayAngle = 4f;
    [SerializeField, Min(0f)] private float swayFrequency = 3.2f;
    [SerializeField, Min(0f)] private float highFuelFlicker = 0.035f;
    [SerializeField, Min(0f)] private float lowFuelFlicker = 0.28f;
    [SerializeField, Min(0f)] private float lowFuelJitterAngle = 9f;
    [SerializeField, Min(0f)] private float windJitterAngle = 8f;
    [SerializeField, Min(0.1f)] private float flickerNoiseFrequency = 7f;
    [SerializeField, Min(0f)] private float windNoiseFrequencyMultiplier = 2.2f;

    [Header("Wind and shelter")]
    [SerializeField, Min(0f)] private float maximumWindLeanAngle = 25f;
    [SerializeField, Range(0f, 1f)] private float shelterWindMultiplier = 0.16f;
    [SerializeField, Range(0f, 1f)] private float shelterMotionMultiplier = 0.38f;
    [SerializeField, Min(0f)] private float windSparkInfluence = 1.25f;
    [SerializeField, Min(1f)] private float gustSparkSpeedMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float warningSparkInfluence = 0.55f;
    [SerializeField, Min(0f)] private float warningFlickerBoost = 0.07f;

    [Header("Ambient sparks")]
    [SerializeField, Min(0.05f)] private float highFuelSparkInterval = 0.48f;
    [SerializeField, Min(0.05f)] private float mediumFuelSparkInterval = 1.15f;
    [SerializeField, Min(0.05f)] private float lowFuelSparkInterval = 2.7f;
    [SerializeField, Min(1)] private int highFuelSparkCount = 2;
    [SerializeField, Min(1)] private int mediumFuelSparkCount = 1;
    [SerializeField, Min(1)] private int lowFuelSparkCount = 1;
    [SerializeField, Min(0f)] private float sparkSpeed = 1.15f;
    [SerializeField, Min(0f)] private float sparkSpread = 0.45f;
    [SerializeField, Min(0.001f)] private float sparkSize = 0.055f;
    [SerializeField, Min(0.01f)] private float sparkLifetime = 0.48f;
    [SerializeField] private Color sparkColor = new Color(1f, 0.38f, 0.055f, 1f);
    [SerializeField] private Color sparkGoldColor = new Color(1f, 0.66f, 0.12f, 1f);
    [SerializeField] private Color sparkYellowColor = new Color(1f, 0.92f, 0.42f, 1f);
    [SerializeField] private Color sparkRedColor = new Color(0.95f, 0.13f, 0.018f, 1f);
    [SerializeField] private Color weakSparkColor = new Color(1f, 0.25f, 0.045f, 0.48f);
    [SerializeField, Min(0f)] private float pickupSparkSideSpread = 0.9f;
    [SerializeField, Min(1f)] private float shelterSparkIntervalMultiplier = 1.35f;

    [Header("Fuel pickup burst")]
    [SerializeField, Min(0.05f)] private float pickupFlashDuration = 0.28f;
    [SerializeField, Min(1)] private int pickupSparkCount = 9;
    [SerializeField, Min(0f)] private float pickupSparkSpeed = 2.3f;
    [SerializeField, Min(0f)] private float pickupLightBoost = 0.45f;
    [SerializeField] private Color pickupFlashColor = new Color(1f, 0.97f, 0.78f, 1f);
    [SerializeField] private Color pickupSparkColor = new Color(1f, 0.82f, 0.28f, 1f);

    private readonly HashSet<FuelPickup> subscribedPickups = new HashSet<FuelPickup>();

    private Vector3 baseFlameScale;
    private Color baseFlameColor;
    private float baseLightIntensity = 1f;
    private float pickupFlashRemaining;
    private float sparkTimer;
    private float noiseSeed;

    private void Awake()
    {
        if (flameRenderer != null)
        {
            baseFlameScale = flameRenderer.transform.localScale;
            baseFlameColor = flameRenderer.color;
        }

        if (flameLight != null)
        {
            baseLightIntensity = flameLight.intensity;
        }

        noiseSeed = Mathf.Abs(GetInstanceID() % 10000) * 0.0137f;
    }

    private void OnEnable()
    {
        if (flameMeter != null)
        {
            flameMeter.ValueChanged += HandleFlameValueChanged;
            baseLightIntensity = flameLight != null ? flameLight.intensity : baseLightIntensity;
        }

        subscribedPickups.Clear();
        if (fuelPickups != null)
        {
            for (int i = 0; i < fuelPickups.Length; i++)
            {
                FuelPickup pickup = fuelPickups[i];
                if (pickup != null && subscribedPickups.Add(pickup))
                {
                    pickup.FuelCollected += HandleFuelCollected;
                }
            }
        }

        if (sparkParticles != null &&
            sparkParticles.gameObject.activeInHierarchy && !sparkParticles.isPlaying)
        {
            sparkParticles.Play(true);
        }

        sparkTimer = Random.Range(0.12f, 0.5f);
    }

    private void Update()
    {
        if (flameMeter == null || flameRenderer == null)
        {
            return;
        }

        float fuel = Mathf.Clamp01(flameMeter.NormalizedValue);
        bool windActive = windController != null && windController.IsWindActive;
        bool windWarning = windController != null && windController.IsWarning;
        float windStrength = windController != null ? windController.CurrentGustStrength : 0f;
        bool sheltered = playerController != null && playerController.IsWindSheltered;

        float lowFuelBlend = 1f - Mathf.Clamp01(
            fuel / Mathf.Max(0.01f, lowFuelThreshold));

        float noiseFrequency = flickerNoiseFrequency *
                               (windActive ? windNoiseFrequencyMultiplier :
                                (windWarning ? 1.35f : 1f));
        float noise = Mathf.PerlinNoise(Time.time * noiseFrequency, noiseSeed) * 2f - 1f;
        float flickerStrength = Mathf.Lerp(highFuelFlicker, lowFuelFlicker, 1f - fuel);
        if (windActive)
        {
            float gustFlicker = 0.12f + Mathf.Clamp01((windStrength - 1f) / 0.85f) * 0.07f;
            flickerStrength = Mathf.Max(flickerStrength, gustFlicker);
        }
        else if (windWarning)
        {
            flickerStrength = Mathf.Max(flickerStrength, warningFlickerBoost);
        }

        float flicker = Mathf.Clamp(
            1f - Mathf.Abs(noise) * flickerStrength, 0.38f, 1.08f);

        float calmMultiplier = sheltered ? shelterMotionMultiplier : 1f;
        float sway = Mathf.Sin(Time.time * swayFrequency) * swayAngle * calmMultiplier;
        float jitter = noise *
                       (lowFuelJitterAngle * lowFuelBlend +
                        (windActive ? windJitterAngle : 0f)) *
                       calmMultiplier;

        float windLean = 0f;
        if ((windActive || windWarning) && windController != null)
        {
            Vector3 localWind = transform.InverseTransformDirection(
                new Vector3(windController.WindDirection.x,
                    windController.WindDirection.y, 0f));
            float shelterFactor = sheltered ? shelterWindMultiplier : 1f;
            float visualStrength = windActive ? Mathf.Max(1f, windStrength) : warningSparkInfluence;
            windLean = -Mathf.Clamp(localWind.x, -1f, 1f) *
                       maximumWindLeanAngle * visualStrength * shelterFactor;
        }

        float fuelScale = GetFuelScale(fuel);
        float flash = pickupFlashDuration > 0f
            ? Mathf.Clamp01(pickupFlashRemaining / pickupFlashDuration)
            : 0f;

        Vector3 targetScale = baseFlameScale * fuelScale;
        targetScale.x *= flicker;
        targetScale.y *= Mathf.Lerp(0.96f, 1.04f, (noise + 1f) * 0.5f);
        targetScale *= Mathf.Lerp(1f, pickupScaleBoost, flash);

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, sway + jitter + windLean);
        float smoothing = 1f - Mathf.Exp(-visualSmoothSpeed * Time.deltaTime);
        Transform flameTransform = flameRenderer.transform;
        flameTransform.localScale = Vector3.Lerp(
            flameTransform.localScale, targetScale, smoothing);
        flameTransform.localRotation = Quaternion.Slerp(
            flameTransform.localRotation, targetRotation, smoothing);

        Color targetColor = GetFuelColor(fuel);
        targetColor.a *= Mathf.Lerp(lowFuelMinimumAlpha, 1f, fuel);
        targetColor.r *= flicker;
        targetColor.g *= flicker;
        targetColor = Color.Lerp(targetColor, pickupFlashColor, flash);
        flameRenderer.color = targetColor;

        if (flameLight != null)
        {
            flameLight.intensity = baseLightIntensity * flicker *
                                   (1f + flash * pickupLightBoost);
        }

        pickupFlashRemaining = Mathf.Max(
            0f, pickupFlashRemaining - Time.deltaTime);

        UpdateAmbientSparks(fuel, windActive, sheltered);
    }

    private float GetFuelScale(float fuel)
    {
        if (fuel >= highFuelThreshold)
        {
            return highFuelScale;
        }

        if (fuel >= lowFuelThreshold)
        {
            return mediumFuelScale;
        }

        return lowFuelScale;
    }

    private Color GetFuelColor(float fuel)
    {
        if (fuel >= highFuelThreshold)
        {
            return highFuelColor;
        }

        if (fuel >= lowFuelThreshold)
        {
            return mediumFuelColor;
        }

        return lowFuelColor;
    }

    private void UpdateAmbientSparks(float fuel, bool windActive, bool sheltered)
    {
        if (sparkParticles == null)
        {
            return;
        }

        float interval;
        int count;

        if (fuel >= highFuelThreshold)
        {
            interval = highFuelSparkInterval;
            count = highFuelSparkCount;
        }
        else if (fuel >= lowFuelThreshold)
        {
            interval = mediumFuelSparkInterval;
            count = mediumFuelSparkCount;
        }
        else
        {
            interval = lowFuelSparkInterval;
            count = lowFuelSparkCount;
        }

        if (windActive)
        {
            float strengthBoost = Mathf.Lerp(
                1f, 1.2f,
                Mathf.Clamp01((windController.CurrentGustStrength - 1f) / 0.85f));
            interval *= 0.78f / strengthBoost;
        }
        else if (windController != null && windController.IsWarning)
        {
            // A brief increase in warm sparks gives the player a readable pre-gust cue.
            interval *= 0.72f;
        }

        if (sheltered)
        {
            interval *= shelterSparkIntervalMultiplier;
        }

        sparkTimer -= Time.deltaTime;
        if (sparkTimer > 0f)
        {
            return;
        }

        EmitSparks(count, false, fuel, windActive, sheltered);
        sparkTimer = Mathf.Max(0.05f, interval) * Random.Range(0.8f, 1.2f);
    }

    private void EmitSparks(
        int count, bool pickupBurst, float fuel, bool windActive, bool sheltered)
    {
        if (sparkParticles == null || count <= 0)
        {
            return;
        }

        if (!sparkParticles.isPlaying)
        {
            sparkParticles.Play(true);
        }

        Vector2 wind = Vector2.zero;
        bool windWarning = windController != null && windController.IsWarning;
        float windPower = windActive && windController != null
            ? Mathf.Max(1f, windController.CurrentGustStrength)
            : (windWarning ? warningSparkInfluence : 0f);
        if (windPower > 0f && windController != null)
        {
            wind = windController.WindDirection * windSparkInfluence * windPower;
            if (sheltered) wind *= shelterWindMultiplier;
        }

        Vector2 flameUp = flameRenderer != null
            ? (Vector2)flameRenderer.transform.up
            : (Vector2)transform.up;
        Vector2 flameRight = flameRenderer != null
            ? (Vector2)flameRenderer.transform.right
            : (Vector2)transform.right;

        for (int i = 0; i < count; i++)
        {
            // Keep the launch predominantly upward; use a controlled lateral arc
            // instead of scattering particles in a full circle.
            float sideSpread = pickupBurst
                ? pickupSparkSideSpread
                : sparkSpread * 0.55f;
            float sideVelocity = Random.Range(-sideSpread, sideSpread);
            Vector2 direction = (flameUp * Random.Range(0.88f, 1.18f) +
                                 flameRight * sideVelocity + wind).normalized;

            float speed = pickupBurst
                ? pickupSparkSpeed * Random.Range(0.82f, 1.42f)
                : sparkSpeed * Random.Range(0.78f, 1.28f);

            if (fuel < lowFuelThreshold && !pickupBurst)
            {
                speed *= 0.62f;
            }

            if (windActive && !sheltered)
            {
                speed *= gustSparkSpeedMultiplier * windPower;
            }
            else if (windWarning && !sheltered)
            {
                speed *= 1.12f;
            }

            float sizeVariation = Random.Range(0.62f, 1.38f);
            float lifetimeVariation = Random.Range(0.78f, 1.28f);
            Color particleColor = GetRandomSparkColor(pickupBurst, fuel);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = flameRenderer != null
                    ? flameRenderer.transform.position + (Vector3)(flameUp * 0.075f)
                    : transform.position,
                velocity = direction * speed,
                startColor = particleColor,
                startSize = sparkSize * sizeVariation *
                    (pickupBurst ? 1.12f : (fuel < lowFuelThreshold ? 0.72f : 1f)),
                startLifetime = sparkLifetime * lifetimeVariation *
                    (pickupBurst ? 1.18f : 1f)
            };

            sparkParticles.Emit(emit, 1);
        }
    }

    private Color GetRandomSparkColor(bool pickupBurst, float fuel)
    {
        float roll = Random.value;
        Color selected;

        if (pickupBurst)
        {
            selected = roll < 0.12f ? sparkRedColor
                : roll < 0.34f ? sparkColor
                : roll < 0.72f ? sparkGoldColor
                : sparkYellowColor;

            return Color.Lerp(selected, pickupSparkColor, 0.28f);
        }

        selected = roll < 0.09f ? sparkRedColor
            : roll < 0.43f ? sparkColor
            : roll < 0.78f ? sparkGoldColor
            : sparkYellowColor;

        if (fuel < lowFuelThreshold)
        {
            return Color.Lerp(weakSparkColor, selected, 0.22f);
        }

        return selected;
    }

    private void HandleFuelCollected()
    {
        pickupFlashRemaining = pickupFlashDuration;
        float fuel = flameMeter != null ? flameMeter.NormalizedValue : 1f;
        bool windActive = windController != null && windController.IsWindActive;
        bool sheltered = playerController != null &&
                         (playerController.IsInCover || playerController.IsProtecting);
        EmitSparks(pickupSparkCount, true, fuel, windActive, sheltered);
    }

    private void HandleFlameValueChanged(float current, float maximum)
    {
        if (flameLight != null)
        {
            // FlameMeter updates its base light before invoking ValueChanged.
            baseLightIntensity = flameLight.intensity;
        }
    }

    private void OnDisable()
    {
        if (flameMeter != null)
        {
            flameMeter.ValueChanged -= HandleFlameValueChanged;
        }

        foreach (FuelPickup pickup in subscribedPickups)
        {
            if (pickup != null)
            {
                pickup.FuelCollected -= HandleFuelCollected;
            }
        }

        subscribedPickups.Clear();
        pickupFlashRemaining = 0f;

        // Keep the authored ParticleSystem enabled. Its existing particles are
        // allowed to finish naturally while this component releases its event hooks.
        RestoreBaseVisuals();
    }

    private void RestoreBaseVisuals()
    {
        if (flameRenderer != null)
        {
            flameRenderer.transform.localScale = baseFlameScale;
            flameRenderer.transform.localRotation = Quaternion.identity;
            flameRenderer.color = baseFlameColor;
        }

        if (flameLight != null)
        {
            flameLight.intensity = baseLightIntensity;
        }
    }

    private void OnValidate()
    {
        lowFuelThreshold = Mathf.Clamp01(lowFuelThreshold);
        highFuelThreshold = Mathf.Clamp(highFuelThreshold, lowFuelThreshold, 1f);
        pickupFlashDuration = Mathf.Max(0.05f, pickupFlashDuration);
        shelterSparkIntervalMultiplier = Mathf.Max(1f, shelterSparkIntervalMultiplier);
    }
}
