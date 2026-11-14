using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles 2D player movement and the held protection action.
/// Assign all references in the Inspector; the protection visual must already exist
/// as a child/object in the scene or player prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference protectAction;
    [SerializeField] private GameObject guardVisual;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float guardingSpeedMultiplier = 0.45f;

    private InputAction moveInputAction;
    private InputAction protectInputAction;

    private Vector2 moveInput;
    private bool controlEnabled = true;
    private bool isInCover;

    private bool disableMoveActionOnDisable;
    private bool disableProtectActionOnDisable;

    /// <summary>True while Protect is held and player control is enabled.</summary>
    public bool IsGuarding =>
        controlEnabled &&
        protectInputAction != null &&
        protectInputAction.IsPressed();

    /// <summary>True while the player is inside a cover volume.</summary>
    public bool IsInCover => isInCover;

    /// <summary>Cover or active guarding protects the player.</summary>
    public bool IsProtected => IsInCover || IsGuarding;

    private void Awake()
    {
        if (body == null)
        {
            Debug.LogError($"{nameof(PlayerController)} requires a Rigidbody2D reference.", this);
            enabled = false;
            return;
        }

        if (moveAction == null || moveAction.action == null ||
            protectAction == null || protectAction.action == null)
        {
            Debug.LogError(
                $"{nameof(PlayerController)} requires Move and Protect InputActionReferences.",
                this);
            enabled = false;
            return;
        }

        moveInputAction = moveAction.action;
        protectInputAction = protectAction.action;

        if (moveInputAction == protectInputAction)
        {
            Debug.LogError("Move and Protect must reference different Input Actions.", this);
            enabled = false;
        }

        if (guardVisual == gameObject)
        {
            Debug.LogError(
                "Guard Visual must reference a separate, pre-existing visual object, not the player root.",
                this);
            guardVisual = null;
        }
    }

    private void OnEnable()
    {
        if (moveInputAction == null || protectInputAction == null)
            return;

        // Do not take ownership of actions that another component already enabled.
        disableMoveActionOnDisable = !moveInputAction.enabled;
        disableProtectActionOnDisable = !protectInputAction.enabled;

        if (disableMoveActionOnDisable)
            moveInputAction.Enable();

        if (disableProtectActionOnDisable)
            protectInputAction.Enable();

        RefreshGuardVisual();
    }

    private void OnDisable()
    {
        if (disableMoveActionOnDisable && moveInputAction != null)
            moveInputAction.Disable();

        if (disableProtectActionOnDisable && protectInputAction != null)
            protectInputAction.Disable();

        disableMoveActionOnDisable = false;
        disableProtectActionOnDisable = false;
        moveInput = Vector2.zero;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (guardVisual != null && guardVisual.activeSelf)
            guardVisual.SetActive(false);
    }

    private void Update()
    {
        if (!controlEnabled || moveInputAction == null || !moveInputAction.enabled)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            moveInput = Vector2.ClampMagnitude(moveInputAction.ReadValue<Vector2>(), 1f);
        }

        RefreshGuardVisual();
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        float speed = movementSpeed * (IsGuarding ? guardingSpeedMultiplier : 1f);
        Vector2 direction = controlEnabled ? moveInput : Vector2.zero;

        // Drive the existing Rigidbody2D through the physics step.
        body.linearVelocity = direction * speed;
    }

    /// <summary>Enables or suppresses player input and movement.</summary>
    public void SetControlEnabled(bool isEnabled)
    {
        controlEnabled = isEnabled;

        if (!controlEnabled)
        {
            moveInput = Vector2.zero;

            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

        RefreshGuardVisual();
    }

    /// <summary>Sets whether the player is currently inside a cover area.</summary>
    public void SetInCover(bool inCover)
    {
        isInCover = inCover;
    }

    /// <summary>
    /// Restores the player's default gameplay state, clears movement/cover,
    /// stops the Rigidbody2D and turns off the protection visual.
    /// </summary>
    public void ResetPlayer()
    {
        controlEnabled = true;
        isInCover = false;
        moveInput = Vector2.zero;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        // Clear a held state so protection does not remain latched across a reset.
        if (protectInputAction != null && protectInputAction.enabled)
        {
            protectInputAction.Disable();
            protectInputAction.Enable();
        }

        RefreshGuardVisual();
    }

    private void RefreshGuardVisual()
    {
        if (guardVisual == null)
            return;

        bool shouldBeVisible = IsGuarding;
        if (guardVisual.activeSelf != shouldBeVisible)
            guardVisual.SetActive(shouldBeVisible);
    }
}
