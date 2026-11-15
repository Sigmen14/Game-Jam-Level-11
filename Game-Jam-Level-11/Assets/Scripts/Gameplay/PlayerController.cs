using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the Player action map and moves the pre-placed Rigidbody2D.
/// Holding Protect reduces movement speed; CoverZone supplies cover state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 5.2f;
    [SerializeField, Range(0.05f, 1f)] private float protectionSpeedMultiplier = 0.45f;

    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject protectionVisual;

    public bool IsProtecting { get; private set; }
    public bool IsInCover { get; private set; }
    public bool IsGameplayActive => gameplayActive;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction protectAction;
    private Vector2 moveInput;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float spawnBodyRotation;
    private bool gameplayActive;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
            spawnBodyRotation = body.rotation;

        if (inputActions == null)
        {
            Debug.LogError("PlayerController: assign InputSystem_Actions in the Inspector.", this);
            return;
        }

        playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap == null)
        {
            Debug.LogError("PlayerController: the Input Actions asset has no Player map.", this);
            return;
        }

        moveAction = playerMap.FindAction("Move", false);
        protectAction = playerMap.FindAction("Protect", false);
        if (moveAction == null || protectAction == null)
            Debug.LogError("PlayerController: Player map must contain Move and Protect actions.", this);

        SetGameplayActive(false);
    }

    private void Update()
    {
        if (!gameplayActive)
        {
            moveInput = Vector2.zero;
            IsProtecting = false;
            SetProtectionVisual(false);
            return;
        }

        // Movement must not depend on the optional Protect action being present.
        Vector2 actionInput = moveAction != null && moveAction.enabled
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        // Keep keyboard movement reliable even if an action map/binding was not enabled
        // by the active scene or its Input Actions asset is out of sync.
        if (actionInput.sqrMagnitude < 0.0001f)
        {
            actionInput = ReadKeyboardMovement();
        }

        moveInput = Vector2.ClampMagnitude(actionInput, 1f);
        IsProtecting = protectAction != null && protectAction.enabled &&
                       protectAction.IsPressed();
        SetProtectionVisual(IsProtecting);
    }

    private static Vector2 ReadKeyboardMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                           (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                         (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (!gameplayActive)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float speed = movementSpeed * (IsProtecting ? protectionSpeedMultiplier : 1f);
        body.linearVelocity = moveInput * speed;
    }

    public void SetGameplayActive(bool isActive)
    {
        gameplayActive = isActive;

        if (playerMap != null)
        {
            if (isActive) playerMap.Enable();
            else playerMap.Disable();
        }

        if (!isActive)
        {
            moveInput = Vector2.zero;
            IsProtecting = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        SetProtectionVisual(isActive && IsProtecting);
    }

    public void SetInCover(bool isInCover)
    {
        IsInCover = isInCover;
    }

    public void ResetPosition()
    {
        moveInput = Vector2.zero;
        IsProtecting = false;
        IsInCover = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = spawnPosition;
            body.rotation = spawnBodyRotation;
        }

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        SetProtectionVisual(false);
    }

    private void SetProtectionVisual(bool isVisible)
    {
        if (protectionVisual != null && protectionVisual.activeSelf != isVisible)
            protectionVisual.SetActive(isVisible);
    }

    private void OnDisable()
    {
        if (playerMap != null)
            playerMap.Disable();

        gameplayActive = false;
        IsProtecting = false;
        moveInput = Vector2.zero;
        SetProtectionVisual(false);
    }

    private void OnValidate()
    {
        protectionSpeedMultiplier = Mathf.Clamp(protectionSpeedMultiplier, 0.05f, 1f);
    }
}
