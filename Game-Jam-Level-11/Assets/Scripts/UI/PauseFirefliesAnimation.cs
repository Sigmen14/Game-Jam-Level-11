using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Subtle floating fireflies for the pause card.
/// All particle objects are authored in the prefab; this component only animates them.
/// Unscaled time keeps the fireflies moving while gameplay is paused.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseFirefliesAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform[] particles = Array.Empty<RectTransform>();
    [SerializeField, Min(0f)] private float driftDistance = 7f;
    [SerializeField, Min(0.1f)] private float speed = 1f;
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.35f;

    private Vector2[] _basePositions = Array.Empty<Vector2>();
    private Vector3[] _baseScales = Array.Empty<Vector3>();
    private Image[] _images = Array.Empty<Image>();
    private float[] _baseAlphas = Array.Empty<float>();
    private float[] _phases = Array.Empty<float>();

    private void Awake()
    {
        if (particles == null)
            particles = Array.Empty<RectTransform>();

        int count = particles.Length;
        _basePositions = new Vector2[count];
        _baseScales = new Vector3[count];
        _images = new Image[count];
        _baseAlphas = new float[count];
        _phases = new float[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform particle = particles[i];
            if (particle == null)
                continue;

            _basePositions[i] = particle.anchoredPosition;
            _baseScales[i] = particle.localScale;
            _phases[i] = i * 1.731f;

            Image image = particle.GetComponent<Image>();
            _images[i] = image;
            if (image != null)
            {
                image.raycastTarget = false;
                _baseAlphas[i] = image.color.a;
            }
        }
    }

    private void Update()
    {
        float time = Time.unscaledTime * speed;

        for (int i = 0; i < particles.Length; i++)
        {
            RectTransform particle = particles[i];
            if (particle == null)
                continue;

            float phase = _phases[i];
            float wave = time * (0.65f + (i % 3) * 0.13f) + phase;
            particle.anchoredPosition = _basePositions[i] + new Vector2(
                Mathf.Sin(wave) * driftDistance,
                Mathf.Cos(wave * 0.83f) * driftDistance * 0.75f);

            float pulse = 0.88f + 0.12f * Mathf.Sin(time * 1.4f + phase);
            particle.localScale = _baseScales[i] * pulse;

            Image image = _images[i];
            if (image != null)
            {
                Color color = image.color;
                float shimmer = 0.5f + 0.5f * Mathf.Sin(time * 1.8f + phase);
                color.a = _baseAlphas[i] * Mathf.Lerp(minimumAlpha, 1f, shimmer);
                image.color = color;
            }
        }
    }

    private void OnValidate()
    {
        driftDistance = Mathf.Max(0f, driftDistance);
        speed = Mathf.Max(0.1f, speed);
        minimumAlpha = Mathf.Clamp01(minimumAlpha);
    }
}
