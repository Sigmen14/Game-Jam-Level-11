using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A persistent in-level beacon. The player must approach and press E to spend fuel.
/// Its lit radius partially reduces Grey Wind damage for as long as the player remains nearby.
/// </summary>
[DisallowMultipleComponent]
public sealed class BeaconController : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer towerRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private SpriteRenderer flameRenderer;
    [SerializeField] private Light2D beaconLight;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRadius = 1.8f;
    [SerializeField, Min(0.1f)] private float fuelCost = 25f;

    [Header("Grey Wind shelter")]
    [SerializeField, Min(0.1f)] private float safeRadius = 4.25f;

    [Header("Light animation")]
    [SerializeField, Min(0f)] private float lightIntensity = 1.35f;
    [SerializeField, Min(0f)] private float lightRadius = 4.8f;

    public bool IsActivated => _activated;

    private PlayerController _playerController;
    private FlameMeter _flameMeter;
    private GameManager _gameManager;
    private bool _activated;
    private bool _showPrompt;
    private bool _notEnoughFuel;
    private bool _insideSafeZone;
    private float _activationMessageTimer;
    private Vector3 _baseGlowScale = Vector3.one;
    private Vector3 _baseFlameScale = Vector3.one;

    private void Awake()
    {
        if (glowRenderer != null)
            _baseGlowScale = glowRenderer.transform.localScale;
        if (flameRenderer != null)
            _baseFlameScale = flameRenderer.transform.localScale;

        ApplyVisualState(false);
    }

    /// <summary>Called by the scene GameManager so this prefab uses the active game systems.</summary>
    public void Bind(PlayerController player, FlameMeter flame, GameManager gameManager)
    {
        _playerController = player;
        _flameMeter = flame;
        _gameManager = gameManager;
    }

    private void Update()
    {
        if (_playerController == null || _flameMeter == null || _gameManager == null)
            return;

        bool gameplayActive = _gameManager.State == GameManager.GameState.Playing &&
                              _playerController.IsGameplayActive;
        if (!gameplayActive)
        {
            _showPrompt = false;
            _notEnoughFuel = false;
            SetSafeZone(false);
            return;
        }

        if (_activated)
        {
            SetSafeZone(IsPlayerInsideSafeRadius());
            if (_activationMessageTimer > 0f)
                _activationMessageTimer = Mathf.Max(0f, _activationMessageTimer - Time.deltaTime);

            AnimateFire();
            return;
        }

        float distance = Vector2.Distance(_playerController.transform.position, transform.position);
        _showPrompt = distance <= interactionRadius;
        if (!_showPrompt)
            _notEnoughFuel = false;

        Keyboard keyboard = Keyboard.current;
        if (_showPrompt && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            if (_flameMeter.TryConsumeFuel(fuelCost))
                ActivateBeacon();
            else
                _notEnoughFuel = true;
        }
    }

    private void ActivateBeacon()
    {
        _activated = true;
        _showPrompt = false;
        _notEnoughFuel = false;
        _activationMessageTimer = 3.5f;
        ApplyVisualState(true);
        SetSafeZone(IsPlayerInsideSafeRadius());
    }

    private bool IsPlayerInsideSafeRadius()
    {
        return _playerController != null &&
               Vector2.Distance(_playerController.transform.position, transform.position) <= safeRadius;
    }

    private void SetSafeZone(bool isInside)
    {
        if (_insideSafeZone == isInside)
            return;

        _insideSafeZone = isInside;
        if (_flameMeter != null)
            _flameMeter.SetBeaconShelter(this, isInside);
    }

    private void ApplyVisualState(bool isLit)
    {
        if (towerRenderer != null)
            towerRenderer.color = isLit
                ? new Color(1f, 0.96f, 0.86f, 1f)
                : Color.white;
        if (glowRenderer != null)
            glowRenderer.enabled = isLit;
        if (flameRenderer != null)
            flameRenderer.enabled = isLit;
        if (beaconLight != null)
        {
            beaconLight.enabled = isLit;
            if (isLit)
            {
                beaconLight.intensity = lightIntensity;
                beaconLight.pointLightOuterRadius = lightRadius;
                beaconLight.pointLightInnerRadius = lightRadius * 0.18f;
            }
        }
    }

    private void AnimateFire()
    {
        float flicker = 0.94f + Mathf.PerlinNoise(Time.time * 8f, 0.37f) * 0.13f;
        if (beaconLight != null)
        {
            beaconLight.intensity = lightIntensity * flicker;
            beaconLight.pointLightOuterRadius = lightRadius * (0.98f + flicker * 0.025f);
        }

        float pulse = 1f + Mathf.Sin(Time.time * 2.4f) * 0.035f;
        if (glowRenderer != null)
            glowRenderer.transform.localScale = _baseGlowScale * pulse;
        if (flameRenderer != null)
            flameRenderer.transform.localScale = _baseFlameScale * (0.96f + flicker * 0.08f);
    }

    /// <summary>Restores the unlit state between rounds and when returning to the menu.</summary>
    public void ResetBeacon()
    {
        SetSafeZone(false);
        _activated = false;
        _showPrompt = false;
        _notEnoughFuel = false;
        _activationMessageTimer = 0f;
        if (glowRenderer != null)
            glowRenderer.transform.localScale = _baseGlowScale;
        if (flameRenderer != null)
            flameRenderer.transform.localScale = _baseFlameScale;
        ApplyVisualState(false);
    }

    private void OnGUI()
    {
        if (_gameManager == null || _gameManager.State != GameManager.GameState.Playing)
            return;

        if (!_showPrompt && _activationMessageTimer <= 0f)
            return;

        bool activatedMessage = _activationMessageTimer > 0f;
        string heading = activatedMessage
            ? "THE OLD BEACON IS LIT"
            : _notEnoughFuel
                ? "NOT ENOUGH FUEL"
                : "PRESS [E] TO IGNITE THE BEACON";
        string detail = activatedMessage
            ? "Golden light weakens the Grey Wind nearby"
            : _notEnoughFuel
                ? "Find fuel — ignition requires " + Mathf.CeilToInt(fuelCost) + " fuel"
                : "Ignition cost: " + Mathf.CeilToInt(fuelCost) + " fuel";

        float panelWidth = Mathf.Min(500f, Screen.width - 28f);
        const float panelHeight = 76f;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f,
                              Screen.height - panelHeight - 28f,
                              panelWidth, panelHeight);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.43f, 0.13f, 1f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), Texture2D.whiteTexture);

        GUIStyle headingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        headingStyle.normal.textColor = activatedMessage || !_notEnoughFuel
            ? new Color(1f, 0.72f, 0.32f, 1f)
            : new Color(1f, 0.42f, 0.3f, 1f);

        GUIStyle detailStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13
        };
        detailStyle.normal.textColor = new Color(0.88f, 0.88f, 0.84f, 1f);

        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 30f), heading, headingStyle);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 39f, panel.width - 24f, 25f), detail, detailStyle);
        GUI.color = previousColor;
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        fuelCost = Mathf.Max(0.1f, fuelCost);
        safeRadius = Mathf.Max(interactionRadius, safeRadius);
        lightRadius = Mathf.Max(0f, lightRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);
    }
}
