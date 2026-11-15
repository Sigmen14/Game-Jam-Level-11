using UnityEngine;

/// <summary>One temporary, one-use group of living embers that restores fuel.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class LivingSparksPickup : MonoBehaviour
{
    private static Sprite sharedSparkSprite;
    private static readonly Vector2[] SparkOffsets =
    {
        new Vector2(-0.34f, -0.03f), new Vector2(-0.17f, 0.22f),
        new Vector2(0.02f, -0.15f), new Vector2(0.20f, 0.17f),
        new Vector2(0.37f, -0.01f)
    };
    private static readonly float[] SparkScales = { 0.34f, 0.28f, 0.39f, 0.30f, 0.25f };
    private static readonly Color[] SparkTints =
    {
        new Color(1f, 0.45f, 0.08f, 1f), new Color(1f, 0.78f, 0.26f, 1f),
        new Color(1f, 0.57f, 0.10f, 1f), new Color(1f, 0.88f, 0.48f, 1f),
        new Color(1f, 0.36f, 0.045f, 1f)
    };

    private readonly Transform[] sparkTransforms = new Transform[5];
    private readonly SpriteRenderer[] sparkRenderers = new SpriteRenderer[5];
    private readonly Vector3[] baseScales = new Vector3[5];
    private FlameMeter flameMeter;
    private HUDView hudView;
    private CircleCollider2D trigger;
    private float fuelReward;
    private float remainingLifetime;
    private bool initialized;
    private bool resolved;

    private void Awake()
    {
        trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.92f;
        BuildVisuals();
    }

    public void Initialize(FlameMeter meter, HUDView hud, float reward, float lifetime)
    {
        flameMeter = meter;
        hudView = hud;
        fuelReward = Mathf.Max(0f, reward);
        remainingLifetime = Mathf.Max(1f, lifetime);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || resolved)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            resolved = true;
            Destroy(gameObject);
            return;
        }

        float time = Time.time;
        for (int i = 0; i < sparkTransforms.Length; i++)
        {
            float phase = i * 1.63f;
            float pulse = 0.88f + 0.14f * Mathf.Sin(time * (4.1f + i * 0.37f) + phase);
            Vector2 drift = new Vector2(
                Mathf.Sin(time * 1.8f + phase) * 0.075f,
                Mathf.Sin(time * 2.35f + phase) * 0.12f);
            sparkTransforms[i].localPosition = SparkOffsets[i] + drift;
            sparkTransforms[i].localScale = baseScales[i] * pulse;
            sparkRenderers[i].color = Color.Lerp(SparkTints[i], Color.white, pulse * 0.18f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || resolved || flameMeter == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsGameplayActive || flameMeter.Current >= flameMeter.Maximum)
            return;

        resolved = true;
        float fuelBefore = flameMeter.Current;
        flameMeter.AddFuel(fuelReward);
        float fuelGained = flameMeter.Current - fuelBefore;
        if (fuelGained > 0.01f)
        {
            flameMeter.RegisterSuccessfulFuelPickup();
            if (hudView != null)
                hudView.ShowPickupNotification(
                    "+" + Mathf.RoundToInt(fuelGained) + " ТОПЛИВА · ИСКРЫ СОБРАНЫ", 3f);
        }

        if (trigger != null)
            trigger.enabled = false;
        Destroy(gameObject);
    }

    private void BuildVisuals()
    {
        Sprite sprite = GetSharedSparkSprite();
        for (int i = 0; i < sparkTransforms.Length; i++)
        {
            GameObject spark = new GameObject("LivingSpark_" + (i + 1), typeof(SpriteRenderer));
            Transform sparkTransform = spark.transform;
            sparkTransform.SetParent(transform, false);
            sparkTransform.localPosition = SparkOffsets[i];
            float scale = SparkScales[i];
            baseScales[i] = new Vector3(scale, scale * 1.15f, 1f);
            sparkTransform.localScale = baseScales[i];

            SpriteRenderer renderer = spark.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = SparkTints[i];
            renderer.sortingOrder = 8;
            sparkTransforms[i] = sparkTransform;
            sparkRenderers[i] = renderer;
        }
    }

    private static Sprite GetSharedSparkSprite()
    {
        if (sharedSparkSprite != null)
            return sharedSparkSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated_LivingSparkGlow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float vertical = y / (float)(size - 1);
            float halfWidth = Mathf.Lerp(0.34f, 0.018f, vertical);
            float center = 0.5f + 0.055f * Mathf.Sin(vertical * 3.1f);
            float topFade = Mathf.Clamp01((1f - vertical) / 0.16f);
            float bottomFade = Mathf.Clamp01(vertical / 0.12f);
            for (int x = 0; x < size; x++)
            {
                float horizontal = x / (float)(size - 1);
                float distance = Mathf.Abs(horizontal - center);
                float body = Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, halfWidth));
                body = Mathf.Clamp01(body * 5.5f) * topFade * bottomFade;
                float glowX = Mathf.Exp(-Mathf.Pow(distance / (halfWidth * 2.5f + 0.035f), 2f) * 1.4f);
                float glowY = Mathf.Exp(-Mathf.Pow((vertical - 0.46f) * 1.45f, 2f));
                float glow = glowX * glowY * 0.26f;
                float alpha = Mathf.Clamp01(glow + body);
                Color color = new Color(1f, 0.22f + 0.45f * vertical, 0.025f, alpha);
                if (body > 0f)
                {
                    Color core = Color.Lerp(
                        new Color(1f, 0.48f, 0.055f, alpha),
                        new Color(1f, 0.94f, 0.63f, alpha),
                        Mathf.Clamp01((1f - distance / Mathf.Max(0.001f, halfWidth)) * 1.45f));
                    color = Color.Lerp(color, core, body);
                    color.a = alpha;
                }
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedSparkSprite = Sprite.Create(
            texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.42f), 64f, 0,
            SpriteMeshType.Tight);
        sharedSparkSprite.name = "SPR_LivingEmber";
        return sharedSparkSprite;
    }
}
