using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the one-time story sequence over the existing main-menu Canvas.
/// All visual objects and audio sources are authored by StoryPrologueSetup.
/// Runtime code only animates and toggles those pre-authored references.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologueController : MonoBehaviour
{
    public const string ViewedKey = "LastEmber.PrologueViewed.v1";

    [Header("Presentation groups")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private CanvasGroup worldGroup;
    [SerializeField] private CanvasGroup flameGroup;
    [SerializeField] private CanvasGroup beaconGroup;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private CanvasGroup storyGroup;
    [SerializeField] private CanvasGroup finalGroup;

    [Header("Story text")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text storyText;
    [SerializeField] private Text finalText;
    [SerializeField] private Text continueHint;

    [Header("Light and particles")]
    [SerializeField] private Image flameGlow;
    [SerializeField] private Image flameCore;
    [SerializeField] private Image beaconGlow;
    [SerializeField] private Image[] ashParticles;
    [SerializeField] private Image[] emberParticles;

    [Header("Controls and ambience")]
    [SerializeField] private Button skipButton;
    [SerializeField] private AudioSource windAudio;
    [SerializeField] private AudioSource fireAudio;

    [Header("Timing")]
    [SerializeField, Min(12f)] private float totalDuration = 21.4f;

    private static readonly string FirstStoryLine =
        "Когда-то Цепь огней соединяла мир. Древние маяки согревали поселения и отгоняли тьму.";
    private static readonly string SecondStoryLine =
        "Но пришёл Серый Ветер. Он погасил почти все огни: дома опустели, улицы стихли, костры стали пеплом.";
    private static readonly string ThirdStoryLine =
        "Последний Смотритель сберёг одну живую искру. Чтобы вновь зажечь древний маяк, пламя должно продержаться 180 секунд — пока Серый Ветер пытается его погасить.";

    private Vector2[] _ashOrigins;
    private Vector2[] _emberOrigins;
    private Action _completed;
    private string _activeStoryLine;
    private float _elapsed;
    private bool _isPlaying;

    private void Awake()
    {
        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);

        _ashOrigins = CaptureParticleOrigins(ashParticles);
        _emberOrigins = CaptureParticleOrigins(emberParticles);

        SetOverlayState(false);
        SetGroupAlpha(worldGroup, 0f);
        SetGroupAlpha(flameGroup, 0f);
        SetGroupAlpha(beaconGroup, 0f);
        SetGroupAlpha(titleGroup, 0f);
        SetGroupAlpha(storyGroup, 0f);
        SetGroupAlpha(finalGroup, 0f);
        SetTextAlpha(continueHint, 0f);
    }

    /// <summary>
    /// Starts the prologue only when it has not been completed or skipped before.
    /// Returns false when the existing menu should be shown immediately.
    /// </summary>
    public bool TryPlay(Action onCompleted)
    {
        if (PlayerPrefs.GetInt(ViewedKey, 0) != 0)
        {
            SetOverlayState(false);
            return false;
        }

        _completed = onCompleted;
        _elapsed = 0f;
        _activeStoryLine = null;
        _isPlaying = true;

        if (titleText != null)
            titleText.text = "Когда погас последний свет";

        if (storyText != null)
            storyText.text = FirstStoryLine;

        if (finalText != null)
            finalText.text = "Пока горит хотя бы одна искра — надежда не умерла";

        SetOverlayState(true);
        UpdateTimeline();

        PlayAmbient(windAudio);
        PlayAmbient(fireAudio);
        return true;
    }

    private void Update()
    {
        if (!_isPlaying)
            return;

        // Project Settings uses Both input handling, so legacy polling is available.
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            Skip();
            return;
        }

        // Finish the story on its last frame and wait for an explicit player action.
        _elapsed += Time.unscaledDeltaTime;
        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        const float outro = 1f;

        SetGroupAlpha(worldGroup, SmoothRange(_elapsed, 1.6f, 2.7f) * outro);
        SetGroupAlpha(flameGroup, SmoothRange(_elapsed, 7.1f, 1.5f) * outro);
        SetGroupAlpha(beaconGroup, SmoothRange(_elapsed, 12.2f, 1.8f) * outro);

        SetGroupAlpha(titleGroup, FadeWindow(_elapsed, 3.25f, 7.35f, 0.55f) * outro);

        string nextLine;
        float storyAlpha;
        if (_elapsed < 8.05f)
        {
            nextLine = FirstStoryLine;
            storyAlpha = FadeWindow(_elapsed, 4.15f, 7.95f, 0.48f);
        }
        else if (_elapsed < 12.05f)
        {
            nextLine = SecondStoryLine;
            storyAlpha = FadeWindow(_elapsed, 8.05f, 11.95f, 0.48f);
        }
        else
        {
            nextLine = ThirdStoryLine;
            storyAlpha = FadeWindow(_elapsed, 12.05f, 16.45f, 0.55f);
        }

        if (_activeStoryLine != nextLine)
        {
            _activeStoryLine = nextLine;
            if (storyText != null)
                storyText.text = nextLine;
        }

        SetGroupAlpha(storyGroup, storyAlpha * outro);
        SetGroupAlpha(finalGroup, SmoothRange(_elapsed, 16.55f, 0.7f));
        SetTextAlpha(continueHint, SmoothRange(_elapsed, totalDuration - 2.0f, 0.55f));

        UpdateParticles();
        UpdateLights();
        UpdateAudio(outro);
    }

    private void UpdateParticles()
    {
        if (ashParticles != null)
        {
            for (int i = 0; i < ashParticles.Length; i++)
            {
                Image particle = ashParticles[i];
                if (particle == null)
                    continue;

                RectTransform rect = particle.rectTransform;
                Vector2 origin = _ashOrigins != null && i < _ashOrigins.Length
                    ? _ashOrigins[i]
                    : Vector2.zero;

                float speed = 18f + (i % 5) * 6f;
                float x = Mathf.Repeat(origin.x + 980f - _elapsed * speed, 1960f) - 980f;
                float y = origin.y + Mathf.Sin(_elapsed * 1.25f + i * 1.7f) * 12f;
                rect.anchoredPosition = new Vector2(x, y);
            }
        }

        if (emberParticles != null)
        {
            for (int i = 0; i < emberParticles.Length; i++)
            {
                Image particle = emberParticles[i];
                if (particle == null)
                    continue;

                RectTransform rect = particle.rectTransform;
                Vector2 origin = _emberOrigins != null && i < _emberOrigins.Length
                    ? _emberOrigins[i]
                    : Vector2.zero;

                float speed = 20f + (i % 4) * 8f;
                float y = Mathf.Repeat(origin.y + 500f + _elapsed * speed, 1000f) - 500f;
                float x = origin.x + Mathf.Sin(_elapsed * 1.8f + i * 2.1f) * 15f;
                rect.anchoredPosition = new Vector2(x, y);

                Color color = particle.color;
                float shimmer = 0.55f + 0.45f * Mathf.Sin(_elapsed * 4.1f + i * 1.9f);
                color.a = Mathf.Clamp01(0.34f + shimmer * 0.54f);
                particle.color = color;
            }
        }
    }

    private void UpdateLights()
    {
        float pulse = 0.92f
            + 0.055f * Mathf.Sin(_elapsed * 2.2f)
            + 0.025f * Mathf.Sin(_elapsed * 5.7f + 0.8f);

        if (flameGlow != null)
            flameGlow.rectTransform.localScale = Vector3.one * (1.05f + pulse * 0.16f);

        if (flameCore != null)
            flameCore.rectTransform.localScale = Vector3.one * (0.96f + pulse * 0.08f);

        if (beaconGlow != null)
        {
            float beaconPulse = 0.9f + 0.1f * Mathf.Sin(_elapsed * 1.35f);
            beaconGlow.rectTransform.localScale = Vector3.one * (1.0f + beaconPulse * 0.12f);
        }
    }

    private void UpdateAudio(float outro)
    {
        float gust = 0.86f + 0.1f * Mathf.Sin(_elapsed * 0.65f) + 0.04f * Mathf.Sin(_elapsed * 1.9f);
        if (windAudio != null)
            windAudio.volume = 0.28f * gust * outro;

        if (fireAudio != null)
        {
            float fireIn = SmoothRange(_elapsed, 7.4f, 2.0f);
            float flicker = 0.84f + 0.12f * Mathf.Sin(_elapsed * 3.7f) + 0.04f * Mathf.Sin(_elapsed * 8.2f);
            fireAudio.volume = 0.2f * fireIn * flicker * outro;
        }
    }

    public void Skip()
    {
        if (_isPlaying)
            Complete();
    }

    private void Complete()
    {
        if (!_isPlaying)
            return;

        _isPlaying = false;
        PlayerPrefs.SetInt(ViewedKey, 1);
        PlayerPrefs.Save();

        StopAmbient(windAudio);
        StopAmbient(fireAudio);
        SetOverlayState(false);

        Action callback = _completed;
        _completed = null;
        callback?.Invoke();
    }

    private void SetOverlayState(bool visible)
    {
        if (overlayGroup == null)
            return;

        overlayGroup.alpha = visible ? 1f : 0f;
        overlayGroup.interactable = visible;
        overlayGroup.blocksRaycasts = visible;
    }

    private static void PlayAmbient(AudioSource source)
    {
        if (source == null)
            return;

        source.volume = 0f;
        if (!source.isPlaying)
            source.Play();
    }

    private static void StopAmbient(AudioSource source)
    {
        if (source == null)
            return;

        source.volume = 0f;
        source.Stop();
    }

    private static void SetGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
            group.alpha = Mathf.Clamp01(alpha);
    }

    private static void SetTextAlpha(Text text, float alpha)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private static float FadeWindow(float time, float start, float end, float fadeDuration)
    {
        float fadeIn = SmoothRange(time, start, fadeDuration);
        float fadeOut = 1f - SmoothRange(time, end - fadeDuration, fadeDuration);
        return Mathf.Clamp01(fadeIn * fadeOut);
    }

    private static float SmoothRange(float time, float start, float duration)
    {
        float value = Mathf.Clamp01((time - start) / Mathf.Max(0.001f, duration));
        return value * value * (3f - 2f * value);
    }

    private static Vector2[] CaptureParticleOrigins(Image[] particles)
    {
        if (particles == null)
            return Array.Empty<Vector2>();

        Vector2[] positions = new Vector2[particles.Length];
        for (int i = 0; i < particles.Length; i++)
            positions[i] = particles[i] != null ? particles[i].rectTransform.anchoredPosition : Vector2.zero;

        return positions;
    }

    private void OnDestroy()
    {
        if (skipButton != null)
            skipButton.onClick.RemoveListener(Skip);

        StopAmbient(windAudio);
        StopAmbient(fireAudio);
    }
}
