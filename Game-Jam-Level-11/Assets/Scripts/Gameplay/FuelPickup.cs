using UnityEngine;

/// <summary>
/// A pre-placed fuel pickup that temporarily hides and later restores itself.
/// </summary>
[DisallowMultipleComponent]
public sealed class FuelPickup : MonoBehaviour
{
    [Header("Pickup references")]
    [SerializeField] private Renderer pickupRenderer;
    [SerializeField] private Collider2D pickupCollider;

    [Header("Pickup settings")]
    [SerializeField, Min(0f)] private float fuelAmount = 25f;
    [SerializeField, Min(0f)] private float respawnDelay = 5f;

    private bool isAvailable = true;
    private float respawnTimer;

    private void Reset()
    {
        // Populate same-object references when this component is first added in the Editor.
        pickupRenderer = GetComponent<Renderer>();
        pickupCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (pickupRenderer == null)
        {
            Debug.LogError("FuelPickup requires its Renderer reference to be assigned.", this);
        }

        if (pickupCollider == null)
        {
            Debug.LogError("FuelPickup requires its Collider2D reference to be assigned.", this);
        }
    }

    private void Update()
    {
        if (isAvailable)
        {
            return;
        }

        respawnTimer -= Time.deltaTime;

        if (respawnTimer <= 0f)
        {
            ResetPickup();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAvailable)
        {
            return;
        }

        // Supports a player whose collider is on a child object while FlameMeter is on the root.
        FlameMeter flameMeter = other.GetComponentInParent<FlameMeter>();

        if (flameMeter == null)
        {
            return;
        }

        flameMeter.AddFuel(fuelAmount);

        isAvailable = false;
        respawnTimer = Mathf.Max(0f, respawnDelay);
        SetPickupComponentsEnabled(false);

        if (respawnTimer <= 0f)
        {
            ResetPickup();
        }
    }

    /// <summary>Restores this same pickup's Renderer and Collider2D.</summary>
    public void ResetPickup()
    {
        respawnTimer = 0f;
        isAvailable = true;
        SetPickupComponentsEnabled(true);
    }

    private void SetPickupComponentsEnabled(bool value)
    {
        if (pickupRenderer != null)
        {
            pickupRenderer.enabled = value;
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = value;
        }
    }

    private void OnValidate()
    {
        fuelAmount = Mathf.Max(0f, fuelAmount);
        respawnDelay = Mathf.Max(0f, respawnDelay);

        // Keep required references easy to configure in the Inspector without runtime lookups.
        if (pickupRenderer == null)
        {
            pickupRenderer = GetComponent<Renderer>();
        }

        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider2D>();
        }
    }
}
