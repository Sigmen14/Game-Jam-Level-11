using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable cinematic view for the victory/defeat preludes and the two existing
/// lighthouse finales. It animates only the UI elements already authored in UI_HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingSequenceView : MonoBehaviour
{
    [Header("Сцена и маяк")]
    [SerializeField] private Image atmosphere;
    [SerializeField] private Image beaconCore;
    [SerializeField] private RectTransform ember;
    [SerializeField] private Image emberGlow;
    [SerializeField] private RectTransform signalWave;
    [SerializeField] private Image signalWaveImage;
    [SerializeField] private Image[] distantBeacons;

    [Header("Финальный текст")]
    [SerializeField] private CanvasGroup resultContentGroup;
    [SerializeField] private Text endingTitleText;
    [SerializeField] private Text endingBodyText;
    [SerializeField] private Text statisticsText;

    [Header("Атмосферный звук")]
    [SerializeField] private AudioSource windAudio;
    [SerializeField] private AudioSource fireAudio;

    private Vector2 emberHomePosition;
    private Vector3 emberHomeScale;
    private Vector3 coreHomeScale;
    private Color atmosphereHomeColor;
    private Color coreHomeColor;
    private Color emberGlowHomeColor;
    private Color emberHomeColor;
    private Vector2[] distantBeaconHomePositions;
    private Vector3[] distantBeaconHomeScales;
    private Color[] distantBeaconHomeColors;
    private float windHomeVolume;
    private float fireHomeVolume;
    private bool referencesCached;

    private void Awake()
    {
        CacheHomeValues();
        ResetVisuals();
    }

    public IEnumerator PlayEnding(EndingStatistics.Choice choice, string statistics)
    {
        CacheHomeValues();
        ResetVisuals();
        SetSequenceCanvasState(true, true);

        if (choice == EndingStatistics.Choice.RekindleWorld)
        {
            yield return PlayRekindleWorld();
            yield return RevealFinalCopy(
                "Разжечь мир",
                "Одна искра смогла разбудить тысячи. Мир ещё не спасён. Но теперь он больше не одинок.",
                statistics);
        }
        else if (choice == EndingStatistics.Choice.KeepEmber)
        {
            yield return PlayKeepEmber();
            yield return RevealFinalCopy(
                "Сохранить искру",
                "Он не стал последним светом этого мира. Он решил стать первым, кто найдёт остальных.",
                statistics);
        }
    }

    /// <summary>
    /// Shows a short story beat before the existing result flow. Victory leads back
    /// to the two-choice screen; defeat is followed by HUDView's existing result panel.
    /// </summary>
    public IEnumerator PlayOutcomePrelude(bool didWin)
    {
        CacheHomeValues();
        ResetVisuals();
        SetSequenceCanvasState(true, false);

        if (didWin)
        {
            yield return PlayVictoryPrelude();
            yield return RevealFinalCopy(
                "НАДЕЖДА ВЕРНУЛАСЬ",
                "Последняя искра добралась до древнего маяка. Его золотой свет разошёлся по миру, и вдали один за другим вспыхнули забытые огни. Маленькое пламя изменило судьбу мира.",
                string.Empty,
                false);
        }
        else
        {
            yield return PlayDefeatPrelude();
            yield return RevealFinalCopy(
                "ИСКРА ПОД ПЕПЛОМ",
                "Серый Ветер оказался сильнее. Последний огонь погас, и мир снова погрузился во тьму. Но глубоко под пеплом ещё теплится крошечная искра. Эта история ещё не закончена...",
                string.Empty,
                false);
        }

        // Keep the final line readable, then fade the complete authored panel away.
        yield return WaitRealtime(didWin ? 2.1f : 2.25f);
        yield return FadeSequenceCanvasGroup(0.62f);
        StopOutcomeAmbience();
    }

    public void StopOutcomeAmbience()
    {
        if (windAudio != null)
        {
            windAudio.Stop();
            windAudio.volume = windHomeVolume;
        }

        if (fireAudio != null)
        {
            fireAudio.Stop();
            fireAudio.volume = fireHomeVolume;
        }
    }

    public void ResetVisuals()
    {
        CacheHomeValues();
        StopOutcomeAmbience();

        if (atmosphere != null)
            atmosphere.color = atmosphereHomeColor;

        if (beaconCore != null)
        {
            beaconCore.color = coreHomeColor;
            beaconCore.rectTransform.localScale = coreHomeScale;
            SetAlpha(beaconCore, coreHomeColor.a);
        }

        if (ember != null)
        {
            ember.anchoredPosition = emberHomePosition;
            ember.localScale = emberHomeScale;
        }

        if (ember != null)
        {
            Image emberImage = ember.GetComponent<Image>();
            if (emberImage != null)
                emberImage.color = emberHomeColor;
        }

        if (emberGlow != null)
        {
            emberGlow.color = emberGlowHomeColor;
            emberGlow.rectTransform.localScale = Vector3.one;
        }

        if (signalWave != null)
        {
            signalWave.anchoredPosition = beaconCore != null
                ? beaconCore.rectTransform.anchoredPosition
                : Vector2.zero;
            signalWave.localScale = Vector3.one * 0.18f;
        }

        if (signalWaveImage != null)
            SetAlpha(signalWaveImage, 0f);

        if (distantBeacons != null)
        {
            for (int i = 0; i < distantBeacons.Length; i++)
            {
                Image beacon = distantBeacons[i];
                if (beacon == null)
                    continue;

                if (distantBeaconHomePositions != null && i < distantBeaconHomePositions.Length)
                    beacon.rectTransform.anchoredPosition = distantBeaconHomePositions[i];
                if (distantBeaconHomeScales != null && i < distantBeaconHomeScales.Length)
                    beacon.rectTransform.localScale = distantBeaconHomeScales[i];
                if (distantBeaconHomeColors != null && i < distantBeaconHomeColors.Length)
                    beacon.color = distantBeaconHomeColors[i];

                SetAlpha(beacon, 0f);
            }
        }

        if (resultContentGroup != null)
        {
            resultContentGroup.alpha = 0f;
            resultContentGroup.interactable = false;
            resultContentGroup.blocksRaycasts = false;
            resultContentGroup.gameObject.SetActive(false);
        }

        SetSequenceCanvasState(true, false);

        if (endingTitleText != null)
            endingTitleText.text = string.Empty;
        if (endingBodyText != null)
            endingBodyText.text = string.Empty;
        if (statisticsText != null)
            statisticsText.text = string.Empty;
    }

    private IEnumerator PlayVictoryPrelude()
    {
        BeginOutcomeAmbience(true);

        // The last ember travels into the lantern room before the beacon ignites.
        Vector2 beaconPosition = beaconCore != null
            ? beaconCore.rectTransform.anchoredPosition
            : new Vector2(0f, 184f);
        yield return MoveEmber(beaconPosition, 1.55f);
        yield return PulseBeacon(1.12f);
        yield return ExpandSignalWave(2.15f);

        // A staggered parallel reveal keeps the distant lights cinematic but brief.
        yield return RevealDistantBeaconsCinematic(1.75f, 0.055f, 0.96f);
        yield return FadeAtmosphere(0.34f, 0.9f);
        yield return FadeAudio(windAudio, 0f, 0.85f);
        yield return FadeAudio(fireAudio, fireHomeVolume, 0.45f);
    }

    private IEnumerator PlayDefeatPrelude()
    {
        BeginOutcomeAmbience(false);

        if (signalWave != null)
        {
            signalWave.anchoredPosition = emberHomePosition;
            signalWave.localScale = Vector3.one * 0.28f;
        }

        if (signalWaveImage != null)
        {
            signalWaveImage.color = new Color(0.55f, 0.61f, 0.66f, 0f);
            SetAlpha(signalWaveImage, 0f);
        }

        Color darkAtmosphere = new Color(0.006f, 0.009f, 0.016f, 0.985f);
        Color dyingBeacon = new Color(0.13f, 0.14f, 0.15f, 0.78f);
        float elapsed = 0f;
        const float gustDuration = 2.35f;

        // The Grey Wind sweeps over the flame, while ash is carried across the scene.
        while (elapsed < gustDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / gustDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            if (atmosphere != null)
                atmosphere.color = Color.Lerp(atmosphereHomeColor, darkAtmosphere, eased);

            if (signalWave != null)
                signalWave.localScale = Vector3.one * Mathf.Lerp(0.28f, 14.5f, eased);
            if (signalWaveImage != null)
                SetAlpha(signalWaveImage, Mathf.Sin(progress * Mathf.PI) * 0.42f);

            if (emberGlow != null)
            {
                emberGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.16f, eased);
                Color fadingGlow = new Color(1f, 0.34f, 0.11f, Mathf.Lerp(emberGlowHomeColor.a, 0.035f, eased));
                emberGlow.color = fadingGlow;
            }

            if (ember != null)
            {
                ember.localScale = emberHomeScale * Mathf.Lerp(1f, 0.76f, eased);
                Image emberImage = ember.GetComponent<Image>();
                if (emberImage != null)
                {
                    Color fadingEmber = emberHomeColor;
                    fadingEmber.a = Mathf.Lerp(emberHomeColor.a, 0.10f, eased);
                    emberImage.color = fadingEmber;
                }
            }

            if (beaconCore != null)
            {
                beaconCore.color = Color.Lerp(coreHomeColor, dyingBeacon, eased);
                beaconCore.rectTransform.localScale = coreHomeScale * Mathf.Lerp(1f, 0.72f, eased);
            }

            AnimateAshParticles(elapsed, eased);
            BlendDefeatAudio(eased);
            yield return null;
        }

        if (signalWaveImage != null)
            SetAlpha(signalWaveImage, 0f);

        // Hold on the final ember for a moment: it is weak, but not quite gone.
        if (ember != null)
            ember.anchoredPosition = emberHomePosition;
        if (emberGlow != null)
        {
            emberGlow.color = new Color(1f, 0.29f, 0.075f, 0.12f);
            emberGlow.rectTransform.localScale = Vector3.one * 0.22f;
        }
        if (ember != null)
            ember.localScale = emberHomeScale * 0.72f;

        yield return WaitRealtime(1.25f);
    }

    private void AnimateAshParticles(float elapsed, float progress)
    {
        if (distantBeacons == null)
            return;

        for (int i = 0; i < distantBeacons.Length; i++)
        {
            Image particle = distantBeacons[i];
            if (particle == null)
                continue;

            // Deterministic screen-space paths make the ash drift feel authored,
            // rather than spawning random geometry at runtime.
            float xOrigin = Mathf.Repeat(i * 137f + 220f, 1720f) - 860f;
            float yOrigin = 410f - Mathf.Repeat(i * 83f + 35f, 790f);
            float fallSpeed = 22f + (i % 5) * 8f;
            float x = xOrigin + Mathf.Sin(elapsed * 0.82f + i * 1.6f) * (12f + i % 4 * 5f);
            float y = yOrigin - elapsed * fallSpeed;

            particle.rectTransform.anchoredPosition = new Vector2(x, y);
            float scalePulse = 0.72f + 0.28f * Mathf.Sin(elapsed * 2.1f + i);
            particle.rectTransform.localScale =
                distantBeaconHomeScales != null && i < distantBeaconHomeScales.Length
                    ? distantBeaconHomeScales[i] * scalePulse
                    : Vector3.one * scalePulse;

            Color ashColor = i % 6 == 0
                ? new Color(1f, 0.42f, 0.14f, 1f)
                : new Color(0.48f, 0.52f, 0.56f, 1f);
            float shimmer = 0.72f + 0.28f * Mathf.Sin(elapsed * 3.4f + i * 1.9f);
            ashColor.a = Mathf.Lerp(0.08f, 0.62f, progress) * shimmer;
            particle.color = ashColor;
        }
    }

    private IEnumerator RevealDistantBeaconsCinematic(
        float duration, float stagger, float targetAlpha)
    {
        if (distantBeacons == null)
            yield break;

        float elapsed = 0f;
        const float fadeDuration = 0.48f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < distantBeacons.Length; i++)
            {
                Image beacon = distantBeacons[i];
                if (beacon == null)
                    continue;

                float progress = Mathf.Clamp01((elapsed - i * stagger) / fadeDuration);
                SetAlpha(beacon, targetAlpha * Mathf.SmoothStep(0f, 1f, progress));
            }

            yield return null;
        }

        for (int i = 0; i < distantBeacons.Length; i++)
        {
            if (distantBeacons[i] != null)
                SetAlpha(distantBeacons[i], targetAlpha);
        }
    }

    private void BeginOutcomeAmbience(bool victory)
    {
        if (windAudio != null)
        {
            windAudio.volume = windHomeVolume * (victory ? 0.72f : 0.48f);
            if (!windAudio.isPlaying)
                windAudio.Play();
        }

        if (fireAudio != null)
        {
            fireAudio.volume = fireHomeVolume * (victory ? 0.7f : 0.9f);
            if (!fireAudio.isPlaying)
                fireAudio.Play();
        }
    }

    private void BlendDefeatAudio(float progress)
    {
        if (windAudio != null)
            windAudio.volume = windHomeVolume * Mathf.Lerp(0.48f, 1.35f, progress);
        if (fireAudio != null)
            fireAudio.volume = fireHomeVolume * (1f - progress);
    }

    private IEnumerator FadeAudio(AudioSource source, float targetVolume, float duration)
    {
        if (source == null)
            yield break;

        float startVolume = source.volume;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            source.volume = Mathf.Lerp(startVolume, targetVolume, progress);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private IEnumerator FadeSequenceCanvasGroup(float duration)
    {
        CanvasGroup group = GetSequenceCanvasGroup();
        if (group == null)
        {
            if (resultContentGroup != null)
                yield return FadeCanvasGroup(resultContentGroup, 0f, duration);
            yield break;
        }

        float startAlpha = group.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);
        group.interactable = false;
        group.blocksRaycasts = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }

        group.alpha = 0f;
    }

    private IEnumerator PlayRekindleWorld()
    {
        // 1. The last ember rises from the player towards the lighthouse heart.
        yield return MoveEmber(new Vector2(0f, 142f), 1.9f);

        // 2. The lighthouse heart ignites in a bright golden flash.
        yield return PulseBeacon(1.25f);

        // 3. A warm wave spreads out from the tower and crosses the sky.
        yield return ExpandSignalWave(2.8f);

        // 4. Distant beacons answer one after another.
        yield return RevealDistantBeacons(
            distantBeacons == null ? 0 : distantBeacons.Length,
            0.11f,
            0.96f,
            0.42f);

        // 5. Darkness retreats from the world.
        yield return FadeAtmosphere(0.30f, 1.75f);
    }

    private IEnumerator PlayKeepEmber()
    {
        // 1. The ember draws back from the lighthouse instead of entering its heart.
        yield return MoveEmber(new Vector2(-190f, -62f), 1.7f);

        // 2. It remains small, but its light stays steady and alive.
        yield return SustainEmber(2.1f);

        // 3. Only a few faint lights answer on the distant horizon.
        int horizonLights = distantBeacons == null
            ? 0
            : Mathf.Min(5, distantBeacons.Length);
        yield return RevealDistantBeacons(horizonLights, 0.58f, 0.34f, 0.78f);

        // This ending deliberately keeps the world dark: the journey is beginning,
        // not ending in failure.
        yield return WaitRealtime(0.65f);
    }

    private IEnumerator MoveEmber(Vector2 destination, float duration)
    {
        if (ember == null)
            yield break;

        Vector2 start = ember.anchoredPosition;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            ember.anchoredPosition = Vector2.LerpUnclamped(start, destination, eased);

            if (emberGlow != null)
            {
                float glowPulse = 1f + Mathf.Sin(progress * Mathf.PI * 3f) * 0.16f;
                emberGlow.rectTransform.localScale = Vector3.one * glowPulse;
            }

            yield return null;
        }

        ember.anchoredPosition = destination;
    }

    private IEnumerator PulseBeacon(float duration)
    {
        if (beaconCore == null)
            yield break;

        float elapsed = 0f;
        Color flashColor = new Color(1f, 0.91f, 0.48f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            beaconCore.rectTransform.localScale =
                coreHomeScale * (1f + pulse * 0.78f);
            beaconCore.color = Color.Lerp(coreHomeColor, flashColor, pulse);
            yield return null;
        }

        beaconCore.rectTransform.localScale = coreHomeScale * 1.18f;
        beaconCore.color = flashColor;
    }

    private IEnumerator ExpandSignalWave(float duration)
    {
        if (signalWave == null || signalWaveImage == null)
            yield break;

        signalWave.gameObject.SetActive(true);
        signalWave.anchoredPosition = beaconCore != null
            ? beaconCore.rectTransform.anchoredPosition
            : Vector2.zero;

        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);
        SetAlpha(signalWaveImage, 0.78f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            signalWave.localScale = Vector3.one * Mathf.Lerp(0.18f, 17f, eased);
            SetAlpha(signalWaveImage, 0.78f * (1f - progress));
            yield return null;
        }

        SetAlpha(signalWaveImage, 0f);
    }

    private IEnumerator RevealDistantBeacons(
        int count, float interval, float targetAlpha, float fadeDuration)
    {
        if (distantBeacons == null)
            yield break;

        count = Mathf.Clamp(count, 0, distantBeacons.Length);

        for (int i = 0; i < count; i++)
        {
            Image beacon = distantBeacons[i];
            if (beacon != null)
                yield return FadeImage(beacon, targetAlpha, fadeDuration);

            if (i < count - 1)
                yield return WaitRealtime(interval);
        }
    }

    private IEnumerator FadeImage(Image image, float targetAlpha, float duration)
    {
        if (image == null)
            yield break;

        Color original = image.color;
        float startAlpha = original.a;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetAlpha(image, Mathf.Lerp(startAlpha, targetAlpha, progress));
            yield return null;
        }

        SetAlpha(image, targetAlpha);
    }

    private IEnumerator SustainEmber(float duration)
    {
        if (emberGlow == null || ember == null)
        {
            yield return WaitRealtime(duration);
            yield break;
        }

        float elapsed = 0f;
        Color steadyGlow = new Color(1f, 0.46f, 0.12f, 0.94f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 2.4f);
            emberGlow.color = Color.Lerp(steadyGlow, new Color(1f, 0.76f, 0.28f, 1f), pulse * 0.22f);
            emberGlow.rectTransform.localScale = Vector3.one * (1f + pulse * 0.11f);
            ember.localScale = emberHomeScale * (1f + pulse * 0.025f);
            yield return null;
        }

        ember.localScale = emberHomeScale;
    }

    private IEnumerator FadeAtmosphere(float targetAlpha, float duration)
    {
        if (atmosphere == null)
            yield break;

        Color start = atmosphere.color;
        Color end = new Color(0.10f, 0.075f, 0.055f, targetAlpha);
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            atmosphere.color = Color.Lerp(start, end, progress);
            yield return null;
        }

        atmosphere.color = end;
    }

    private IEnumerator RevealFinalCopy(
        string title, string body, string statistics, bool allowInteraction = true)
    {
        if (endingTitleText != null)
            endingTitleText.text = title;
        if (endingBodyText != null)
            endingBodyText.text = body;
        if (statisticsText != null)
            statisticsText.text = statistics;

        if (resultContentGroup == null)
            yield break;

        resultContentGroup.gameObject.SetActive(true);
        resultContentGroup.alpha = 0f;
        resultContentGroup.interactable = allowInteraction;
        resultContentGroup.blocksRaycasts = allowInteraction;

        float elapsed = 0f;
        const float duration = 1.05f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            resultContentGroup.alpha = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        resultContentGroup.alpha = 1f;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
    {
        if (group == null)
            yield break;

        float startAlpha = group.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        group.alpha = targetAlpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private CanvasGroup GetSequenceCanvasGroup()
    {
        return atmosphere != null ? atmosphere.GetComponent<CanvasGroup>() : null;
    }

    private void SetSequenceCanvasState(bool visible, bool interactive)
    {
        CanvasGroup group = GetSequenceCanvasGroup();
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private void CacheHomeValues()
    {
        if (referencesCached)
            return;

        emberHomePosition = ember != null ? ember.anchoredPosition : Vector2.zero;
        emberHomeScale = ember != null ? ember.localScale : Vector3.one;
        Image emberImage = ember != null ? ember.GetComponent<Image>() : null;
        emberHomeColor = emberImage != null ? emberImage.color : Color.white;
        coreHomeScale = beaconCore != null ? beaconCore.rectTransform.localScale : Vector3.one;
        atmosphereHomeColor = atmosphere != null
            ? atmosphere.color
            : new Color(0.005f, 0.012f, 0.026f, 0.94f);
        coreHomeColor = beaconCore != null ? beaconCore.color : Color.white;
        emberGlowHomeColor = emberGlow != null ? emberGlow.color : Color.white;
        windHomeVolume = windAudio != null ? windAudio.volume : 0f;
        fireHomeVolume = fireAudio != null ? fireAudio.volume : 0f;

        if (distantBeacons != null)
        {
            distantBeaconHomePositions = new Vector2[distantBeacons.Length];
            distantBeaconHomeScales = new Vector3[distantBeacons.Length];
            distantBeaconHomeColors = new Color[distantBeacons.Length];

            for (int i = 0; i < distantBeacons.Length; i++)
            {
                Image beacon = distantBeacons[i];
                if (beacon == null)
                    continue;

                distantBeaconHomePositions[i] = beacon.rectTransform.anchoredPosition;
                distantBeaconHomeScales[i] = beacon.rectTransform.localScale;
                distantBeaconHomeColors[i] = beacon.color;
            }
        }

        referencesCached = true;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
