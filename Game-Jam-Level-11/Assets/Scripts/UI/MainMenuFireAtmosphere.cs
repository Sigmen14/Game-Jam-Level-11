using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight, pre-authored UI ember ambience for the existing Last Ember menu.
/// All particle Images are created by the Editor setup tool and never instantiated at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuFireAtmosphere : MonoBehaviour
{
    [Header("Ссылки UI")]
    [SerializeField] private RectTransform atmosphereRoot;
    [SerializeField] private Image hearthGlow;
    [SerializeField] private Image hearthCore;
    [SerializeField] private Image transitionFlash;
    [SerializeField] private Image[] floatingLights = Array.Empty<Image>();
    [SerializeField] private Image[] risingSparks = Array.Empty<Image>();
    [SerializeField] private Image[] brightEmbers = Array.Empty<Image>();

    [Header("Количество частиц")]
    [SerializeField, Range(0, 24)] private int floatingLightCount = 22;
    [SerializeField, Range(0, 14)] private int risingSparkCount = 12;
    [SerializeField, Range(0, 5)] private int brightEmberCount = 4;

    [Header("Настройки атмосферы")]
    [SerializeField, Range(0.1f, 2f)] private float speedMultiplier = 1f;
    [SerializeField, Range(0.1f, 2f)] private float brightnessMultiplier = 1f;
    [SerializeField, Range(0.1f, 2f)] private float glowStrength = 1f;

    [Header("Вспышка при запуске")]
    [SerializeField, Min(0.1f)] private float transitionDuration = 0.72f;
    [SerializeField, Range(0f, 0.4f)] private float flashPeakAlpha = 0.24f;

    private struct ParticleState
    {
        public Image image;
        public RectTransform rect;
        public Vector2 position;
        public Vector2 velocity;
        public Color color;
        public Vector3 baseScale;
        public float baseAlpha;
        public float phase;
        public float age;
        public float lifetime;
    }

    private ParticleState[] _floatingStates = Array.Empty<ParticleState>();
    private ParticleState[] _sparkStates = Array.Empty<ParticleState>();
    private ParticleState[] _emberStates = Array.Empty<ParticleState>();

    private float _halfWidth = 960f;
    private float _halfHeight = 540f;
    private float _elapsed;
    private float _transitionBoost;
    private bool _initialized;
    private bool _atmosphereVisible = true;
    private Coroutine _transitionRoutine;
    private Color _flashColor;
    private Vector3 _flashBaseScale = Vector3.one;

    private void Start()
    {
        Canvas.ForceUpdateCanvases();

        if (atmosphereRoot != null)
        {
            Rect bounds = atmosphereRoot.rect;
            _halfWidth = Mathf.Max(1f, bounds.width * 0.5f);
            _halfHeight = Mathf.Max(1f, bounds.height * 0.5f);
        }
        else
        {
            _halfWidth = Mathf.Max(1f, Screen.width * 0.5f);
            _halfHeight = Mathf.Max(1f, Screen.height * 0.5f);
        }

        _floatingStates = BuildStates(floatingLights, false);
        _sparkStates = BuildStates(risingSparks, true);
        _emberStates = BuildStates(brightEmbers, false);

        _flashColor = new Color(1f, 0.62f, 0.16f, 0f);
        if (transitionFlash != null)
        {
            // UI/Default is Unity's built-in Canvas shader. A custom lit sprite
            // material is not needed for a screen-space Image.
            transitionFlash.material = null;
            transitionFlash.raycastTarget = false;
            transitionFlash.maskable = false;
            _flashBaseScale = transitionFlash.rectTransform.localScale;
            transitionFlash.color = _flashColor;
            transitionFlash.enabled = false;
        }

        if (hearthGlow != null)
        {
            hearthGlow.material = null;
            hearthGlow.raycastTarget = false;
            hearthGlow.maskable = false;
        }

        if (hearthCore != null)
        {
            hearthCore.material = null;
            hearthCore.raycastTarget = false;
            hearthCore.maskable = false;
        }

        ApplyParticleCounts();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || !_atmosphereVisible)
            return;

        float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        _elapsed += deltaTime;

        UpdateFloatingLights(deltaTime);
        UpdateRisingSparks(deltaTime);
        UpdateBrightEmbers(deltaTime);
        UpdateHearthGlow();
    }

    /// <summary>Shows or hides only the decorative layer; menu panels and input stay untouched.</summary>
    public void SetAtmosphereVisible(bool visible)
    {
        _atmosphereVisible = visible;

        if (atmosphereRoot != null && atmosphereRoot.gameObject.activeSelf != visible)
            atmosphereRoot.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Plays a brief warm flash, then invokes the existing menu action.
    /// The flash Image is non-raycastable and does not block the menu.
    /// </summary>
    public void PlayStartFlash(Action onFinished)
    {
        if (transitionFlash == null || !isActiveAndEnabled)
        {
            onFinished?.Invoke();
            return;
        }

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(PlayStartFlashRoutine(onFinished));
    }

    private IEnumerator PlayStartFlashRoutine(Action onFinished)
    {
        float duration = Mathf.Max(0.1f, transitionDuration);
        float elapsed = 0f;

        transitionFlash.enabled = true;

        while (elapsed < duration)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            _transitionBoost = envelope;

            Color flash = _flashColor;
            flash.a = flashPeakAlpha * envelope;
            transitionFlash.color = flash;
            transitionFlash.rectTransform.localScale =
                _flashBaseScale * (0.82f + 0.42f * envelope);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Color finishedFlash = _flashColor;
        finishedFlash.a = 0f;
        transitionFlash.color = finishedFlash;
        transitionFlash.rectTransform.localScale = _flashBaseScale;
        transitionFlash.enabled = false;
        _transitionBoost = 0f;
        _transitionRoutine = null;

        onFinished?.Invoke();
    }

    private ParticleState[] BuildStates(Image[] images, bool upward)
    {
        if (images == null || images.Length == 0)
            return Array.Empty<ParticleState>();

        ParticleState[] states = new ParticleState[images.Length];

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            image.material = null;
            image.raycastTarget = false;
            image.maskable = false;

            ParticleState state = new ParticleState
            {
                image = image,
                rect = image.rectTransform,
                color = image.color,
                baseAlpha = image.color.a,
                baseScale = image.rectTransform.localScale,
                phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            };

            if (upward)
            {
                state.position = new Vector2(
                    UnityEngine.Random.Range(-_halfWidth, _halfWidth),
                    UnityEngine.Random.Range(-_halfHeight, _halfHeight));
                state.velocity = new Vector2(
                    UnityEngine.Random.Range(-9f, 9f),
                    UnityEngine.Random.Range(34f, 66f));
                state.lifetime = UnityEngine.Random.Range(2.5f, 4.6f);
                state.age = UnityEngine.Random.Range(0f, state.lifetime);
            }
            else
            {
                state.position = new Vector2(
                    UnityEngine.Random.Range(-_halfWidth, _halfWidth),
                    UnityEngine.Random.Range(-_halfHeight, _halfHeight));
                state.velocity = UnityEngine.Random.insideUnitCircle
                    * UnityEngine.Random.Range(5f, 15f);
                state.lifetime = UnityEngine.Random.Range(2.6f, 5.8f);
                state.age = UnityEngine.Random.Range(0f, state.lifetime);
            }

            state.rect.anchoredPosition = state.position;
            if (upward)
                state.rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-18f, 18f));

            states[i] = state;
        }

        return states;
    }

    private void UpdateFloatingLights(float deltaTime)
    {
        for (int i = 0; i < _floatingStates.Length; i++)
        {
            ParticleState state = _floatingStates[i];
            if (state.image == null || i >= floatingLightCount || !state.image.gameObject.activeSelf)
                continue;

            Vector2 drift = new Vector2(
                Mathf.Sin(_elapsed * 0.55f + state.phase) * 3.5f,
                Mathf.Cos(_elapsed * 0.42f + state.phase * 1.3f) * 2.5f);

            state.position += (state.velocity + drift) * deltaTime * speedMultiplier;
            WrapPosition(ref state.position, 22f);

            float flicker = 0.72f
                + 0.19f * Mathf.Sin(_elapsed * 2.1f + state.phase)
                + 0.09f * Mathf.Sin(_elapsed * 4.7f + state.phase * 1.8f);

            ApplyParticle(ref state, flicker, 1f);
            _floatingStates[i] = state;
        }
    }

    private void UpdateRisingSparks(float deltaTime)
    {
        for (int i = 0; i < _sparkStates.Length; i++)
        {
            ParticleState state = _sparkStates[i];
            if (state.image == null || i >= risingSparkCount || !state.image.gameObject.activeSelf)
                continue;

            state.age += deltaTime * speedMultiplier;
            state.position += state.velocity * deltaTime * speedMultiplier;

            if (state.age >= state.lifetime || state.position.y > _halfHeight + 36f)
                ResetSpark(ref state);

            float life = Mathf.Clamp01(state.age / Mathf.Max(0.01f, state.lifetime));
            float fade = Mathf.Sin(life * Mathf.PI);
            float shimmer = 0.78f + 0.22f * Mathf.Sin(_elapsed * 6f + state.phase);

            ApplyParticle(ref state, fade * shimmer, 1f);
            _sparkStates[i] = state;
        }
    }

    private void UpdateBrightEmbers(float deltaTime)
    {
        for (int i = 0; i < _emberStates.Length; i++)
        {
            ParticleState state = _emberStates[i];
            if (state.image == null || i >= brightEmberCount || !state.image.gameObject.activeSelf)
                continue;

            state.age += deltaTime * speedMultiplier;
            state.position += state.velocity * deltaTime * speedMultiplier;

            if (state.age >= state.lifetime)
                ResetEmber(ref state);

            float life = Mathf.Clamp01(state.age / Mathf.Max(0.01f, state.lifetime));
            float pulse = Mathf.Sin(life * Mathf.PI);
            pulse *= pulse;

            float scalePulse = 0.88f + 0.18f * Mathf.Sin(_elapsed * 2.4f + state.phase);
            ApplyParticle(ref state, pulse, scalePulse);
            _emberStates[i] = state;
        }
    }

    private void UpdateHearthGlow()
    {
        float pulse = 0.91f + 0.09f * Mathf.Sin(_elapsed * 0.9f);
        float transition = 1f + _transitionBoost * 0.9f;

        ApplyGlow(hearthGlow, pulse * transition, 1f + _transitionBoost * 0.12f, 0.10f);
        ApplyGlow(hearthCore, pulse * transition, 1f + _transitionBoost * 0.16f, 0.13f);
    }

    private void ApplyGlow(Image image, float alphaMultiplier, float scaleMultiplier, float baseAlpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(baseAlpha * glowStrength * brightnessMultiplier * alphaMultiplier);
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.localScale = Vector3.one * scaleMultiplier;
    }

    private void ApplyParticle(ref ParticleState state, float alphaMultiplier, float scaleMultiplier)
    {
        if (state.rect == null || state.image == null)
            return;

        state.rect.anchoredPosition = state.position;
        state.rect.localScale = state.baseScale * scaleMultiplier;

        Color color = state.color;
        color.a = Mathf.Clamp01(state.baseAlpha * Mathf.Max(0f, alphaMultiplier) * brightnessMultiplier);
        state.image.color = color;
    }

    private void ResetSpark(ref ParticleState state)
    {
        state.position = new Vector2(
            UnityEngine.Random.Range(-_halfWidth, _halfWidth),
            -_halfHeight - UnityEngine.Random.Range(18f, 130f));
        state.velocity = new Vector2(
            UnityEngine.Random.Range(-10f, 10f),
            UnityEngine.Random.Range(34f, 70f));
        state.age = 0f;
        state.lifetime = UnityEngine.Random.Range(2.5f, 4.6f);
        state.phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    private void ResetEmber(ref ParticleState state)
    {
        state.position = new Vector2(
            UnityEngine.Random.Range(-_halfWidth, _halfWidth),
            UnityEngine.Random.Range(-_halfHeight * 0.82f, _halfHeight * 0.82f));
        state.velocity = UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(1f, 4f);
        state.age = 0f;
        state.lifetime = UnityEngine.Random.Range(2.6f, 5.8f);
        state.phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    private void WrapPosition(ref Vector2 position, float margin)
    {
        if (position.x > _halfWidth + margin)
            position.x = -_halfWidth - margin;
        else if (position.x < -_halfWidth - margin)
            position.x = _halfWidth + margin;

        if (position.y > _halfHeight + margin)
            position.y = -_halfHeight - margin;
        else if (position.y < -_halfHeight - margin)
            position.y = _halfHeight + margin;
    }

    private void ApplyParticleCounts()
    {
        SetActiveCount(floatingLights, floatingLightCount);
        SetActiveCount(risingSparks, risingSparkCount);
        SetActiveCount(brightEmbers, brightEmberCount);
    }

    private static void SetActiveCount(Image[] images, int count)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.activeSelf != (i < count))
                images[i].gameObject.SetActive(i < count);
        }
    }

    private void OnDestroy()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
    }

    private void OnValidate()
    {
        floatingLightCount = Mathf.Clamp(floatingLightCount, 0, 24);
        risingSparkCount = Mathf.Clamp(risingSparkCount, 0, 14);
        brightEmberCount = Mathf.Clamp(brightEmberCount, 0, 5);
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 2f);
        brightnessMultiplier = Mathf.Clamp(brightnessMultiplier, 0.1f, 2f);
        glowStrength = Mathf.Clamp(glowStrength, 0.1f, 2f);
        transitionDuration = Mathf.Max(0.1f, transitionDuration);
        flashPeakAlpha = Mathf.Clamp(flashPeakAlpha, 0f, 0.4f);

        ApplyParticleCounts();
    }
}
