using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the existing Player action map and moves the pre-placed Rigidbody2D.
/// Holding Protect reduces movement speed; cover state is supplied by CoverZone.
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
    public bool IsGameplayActive => _gameplayActive;

    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _protectAction;
    private Vector2 _moveInput;
    private Vector3 _spawnPosition;
    private bool _gameplayActive;

    private void Awake()
    {
        _spawnPosition = transform.position;

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (inputActions == null)
        {
            Debug.LogError("PlayerController: assign the InputSystem_Actions asset in the Inspector.", this);
            return;
        }

        _playerMap = inputActions.FindActionMap("Player", false);
        if (_playerMap == null)
        {
            Debug.LogError("PlayerController: the assigned Input Actions asset has no 'Player' map.", this);
            return;
        }

        _moveAction = _playerMap.FindAction("Move", false);
        _protectAction = _playerMap.FindAction("Protect", false);

        if (_moveAction == null || _protectAction == null)
        {
            Debug.LogError("PlayerController: add Move and Protect actions to the Player map.", this);
        }

        SetGameplayActive(false);
    }

    private void Update()
    {
        if (!_gameplayActive || _moveAction == null || _protectAction == null)
        {
            _moveInput = Vector2.zero;
            SetProtectionVisual(false);
            return;
        }

        _moveInput = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
        IsProtecting = _protectAction.IsPressed();
        SetProtectionVisual(IsProtecting);
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        if (!_gameplayActive)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float speed = movementSpeed * (IsProtecting ? protectionSpeedMultiplier : 1f);
        body.linearVelocity = _moveInput * speed;
    }

    public void SetGameplayActive(bool isActive)
    {
        _gameplayActive = isActive;

        if (_playerMap != null)
        {
            if (isActive)
            {
                _playerMap.Enable();
            }
            else
            {
                _playerMap.Disable();
            }
        }

        if (!isActive)
        {
            _moveInput = Vector2.zero;
            IsProtecting = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
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
        _moveInput = Vector2.zero;
        IsProtecting = false;
        IsInCover = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = _spawnPosition;
        }

        transform.position = _spawnPosition;
        SetProtectionVisual(false);
    }

    private void SetProtectionVisual(bool isVisible)
    {
        if (protectionVisual != null && protectionVisual.activeSelf != isVisible)
        {
            protectionVisual.SetActive(isVisible);
        }
    }

    private void OnDisable()
    {
        if (_playerMap != null)
        {
            _playerMap.Disable();
        }

        _gameplayActive = false;
        IsProtecting = false;
        _moveInput = Vector2.zero;
        SetProtectionVisual(false);
    }

    private void OnValidate()
    {
        protectionSpeedMultiplier = Mathf.Clamp(protectionSpeedMultiplier, 0.05f, 1f);
    }
}
