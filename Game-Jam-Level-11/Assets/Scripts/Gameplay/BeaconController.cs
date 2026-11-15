using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// A pre-placed lighthouse that consumes the player's flame once, then protects
/// players inside its trigger from most (but not all) Grey Wind damage.
/// </summary>
[DisallowMultipleComponent]
public sealed class BeaconController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private FlameMeter flameMeter;
    [SerializeField, Min(0.1f)] private float activationCost = 25f;
    [SerializeField, Min(0.1f)] private float interactionRadius = 1.65f;

    [Header("Visuals")]
    [SerializeField] private Light2D beaconLight;
    [SerializeField] private SpriteRenderer flameVisual;
    [SerializeField] private SpriteRenderer lanternGlass;
    [SerializeField] private Color unlitGlassColor = new Color(0.32f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color litGlassColor = new Color(1f, 0.55f, 0.16f, 1f);
    [SerializeField, Min(0f)] private float lightPulseAmount = 0.08f;
    [SerializeField, Min(0f)] private float lightPulseSpeed = 2.4f;

    [Header("Interaction hint")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Text promptText;

    public bool IsLit { get; private set; }

    private PlayerController playerInSafeZone;
    private float baseLightIntensity;

    private void Awake()
    {
        if (beaconLight != null)
            baseLightIntensity = beaconLight.intensity;

        ApplyVisualState();
        SetPrompt(false, string.Empty);
    }

    private void Update()
    {
        bool playerIsClose = playerInSafeZone != null &&
            playerInSafeZone.IsGameplayActive &&
            (playerInSafeZone.transform.position - transform.position).sqrMagnitude <=
            interactionRadius * interactionRadius;

        if (!playerIsClose)
        {
            SetPrompt(false, string.Empty);
        }
        else if (IsLit)
        {
            SetPrompt(true, "МАЯК ЗАЖЖЁН\nСЕРЫЙ ВЕТЕР ОСЛАБЛЕН");
        }
        else if (flameMeter != null && flameMeter.Current > activationCost)
        {
            SetPrompt(true, "E  —  ЗАЖЕЧЬ МАЯК\n−" + Mathf.CeilToInt(activationCost) + " ОГНЯ");
        }
        else
        {
            SetPrompt(true, "НЕДОСТАТОЧНО ОГНЯ\nНУЖНО " + Mathf.CeilToInt(activationCost));
        }

        if (playerIsClose && !IsLit)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                TryActivate();
        }

        if (IsLit && beaconLight != null)
        {
            beaconLight.intensity = baseLightIntensity +
                Mathf.Sin(Time.time * lightPulseSpeed) * lightPulseAmount;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || (playerInSafeZone != null && playerInSafeZone != player))
            return;

        playerInSafeZone = player;
        playerInSafeZone.SetInBeaconSafeZone(IsLit);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != playerInSafeZone)
            return;

        playerInSafeZone.SetInBeaconSafeZone(false);
        playerInSafeZone = null;
        SetPrompt(false, string.Empty);
    }

    private void TryActivate()
    {
        if (IsLit || flameMeter == null)
            return;

        if (!flameMeter.TrySpendFuel(activationCost))
            return;

        IsLit = true;
        ApplyVisualState();

        if (playerInSafeZone != null)
            playerInSafeZone.SetInBeaconSafeZone(true);
    }

    /// <summary>Restores the unlit state for a new match, Retry, or return to menu.</summary>
    public void ResetBeacon()
    {
        IsLit = false;

        if (playerInSafeZone != null)
            playerInSafeZone.SetInBeaconSafeZone(false);

        ApplyVisualState();
        SetPrompt(false, string.Empty);
    }

    private void ApplyVisualState()
    {
        if (beaconLight != null)
        {
            beaconLight.enabled = IsLit;
            beaconLight.intensity = baseLightIntensity;
        }

        if (flameVisual != null)
            flameVisual.enabled = IsLit;

        if (lanternGlass != null)
            lanternGlass.color = IsLit ? litGlassColor : unlitGlassColor;
    }

    private void SetPrompt(bool isVisible, string message)
    {
        if (promptRoot != null && promptRoot.activeSelf != isVisible)
            promptRoot.SetActive(isVisible);

        if (isVisible && promptText != null && promptText.text != message)
            promptText.text = message;
    }

    private void OnDisable()
    {
        if (playerInSafeZone != null)
            playerInSafeZone.SetInBeaconSafeZone(false);

        SetPrompt(false, string.Empty);
    }

    private void OnValidate()
    {
        activationCost = Mathf.Max(0.1f, activationCost);
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        lightPulseAmount = Mathf.Max(0f, lightPulseAmount);
        lightPulseSpeed = Mathf.Max(0f, lightPulseSpeed);
    }
}
