using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the saved master, music and SFX levels to the active audio listener
/// and pre-placed category audio sources. UI and sources are assigned in the Editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioSettingsController : MonoBehaviour
{
    private const string MasterPreferenceKey = "LastFlame.MasterVolume";
    private const string SfxPreferenceKey = "LastFlame.SFXVolume";
    private const string MusicPreferenceKey = "LastFlame.MusicVolume";

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

    [Header("Value labels")]
    [SerializeField] private Text masterValueText;
    [SerializeField] private Text sfxValueText;
    [SerializeField] private Text musicValueText;

    [Header("Pre-placed audio sources")]
    [SerializeField] private AudioSource[] sfxSources;
    [SerializeField] private AudioSource[] musicSources;

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.65f;
    [SerializeField] private bool rememberSettings = true;

    private float masterVolume;
    private float sfxVolume;
    private float musicVolume;

    private void Awake()
    {
        masterVolume = LoadVolume(MasterPreferenceKey, defaultMasterVolume);
        sfxVolume = LoadVolume(SfxPreferenceKey, defaultSfxVolume);
        musicVolume = LoadVolume(MusicPreferenceKey, defaultMusicVolume);

        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(masterVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVolume);

        ApplyAllVolumes();
        UpdateValueLabels();
    }

    private void Start()
    {
        // Music sources already exist in the scene and are never created at runtime.
        if (musicSources == null)
            return;

        for (int i = 0; i < musicSources.Length; i++)
        {
            AudioSource source = musicSources[i];
            if (source != null && source.clip != null && !source.isPlaying)
                source.Play();
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyAllVolumes();
        UpdateValueLabels();
        StoreVolume(MasterPreferenceKey, masterVolume);
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplySfxVolume();
        UpdateValueLabels();
        StoreVolume(SfxPreferenceKey, sfxVolume);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
        UpdateValueLabels();
        StoreVolume(MusicPreferenceKey, musicVolume);
    }

    private float LoadVolume(string key, float defaultValue)
    {
        float value = rememberSettings ? PlayerPrefs.GetFloat(key, defaultValue) : defaultValue;
        return Mathf.Clamp01(value);
    }

    private void StoreVolume(string key, float value)
    {
        if (rememberSettings)
            PlayerPrefs.SetFloat(key, value);
    }

    private void ApplyAllVolumes()
    {
        // Listener volume is global, so the master slider also affects sources
        // that are added to gameplay later and are not in either category array.
        AudioListener.volume = masterVolume;
        ApplySfxVolume();
        ApplyMusicVolume();
    }

    private void ApplySfxVolume()
    {
        if (sfxSources == null)
            return;

        for (int i = 0; i < sfxSources.Length; i++)
        {
            if (sfxSources[i] != null)
                sfxSources[i].volume = sfxVolume;
        }
    }

    private void ApplyMusicVolume()
    {
        if (musicSources == null)
            return;

        for (int i = 0; i < musicSources.Length; i++)
        {
            if (musicSources[i] != null)
                musicSources[i].volume = musicVolume;
        }
    }

    private void UpdateValueLabels()
    {
        SetPercentage(masterValueText, masterVolume);
        SetPercentage(sfxValueText, sfxVolume);
        SetPercentage(musicValueText, musicVolume);
    }

    private static void SetPercentage(Text label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            SaveSettings();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SaveSettings();
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (rememberSettings)
            PlayerPrefs.Save();
    }

    private void OnValidate()
    {
        defaultMasterVolume = Mathf.Clamp01(defaultMasterVolume);
        defaultSfxVolume = Mathf.Clamp01(defaultSfxVolume);
        defaultMusicVolume = Mathf.Clamp01(defaultMusicVolume);
    }
}
