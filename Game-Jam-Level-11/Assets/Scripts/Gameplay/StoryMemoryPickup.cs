using UnityEngine;

/// <summary>
/// Adds one story fragment to an existing fuel pickup, or acts as a standalone
/// non-fuel memory collectible. Fuel pickups are observed through their existing
/// FuelCollected event; no fuel values, cooldowns or collider rules are changed.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryMemoryPickup : MonoBehaviour
{
    [SerializeField] private string fragmentId;
    [SerializeField] private StoryMemoryManager memoryManager;
    [SerializeField] private FuelPickup fuelPickup;
    [SerializeField] private TextMesh markerText;
    [SerializeField] private Collider2D standaloneCollider;

    private void Awake()
    {
        if (fuelPickup == null)
            fuelPickup = GetComponent<FuelPickup>();

        if (fuelPickup != null)
            fuelPickup.FuelCollected += HandleFuelCollected;
    }

    private void Start()
    {
        RefreshMarker();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // FuelPickup owns its trigger and raises FuelCollected only after fuel
        // actually increases. This path is used only by the standalone fifth shard.
        if (fuelPickup != null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsGameplayActive || memoryManager == null)
            return;

        UnlockFragment();
    }

    private void HandleFuelCollected()
    {
        UnlockFragment();
    }

    private void UnlockFragment()
    {
        if (memoryManager == null || !memoryManager.TryCollect(fragmentId))
        {
            RefreshMarker();
            return;
        }

        if (fuelPickup == null && standaloneCollider != null)
            standaloneCollider.enabled = false;

        RefreshMarker();
    }

    private void RefreshMarker()
    {
        bool alreadyCollected = memoryManager != null && memoryManager.HasFragment(fragmentId);

        if (markerText != null)
            markerText.gameObject.SetActive(!alreadyCollected);

        if (fuelPickup == null && standaloneCollider != null)
            standaloneCollider.enabled = !alreadyCollected;
    }

    private void OnDestroy()
    {
        if (fuelPickup != null)
            fuelPickup.FuelCollected -= HandleFuelCollected;
    }
}
