using System;
using UnityEngine;

/// <summary>
/// A pre-placed fuel point. Collection disables only its visual and collider;
/// the same scene instance becomes available again after its cooldown.
/// </summary>
[DisallowMultipleComponent]
public sealed class FuelPickup : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField, Min(0f)] private float fuelAmount = 25f;
    [SerializeField, Min(0f)] private float respawnDelay = 8f;

    [Header("References")]
    [SerializeField] private FlameMeter flameMeter;
    [SerializeField] private Collider2D pickupCollider;
    [SerializeField] private SpriteRenderer pickupVisual;
    [SerializeField] private AudioSource pickupAudio;

    /// <summary>Raised only when this pickup actually increases the player's fuel.</summary>
    public event Action FuelCollected;

    public bool IsAvailable => !_isOnCooldown;
    private bool _isOnCooldown;
    private float _cooldownRemaining;

    private void Awake()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider2D>();
        }

        if (pickupVisual == null)
        {
            pickupVisual = GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (!_isOnCooldown)
        {
            return;
        }

        _cooldownRemaining -= Time.deltaTime;
        if (_cooldownRemaining <= 0f)
        {
            SetAvailable(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (_isOnCooldown || player == null || !player.IsGameplayActive)
        {
            return;
        }

        if (flameMeter == null)
        {
            Debug.LogError("FuelPickup: assign FlameMeter on this scene instance.", this);
            return;
        }

        float previousFuel = flameMeter.Current;
        flameMeter.AddFuel(fuelAmount);
        if (flameMeter.Current > previousFuel)
        {
            FuelCollected?.Invoke();
        }

        if (pickupAudio != null)
        {
            pickupAudio.Play();
        }

        _isOnCooldown = true;
        _cooldownRemaining = respawnDelay;
        SetAvailable(false);
    }

    public void ResetPickup()
    {
        _cooldownRemaining = 0f;
        _isOnCooldown = false;
        SetAvailable(true);
    }

    private void SetAvailable(bool isAvailable)
    {
        _isOnCooldown = !isAvailable;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = isAvailable;
        }

        if (pickupVisual != null)
        {
            pickupVisual.enabled = isAvailable;
        }
    }

    private void OnValidate()
    {
        fuelAmount = Mathf.Max(0f, fuelAmount);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
}
