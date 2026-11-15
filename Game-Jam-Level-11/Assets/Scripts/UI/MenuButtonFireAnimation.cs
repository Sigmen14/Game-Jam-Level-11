using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable warm-fire feedback for an existing uGUI Button.
/// It listens to pointer events without replacing Button.onClick or its transition.
/// All animation uses unscaled time, so it remains responsive while the game is paused.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MenuButtonFireAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Движение")]
    [SerializeField, Range(1f, 1.25f)] private float hoverScale = 1.055f;
    [SerializeField, Min(0f)] private float liftPixels = 7f;
    [SerializeField, Min(0.1f)] private float movementSpeed = 16f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.96f;
    [SerializeField, Min(0f)] private float pressedDownPixels = 2f;

    [Header("Оранжевое свечение")]
    [SerializeField] private Image glowImage;
    [SerializeField, Range(0f, 1f)] private float glowIntensity = 0.72f;
    [SerializeField, Min(0.1f)] private float glowFadeSpeed = 12f;
    [SerializeField] private Image flashImage;
    [SerializeField, Range(0f, 1.5f)] private float flashIntensity = 1f;
    [SerializeField, Min(0.05f)] private float clickFlashDuration = 0.24f;

    [Header("Искры")]
    [SerializeField] private Image[] sparkImages;
    [SerializeField, Min(0.01f)] private float sparkSpawnInterval = 0.14f;
    [SerializeField, Min(1f)] private float sparkSpeed = 38f;
    [SerializeField, Min(0.05f)] private float sparkLifetime = 0.48f;

    private Button _button;
    private RectTransform _rectTransform;
    private Vector3 _baseScale;
    private Vector2 _basePosition;
    private bool _isHovered;
    private bool _isPressed;
    private float _sparkSpawnTimer;
    private float _flashRemaining;
    private float _currentGlow;
    private int _nextSpark;

    private Vector2[] _sparkVelocities;
    private float[] _sparkLifeRemaining;
    private float[] _sparkLifeDuration;
    private Color[] _sparkBaseColors;

    private void Awake()
    {
        CacheReferences();
        CacheSparkData();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (_rectTransform != null)
        {
            _baseScale = _rectTransform.localScale;
            _basePosition = _rectTransform.anchoredPosition;
        }

        _isHovered = false;
        _isPressed = false;
        _sparkSpawnTimer = 0f;
        _flashRemaining = 0f;
        _currentGlow = 0f;

        CacheSparkData();
        SetImageAlpha(glowImage, 0f);
        SetImageAlpha(flashImage, 0f);
        if (flashImage != null)
            flashImage.rectTransform.localScale = Vector3.one;
        ClearSparks();
    }

    private void Update()
    {
        if (_rectTransform == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        float movementLerp = 1f - Mathf.Exp(-movementSpeed * deltaTime);

        float scaleMultiplier = _isHovered ? hoverScale : 1f;
        if (_isPressed)
            scaleMultiplier *= pressedScale;

        Vector3 targetScale = _baseScale * scaleMultiplier;
        _rectTransform.localScale = Vector3.Lerp(
            _rectTransform.localScale,
            targetScale,
            movementLerp);

        float lift = _isHovered ? liftPixels : 0f;
        float pressOffset = _isPressed ? pressedDownPixels : 0f;
        Vector2 targetPosition = _basePosition + Vector2.up * (lift - pressOffset);
        _rectTransform.anchoredPosition = Vector2.Lerp(
            _rectTransform.anchoredPosition,
            targetPosition,
            movementLerp);

        UpdateGlow(deltaTime);
        UpdateClickFlash(deltaTime);
        UpdateSparks(deltaTime);

        if (_isHovered && sparkImages != null && sparkImages.Length > 0)
        {
            _sparkSpawnTimer += deltaTime;
            while (_sparkSpawnTimer >= sparkSpawnInterval)
            {
                _sparkSpawnTimer -= sparkSpawnInterval;
                SpawnSpark(false);
            }
        }
        else
        {
            _sparkSpawnTimer = 0f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button == null || !_button.IsInteractable())
            return;

        _isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button == null || !_button.IsInteractable())
            return;

        _isPressed = true;
        _flashRemaining = clickFlashDuration;

        int burstCount = Mathf.Min(5, sparkImages == null ? 0 : sparkImages.Length);
        for (int i = 0; i < burstCount; i++)
            SpawnSpark(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
    }

    private void OnDisable()
    {
        _isHovered = false;
        _isPressed = false;
        _flashRemaining = 0f;
        _currentGlow = 0f;

        if (_rectTransform != null)
        {
            _rectTransform.localScale = _baseScale;
            _rectTransform.anchoredPosition = _basePosition;
        }

        SetImageAlpha(glowImage, 0f);
        SetImageAlpha(flashImage, 0f);
        ClearSparks();
    }

    private void CacheReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;
    }

    private void CacheSparkData()
    {
        int count = sparkImages == null ? 0 : sparkImages.Length;
        _sparkVelocities = new Vector2[count];
        _sparkLifeRemaining = new float[count];
        _sparkLifeDuration = new float[count];
        _sparkBaseColors = new Color[count];

        for (int i = 0; i < count; i++)
        {
            Image spark = sparkImages[i];
            if (spark != null)
            {
                Color baseColor = spark.color;
                baseColor.a = 1f;
                _sparkBaseColors[i] = baseColor;
            }
        }
    }

    private void UpdateGlow(float deltaTime)
    {
        if (glowImage == null)
            return;

        float hoverTarget = _isHovered ? glowIntensity : 0f;
        float flashTarget = _flashRemaining > 0f ? flashIntensity : 0f;
        float targetAlpha = Mathf.Max(hoverTarget, flashTarget);
        float lerp = 1f - Mathf.Exp(-glowFadeSpeed * deltaTime);

        _currentGlow = Mathf.Lerp(_currentGlow, targetAlpha, lerp);
        SetImageAlpha(glowImage, _currentGlow);
    }

    private void UpdateClickFlash(float deltaTime)
    {
        if (flashImage == null)
            return;

        if (_flashRemaining <= 0f)
        {
            SetImageAlpha(flashImage, 0f);
            flashImage.rectTransform.localScale = Vector3.one;
            return;
        }

        _flashRemaining = Mathf.Max(0f, _flashRemaining - deltaTime);
        float progress = 1f - (_flashRemaining / Mathf.Max(0.05f, clickFlashDuration));
        float alpha = flashIntensity * (1f - Mathf.SmoothStep(0f, 1f, progress));
        SetImageAlpha(flashImage, alpha);

        RectTransform flashRect = flashImage.rectTransform;
        float flashScale = Mathf.Lerp(0.92f, 1.22f, progress);
        flashRect.localScale = Vector3.one * flashScale;
    }

    private void UpdateSparks(float deltaTime)
    {
        if (sparkImages == null)
            return;

        for (int i = 0; i < sparkImages.Length; i++)
        {
            Image spark = sparkImages[i];
            if (spark == null || _sparkLifeRemaining[i] <= 0f)
                continue;

            _sparkLifeRemaining[i] = Mathf.Max(0f, _sparkLifeRemaining[i] - deltaTime);
            RectTransform sparkRect = spark.rectTransform;
            sparkRect.anchoredPosition += _sparkVelocities[i] * deltaTime;
            _sparkVelocities[i] += Vector2.up * (sparkSpeed * 0.22f * deltaTime);

            float normalizedLife = _sparkLifeRemaining[i] /
                Mathf.Max(0.05f, _sparkLifeDuration[i]);
            Color color = _sparkBaseColors[i];
            color.a *= normalizedLife;
            spark.color = color;
        }
    }

    private void SpawnSpark(bool burst)
    {
        if (sparkImages == null || sparkImages.Length == 0 || _rectTransform == null)
            return;

        int index = _nextSpark;
        _nextSpark = (_nextSpark + 1) % sparkImages.Length;

        Image spark = sparkImages[index];
        if (spark == null)
            return;

        float angle = burst
            ? Random.Range(0f, 360f)
            : Random.Range(58f, 122f);

        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        float speed = sparkSpeed * (burst ? Random.Range(0.9f, 1.5f) : Random.Range(0.65f, 1.1f));

        Rect rect = _rectTransform.rect;
        Vector2 origin = burst
            ? new Vector2(Random.Range(-rect.width * 0.28f, rect.width * 0.28f),
                          Random.Range(-rect.height * 0.22f, rect.height * 0.22f))
            : new Vector2(Random.Range(-rect.width * 0.38f, rect.width * 0.38f),
                          Random.Range(-rect.height * 0.12f, rect.height * 0.16f));

        RectTransform sparkRect = spark.rectTransform;
        sparkRect.anchoredPosition = origin;
        float size = Random.Range(7f, 13f);
        sparkRect.sizeDelta = new Vector2(size, size);
        sparkRect.localScale = Vector3.one;

        _sparkVelocities[index] = direction * speed;
        _sparkLifeDuration[index] = sparkLifetime * (burst ? Random.Range(0.8f, 1.2f) : Random.Range(0.75f, 1.1f));
        _sparkLifeRemaining[index] = _sparkLifeDuration[index];

        Color color = _sparkBaseColors[index];
        color.a = 1f;
        spark.color = color;
    }

    private void ClearSparks()
    {
        if (sparkImages == null)
            return;

        for (int i = 0; i < sparkImages.Length; i++)
        {
            _sparkLifeRemaining[i] = 0f;
            if (sparkImages[i] != null)
                SetImageAlpha(sparkImages[i], 0f);
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void OnValidate()
    {
        hoverScale = Mathf.Clamp(hoverScale, 1f, 1.25f);
        movementSpeed = Mathf.Max(0.1f, movementSpeed);
        glowFadeSpeed = Mathf.Max(0.1f, glowFadeSpeed);
        clickFlashDuration = Mathf.Max(0.05f, clickFlashDuration);
        sparkSpawnInterval = Mathf.Max(0.01f, sparkSpawnInterval);
        sparkLifetime = Mathf.Max(0.05f, sparkLifetime);
        sparkSpeed = Mathf.Max(1f, sparkSpeed);
    }
}
