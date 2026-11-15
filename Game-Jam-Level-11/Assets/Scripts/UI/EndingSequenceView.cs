using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the two distinct lighthouse finales using pre-authored UI elements.
/// The world-ending path sends a broad golden signal; the ember path stays intimate and dark.
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

    private Vector2 emberHomePosition;
    private Vector3 emberHomeScale;
    private Vector3 coreHomeScale;
    private Color atmosphereHomeColor;
    private Color coreHomeColor;
    private Color emberGlowHomeColor;
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

    public void ResetVisuals()
    {
        CacheHomeValues();

        if (atmosphere != null)
            atmosphere.color = atmosphereHomeColor;

        if (beaconCore != null)
        {
            beaconCore.color = coreHomeColor;
            beaconCore.rectTransform.localScale = coreHomeScale;
        }

        if (ember != null)
        {
            ember.anchoredPosition = emberHomePosition;
            ember.localScale = emberHomeScale;
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
                if (distantBeacons[i] != null)
                    SetAlpha(distantBeacons[i], 0f);
            }
        }

        if (resultContentGroup != null)
        {
            resultContentGroup.alpha = 0f;
            resultContentGroup.interactable = false;
            resultContentGroup.blocksRaycasts = false;
            resultContentGroup.gameObject.SetActive(false);
        }

        if (endingTitleText != null)
            endingTitleText.text = string.Empty;
        if (endingBodyText != null)
            endingBodyText.text = string.Empty;
        if (statisticsText != null)
            statisticsText.text = string.Empty;
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
        string title, string body, string statistics)
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
        resultContentGroup.interactable = true;
        resultContentGroup.blocksRaycasts = true;

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

    private IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CacheHomeValues()
    {
        if (referencesCached)
            return;

        emberHomePosition = ember != null ? ember.anchoredPosition : Vector2.zero;
        emberHomeScale = ember != null ? ember.localScale : Vector3.one;
        coreHomeScale = beaconCore != null ? beaconCore.rectTransform.localScale : Vector3.one;
        atmosphereHomeColor = atmosphere != null
            ? atmosphere.color
            : new Color(0.005f, 0.012f, 0.026f, 0.94f);
        coreHomeColor = beaconCore != null ? beaconCore.color : Color.white;
        emberHomeColor = ember != null && ember.GetComponent<Image>() != null
            ? ember.GetComponent<Image>().color
            : Color.white;
        emberGlowHomeColor = emberGlow != null ? emberGlow.color : Color.white;
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
