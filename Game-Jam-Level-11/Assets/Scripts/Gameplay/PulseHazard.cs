using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PulseHazard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D obstacleCollider;
    [SerializeField] private FlameMeter flameMeter;
    [Header("Pulse timing")]
    [SerializeField, Min(0.5f)] private float cycleDuration = 3.6f;
    [SerializeField, Min(0f)] private float warningDuration = 0.72f;
    [SerializeField, Min(0f)] private float activeDuration = 1.28f;
    [SerializeField, Min(0f)] private float phaseOffset;
    [Header("Contact damage")]
    [SerializeField, Min(0f)] private float damagePerSecond = 14f;
    [SerializeField] private Color safeColor = new Color(0.25f, 0.30f, 0.36f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.68f, 0.20f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.18f, 0.07f, 1f);

    public bool IsWarning { get; private set; }
    public bool IsActive { get; private set; }
    private bool running;
    private float elapsed;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (obstacleCollider == null) obstacleCollider = GetComponent<BoxCollider2D>();
        ApplyState(false, false);
    }

    private void Update()
    {
        if (!running) return;
        elapsed += Time.deltaTime;
        EvaluatePhase();
    }

    public void SetRunning(bool isRunning)
    {
        running = isRunning;
        elapsed = 0f;
        if (!running)
        {
            ApplyState(false, false);
            return;
        }
        EvaluatePhase();
    }

    private void EvaluatePhase()
    {
        float phase = Mathf.Repeat(elapsed + phaseOffset, Mathf.Max(0.5f, cycleDuration));
        bool warning = phase < warningDuration;
        bool active = phase >= warningDuration && phase < warningDuration + activeDuration;
        ApplyState(warning, active);
    }

    private void ApplyState(bool warning, bool active)
    {
        IsWarning = warning;
        IsActive = active;
        if (obstacleCollider != null) obstacleCollider.enabled = active;
        if (spriteRenderer != null)
            spriteRenderer.color = active ? activeColor : warning ? warningColor : safeColor;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!running || !IsActive || flameMeter == null) return;
        bool playerContact =
            collision.collider.GetComponentInParent<PlayerController>() != null ||
            collision.otherCollider.GetComponentInParent<PlayerController>() != null;
        if (playerContact)
            flameMeter.ApplyHazardDamage(damagePerSecond * Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        running = false;
        ApplyState(false, false);
    }

    private void OnValidate()
    {
        cycleDuration = Mathf.Max(0.5f, cycleDuration);
        warningDuration = Mathf.Clamp(warningDuration, 0f, cycleDuration);
        activeDuration = Mathf.Clamp(activeDuration, 0f, cycleDuration - warningDuration);
        damagePerSecond = Mathf.Max(0f, damagePerSecond);
    }
}
