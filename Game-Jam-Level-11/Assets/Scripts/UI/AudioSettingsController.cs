using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the two settings sliders to pre-placed audio sources.
/// Sources and UI widgets are assigned by the prefab/scene authoring workflow.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioSettingsController : MonoBehaviour
{
    private const string SfxPreferenceKey = "LastFlame.SFXVolume";
    private const string MusicPreferenceKey = "LastFlame.MusicVolume";

    [Header("Sliders")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

    [Header("Value labels")]
    [SerializeField] private Text sfxValueText;
    [SerializeField] private Text musicValueText;

    [Header("Pre-placed audio sources")]
    [SerializeField] private AudioSource[] sfxSources;
    [SerializeField] private AudioSource[] musicSources;

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.65f;
    [SerializeField] private bool rememberSettings = true;

    private float _sfxVolume;
    private float _musicVolume;

    private void Awake()
    {
        _sfxVolume = rememberSettings
            ? PlayerPrefs.GetFloat(SfxPreferenceKey, defaultSfxVolume)
            : defaultSfxVolume;
        _musicVolume = rememberSettings
            ? PlayerPrefs.GetFloat(MusicPreferenceKey, defaultMusicVolume)
            : defaultMusicVolume;

        _sfxVolume = Mathf.Clamp01(_sfxVolume);
        _musicVolume = Mathf.Clamp01(_musicVolume);

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(_sfxVolume);
        }
        else
        {
            Debug.LogError("AudioSettingsController: assign the SFX slider in the Inspector.", this);
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(_musicVolume);
        }
        else
        {
            Debug.LogError("AudioSettingsController: assign the Music slider in the Inspector.", this);
        }

        ApplySfxVolume();
        ApplyMusicVolume();
        UpdateValueLabels();
    }

    private void Start()
    {
        // If a music clip is assigned in the Inspector, it starts behind the menu.
        // The source already exists in the scene; no audio objects are created here.
        if (musicSources == null)
        {
            return;
        }

        for (int i = 0; i < musicSources.Length; i++)
        {
            AudioSource source = musicSources[i];
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        ApplySfxVolume();
        UpdateValueLabels();

        if (rememberSettings)
        {
            PlayerPrefs.SetFloat(SfxPreferenceKey, _sfxVolume);
        }
    }

    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
        UpdateValueLabels();

        if (rememberSettings)
        {
            PlayerPrefs.SetFloat(MusicPreferenceKey, _musicVolume);
        }
    }

    private void ApplySfxVolume()
    {
        if (sfxSources == null)
        {
            return;
        }

        for (int i = 0; i < sfxSources.Length; i++)
        {
            if (sfxSources[i] != null)
            {
                sfxSources[i].volume = _sfxVolume;
            }
        }
    }

    private void ApplyMusicVolume()
    {
        if (musicSources == null)
        {
            return;
        }

        for (int i = 0; i < musicSources.Length; i++)
        {
            if (musicSources[i] != null)
            {
                musicSources[i].volume = _musicVolume;
            }
        }
    }

    private void UpdateValueLabels()
    {
        if (sfxValueText != null)
        {
            sfxValueText.text = Mathf.RoundToInt(_sfxVolume * 100f) + "%";
        }

        if (musicValueText != null)
        {
            musicValueText.text = Mathf.RoundToInt(_musicVolume * 100f) + "%";
        }
    }

    private void OnApplicationQuit()
    {
        if (rememberSettings)
        {
            PlayerPrefs.Save();
        }
    }

    private void OnValidate()
    {
        defaultSfxVolume = Mathf.Clamp01(defaultSfxVolume);
        defaultMusicVolume = Mathf.Clamp01(defaultMusicVolume);
    }
}
