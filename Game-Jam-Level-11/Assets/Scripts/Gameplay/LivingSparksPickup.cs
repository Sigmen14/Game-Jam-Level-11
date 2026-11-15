using UnityEngine;

/// <summary>Five visible living embers, collected once or removed after a short lifetime.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class LivingSparksPickup : MonoBehaviour
{
    private sealed class SparkVisual
    {
        public Transform Root;
        public SpriteRenderer Glow;
        public SpriteRenderer Core;
        public Vector3 BaseScale;
        public float Phase;
    }

    private static readonly Vector2[] SparkOffsets =
    {
        new Vector2(-0.78f, -0.14f),
        new Vector2(-0.40f, 0.48f),
        new Vector2(0.00f, -0.30f),
        new Vector2(0.40f, 0.42f),
        new Vector2(0.80f, -0.04f)
    };
    private static readonly float[] SparkScales = { 1.05f, 0.98f, 1.12f, 1.02f, 0.94f };
    private static readonly Color[] SparkColors =
    {
        new Color(1f, 0.38f, 0.055f, 1f),
        new Color(1f, 0.72f, 0.16f, 1f),
        new Color(1f, 0.50f, 0.075f, 1f),
        new Color(1f, 0.86f, 0.35f, 1f),
        new Color(0.96f, 0.18f, 0.035f, 1f)
    };

    private readonly SparkVisual[] visuals = new SparkVisual[5];
    private FlameMeter flameMeter;
    private HUDView hudView;
    private CircleCollider2D pickupTrigger;
    private float fuelReward;
    private float remainingLifetime;
    private bool initialized;
    private bool resolved;

    private void Awake()
    {
        pickupTrigger = GetComponent<CircleCollider2D>();
        pickupTrigger.enabled = true;
        pickupTrigger.isTrigger = true;
        pickupTrigger.radius = 1.85f;
    }

    public void Initialize(
        FlameMeter meter, HUDView hud, float reward, float lifetime,
        Sprite glowSprite, Sprite coreSprite, Material sparkMaterial)
    {
        flameMeter = meter;
        hudView = hud;
        fuelReward = Mathf.Max(0f, reward);
        remainingLifetime = Mathf.Max(1f, lifetime);

        if (glowSprite == null || coreSprite == null || sparkMaterial == null)
        {
            Debug.LogError("LivingSparksPickup: required Last Ember spark art references are missing.", this);
            Destroy(gameObject);
            return;
        }

        BuildVisuals(glowSprite, coreSprite, sparkMaterial);
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
        for (int i = 0; i < visuals.Length; i++)
        {
            SparkVisual visual = visuals[i];
            if (visual == null || visual.Root == null)
                continue;

            float pulse = 0.88f + 0.16f * Mathf.Sin(time * (4.4f + i * 0.31f) + visual.Phase);
            float flicker = 0.82f + 0.18f * Mathf.Sin(time * (8.3f + i * 0.47f) + visual.Phase * 1.7f);
            Vector2 drift = new Vector2(
                Mathf.Sin(time * 1.7f + visual.Phase) * 0.10f,
                Mathf.Sin(time * 2.15f + visual.Phase) * 0.15f);

            visual.Root.localPosition = SparkOffsets[i] + drift;
            visual.Root.localScale = visual.BaseScale * pulse;
            visual.Glow.color = new Color(
                SparkColors[i].r, SparkColors[i].g, SparkColors[i].b,
                Mathf.Lerp(0.60f, 0.92f, flicker));
            visual.Core.color = Color.Lerp(SparkColors[i], Color.white, 0.18f + flicker * 0.22f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Full fuel must not consume a group. Stay allows collection once fuel is spent.
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (!initialized || resolved || flameMeter == null || other == null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsGameplayActive || flameMeter.Current >= flameMeter.Maximum)
            return;

        float fuelBefore = flameMeter.Current;
        flameMeter.AddFuel(fuelReward);
        float fuelGained = flameMeter.Current - fuelBefore;
        if (fuelGained <= 0.01f)
            return;

        resolved = true;
        flameMeter.RegisterSuccessfulFuelPickup();
        if (pickupTrigger != null)
            pickupTrigger.enabled = false;
        if (hudView != null)
            hudView.ShowPickupNotification(
                "+" + Mathf.RoundToInt(fuelGained) + " ТОПЛИВА · ЖИВЫЕ ИСКРЫ СОБРАНЫ", 3f);
        Destroy(gameObject);
    }

    private void BuildVisuals(Sprite glowSprite, Sprite coreSprite, Material sparkMaterial)
    {
        const float glowScale = 2.55f;
        const float coreScale = 1.55f;
        for (int i = 0; i < visuals.Length; i++)
        {
            GameObject sparkRoot = new GameObject("LivingSpark_" + (i + 1));
            Transform root = sparkRoot.transform;
            root.SetParent(transform, false);
            root.localPosition = SparkOffsets[i];

            float size = SparkScales[i];
            root.localScale = new Vector3(size, size, 1f);

            SpriteRenderer glow = CreateSpriteRenderer(
                "Glow", root, glowSprite, sparkMaterial, SparkColors[i], 48);
            glow.transform.localScale = new Vector3(glowScale, glowScale, 1f);
            // The existing UI sprite's pivot is bottom-left; centre it under the flame core.
            glow.transform.localPosition = new Vector3(-0.32f * glowScale, -0.28f * glowScale, 0f);

            SpriteRenderer core = CreateSpriteRenderer(
                "Flame", root, coreSprite, sparkMaterial, SparkColors[i], 49);
            core.transform.localScale = new Vector3(coreScale, coreScale, 1f);
            core.transform.localPosition = new Vector3(-0.28f * coreScale, -0.28f * coreScale, -0.01f);

            visuals[i] = new SparkVisual
            {
                Root = root,
                Glow = glow,
                Core = core,
                BaseScale = root.localScale,
                Phase = i * 1.47f
            };
        }
    }

    private static SpriteRenderer CreateSpriteRenderer(
        string objectName, Transform parent, Sprite sprite, Material material, Color tint, int sortingOrder)
    {
        GameObject child = new GameObject(objectName, typeof(SpriteRenderer));
        Transform childTransform = child.transform;
        childTransform.SetParent(parent, false);

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = tint;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        return renderer;
    }
}
