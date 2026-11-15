using System;
using UnityEngine;

/// <summary>
/// Small, original synthesized soundtrack and gameplay sound effects.
/// Audio is generated once at startup, so the project needs no external audio packages.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AudioManager : MonoBehaviour
{
    private const int SampleRate = 22050;
    private static AudioManager instance;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.72f;
    [SerializeField] private AudioSource musicSource;

    private AudioSource effectsSource;
    private AudioClip musicClip;
    private AudioClip fuelPickupClip;
    private AudioClip livingSparksClip;
    private AudioClip beaconClip;
    private AudioClip protectOnClip;
    private AudioClip protectOffClip;
    private AudioClip windWarningClip;
    private AudioClip windGustClip;
    private AudioClip uiClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;
        effectsSource.volume = effectsVolume;

        musicClip = CreateMusicLoop();
        fuelPickupClip = CreateFuelPickup();
        livingSparksClip = CreateLivingSparks();
        beaconClip = CreateBeacon();
        protectOnClip = CreateProtect(true);
        protectOffClip = CreateProtect(false);
        windWarningClip = CreateWindWarning();
        windGustClip = CreateWindGust();
        uiClip = CreateUiConfirm();

        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
        musicSource.pitch = 1f;
        musicSource.Play();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void PlayFuelPickup() { Play(instance != null ? instance.fuelPickupClip : null, 0.9f); }
    public static void PlayLivingSparks() { Play(instance != null ? instance.livingSparksClip : null, 1f); }
    public static void PlayBeaconActivation() { Play(instance != null ? instance.beaconClip : null, 0.9f); }
    public static void PlayProtection(bool enabled) { Play(instance != null ? (enabled ? instance.protectOnClip : instance.protectOffClip) : null, 0.65f); }
    public static void PlayWindWarning() { Play(instance != null ? instance.windWarningClip : null, 0.75f); }
    public static void PlayWindGust() { Play(instance != null ? instance.windGustClip : null, 0.8f); }
    public static void PlayUiConfirm() { Play(instance != null ? instance.uiClip : null, 0.6f); }

    private static void Play(AudioClip clip, float scale)
    {
        if (instance == null || instance.effectsSource == null || clip == null)
            return;

        instance.effectsSource.PlayOneShot(clip, instance.effectsVolume * scale);
    }

    private static AudioClip CreateMusicLoop()
    {
        const float duration = 32f;
        int count = Mathf.RoundToInt(duration * SampleRate);
        float[] data = new float[count];
        int[][] chords =
        {
            new[] { 57, 60, 64, 71 }, // Am(add9)
            new[] { 53, 57, 60, 64 }, // Fmaj7
            new[] { 48, 52, 55, 62 }, // C(add9)
            new[] { 55, 57, 62, 67 }  // G(sus2)
        };
        int[] bassNotes = { 45, 41, 36, 43 };
        int[] arpeggio = { 0, 1, 2, 3, 2, 1, 2, 1 };

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            int chord = Mathf.Min(3, (int)(t / 8f));
            float local = t - chord * 8f;
            float padEnvelope = Mathf.SmoothStep(0f, 0.75f, local) *
                                (1f - Mathf.SmoothStep(7.65f, 8f, local));

            float pad = 0f;
            for (int n = 0; n < 4; n++)
            {
                float frequency = MidiToFrequency(chords[chord][n]);
                float phase = 2f * Mathf.PI * frequency * t;
                pad += Mathf.Sin(phase) * 0.72f + Mathf.Sin(phase * 2.001f) * 0.10f;
            }

            float bassFrequency = MidiToFrequency(bassNotes[chord]);
            float bass = Mathf.Sin(2f * Mathf.PI * bassFrequency * t) * 0.8f;

            int step = Mathf.Min(7, (int)(local / 1f));
            float sincePluck = local - step;
            float pluckEnvelope = Mathf.Exp(-4.2f * sincePluck) *
                                  Mathf.Clamp01(1f - sincePluck / 0.96f);
            float pluckFrequency = MidiToFrequency(chords[chord][arpeggio[step]] + 12);
            float pluckPhase = 2f * Mathf.PI * pluckFrequency * t;
            float pluck = (Mathf.Sin(pluckPhase) +
                           Mathf.Sin(pluckPhase * 2.01f) * 0.24f) * pluckEnvelope;

            float edgeFade = Mathf.SmoothStep(0f, 0.22f, t) *
                             (1f - Mathf.SmoothStep(duration - 0.22f, duration, t));
            data[i] = (pad * 0.026f * padEnvelope +
                       bass * 0.016f +
                       pluck * 0.048f) * edgeFade;
        }

        return MakeClip("LastEmber_Original_Ambient_Loop", data);
    }

    private static AudioClip CreateFuelPickup()
    {
        return MakeClip("SFX_Fuel_Pickup", 0.56f, t =>
            Bell(t, 0f, 880f, 0.38f, 5.5f) +
            Bell(t, 0.095f, 1174.66f, 0.34f, 6.2f) +
            Bell(t, 0.18f, 1760f, 0.15f, 8f));
    }

    private static AudioClip CreateLivingSparks()
    {
        return MakeClip("SFX_Living_Sparks_Collected", 0.95f, t =>
            Bell(t, 0f, 659.25f, 0.28f, 3.8f) +
            Bell(t, 0.12f, 880f, 0.27f, 4.2f) +
            Bell(t, 0.25f, 1318.51f, 0.24f, 4.8f) +
            Bell(t, 0.40f, 1760f, 0.13f, 5.5f));
    }

    private static AudioClip CreateBeacon()
    {
        return MakeClip("SFX_Beacon_Lit", 1.25f, t =>
            Bell(t, 0f, 523.25f, 0.34f, 3.0f) +
            Bell(t, 0.16f, 659.25f, 0.32f, 3.5f) +
            Bell(t, 0.32f, 783.99f, 0.31f, 4.0f) +
            Bell(t, 0.48f, 1046.5f, 0.26f, 4.5f));
    }

    private static AudioClip CreateProtect(bool enabled)
    {
        return MakeClip(enabled ? "SFX_Protection_On" : "SFX_Protection_Off", 0.36f, t =>
        {
            float progress = Mathf.Clamp01(t / 0.36f);
            float frequency = enabled
                ? Mathf.Lerp(240f, 620f, progress)
                : Mathf.Lerp(620f, 210f, progress);
            float envelope = enabled
                ? Mathf.Sin(progress * Mathf.PI) * 0.75f
                : Mathf.Sin(progress * Mathf.PI) * 0.52f;
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            float noise = Noise(t, enabled ? 3.7f : 7.2f) *
                          Mathf.Sin(progress * Mathf.PI) * 0.11f;
            return tone * 0.20f + noise;
        });
    }

    private static AudioClip CreateWindWarning()
    {
        return MakeClip("SFX_Grey_Wind_Warning", 0.8f, t =>
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI);
            float low = Mathf.Sin(2f * Mathf.PI * (155f + 90f * t) * t) * 0.12f;
            return (Noise(t, 11.3f) * 0.17f + low) * envelope;
        });
    }

    private static AudioClip CreateWindGust()
    {
        return MakeClip("SFX_Grey_Wind_Gust", 1.05f, t =>
        {
            float progress = Mathf.Clamp01(t / 1.05f);
            float envelope = Mathf.Sin(progress * Mathf.PI) * (0.5f + 0.5f * progress);
            float low = Mathf.Sin(2f * Mathf.PI * (72f + progress * 48f) * t) * 0.19f;
            return (Noise(t, 19.1f) * 0.24f + low) * envelope;
        });
    }

    private static AudioClip CreateUiConfirm()
    {
        return MakeClip("SFX_UI_Confirm", 0.22f, t =>
            Bell(t, 0f, 660f, 0.13f, 12f) +
            Bell(t, 0.045f, 990f, 0.10f, 14f));
    }

    private static float Bell(float t, float start, float frequency, float amplitude, float decay)
    {
        float elapsed = t - start;
        if (elapsed < 0f)
            return 0f;

        float envelope = Mathf.Exp(-decay * elapsed);
        float phase = 2f * Mathf.PI * frequency * elapsed;
        return (Mathf.Sin(phase) + Mathf.Sin(phase * 2.76f) * 0.16f) * amplitude * envelope;
    }

    private static float Noise(float t, float seed)
    {
        float value = Mathf.Sin((t * SampleRate + 1f) * 12.9898f + seed * 78.233f) *
                      Mathf.Sin((t * SampleRate + 1f) * 4.1414f + seed * 19.19f);
        return value * 0.5f;
    }

    private static float MidiToFrequency(int note)
    {
        return 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }

    private static AudioClip MakeClip(string clipName, float duration, Func<float, float> sample)
    {
        int count = Mathf.RoundToInt(duration * SampleRate);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f);
        return MakeClip(clipName, data);
    }

    private static AudioClip MakeClip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        effectsVolume = Mathf.Clamp01(effectsVolume);
    }
}
