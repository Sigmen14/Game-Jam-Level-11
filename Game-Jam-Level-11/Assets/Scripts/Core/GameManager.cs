using System;
using UnityEngine;

/// <summary>
/// Owns the complete game flow and coordinates the pre-placed gameplay/UI objects.
/// Runtime code only changes existing objects; it never creates or destroys them.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Won,
        Lost
    }

    [Header("Match")]
    [SerializeField, Min(1f)] private float matchDuration = 180f;

    [Header("Gameplay references")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private FlameMeter flameMeter;
    [SerializeField] private WindController windController;
    [SerializeField] private FuelPickup[] fuelPickups;
    [SerializeField] private CoverZone[] coverZones;
    [SerializeField] private BeaconController[] beacons;
    [SerializeField] private GameplayChallengeController routeDirector;

    [Header("Fuel pickup spawn area")]
    [SerializeField] private Vector2 fuelSpawnMin = new Vector2(-7.2f, -3.6f);
    [SerializeField] private Vector2 fuelSpawnMax = new Vector2(7.2f, 3.6f);
    [SerializeField, Min(0f)] private float minimumPickupSpacing = 2.1f;
    [SerializeField, Min(0f)] private float playerSpawnClearance = 2.4f;

    [Header("Living sparks visuals")]
    [SerializeField] private Sprite livingSparksGlowSprite;
    [SerializeField] private Sprite livingSparksCoreSprite;
    [SerializeField] private Material livingSparksMaterial;

    [Header("UI references")]
    [SerializeField] private HUDView hudView;
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private PauseMenuController pauseMenuController;
    [SerializeField] private EndingFlowController endingFlowController;

    public GameState State => _state;
    public float RemainingTime => _remainingTime;
    public float ElapsedTime => Mathf.Clamp(matchDuration - _remainingTime, 0f, matchDuration);
    public float MatchDuration => matchDuration;

    public event Action<GameState> StateChanged;

    private GameState _state = GameState.MainMenu;
    private float _remainingTime;
    private LivingSparksPickup activeLivingSparks;
    private float livingSparksSpawnCountdown;
    private bool livingSparksWarningShown;

    private const float LivingSparksFuelReward = 10f;
    private const float LivingSparksLifetime = 12f;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (flameMeter != null)
            flameMeter.FlameDepleted += HandleFlameDepleted;
    }

    private void Start()
    {
        if (mainMenuController == null)
        {
            Debug.LogError(
                "GameManager: MainMenuController is not assigned in Main.unity. " +
                "The game stays in MainMenu and PlayerController input remains inactive. " +
                "Run Game Jam > Setup Player Flame Visual Feedback to wire the menu.",
                this);
        }

        if (playerController == null)
        {
            Debug.LogError(
                "GameManager: PlayerController is not assigned. The player cannot be activated for gameplay.",
                this);
        }

        if (hudView != null)
            hudView.Bind(this, flameMeter, windController);

        if (endingFlowController != null)
            endingFlowController.Bind(this);

        if (beacons != null)
        {
            for (int i = 0; i < beacons.Length; i++)
            {
                if (beacons[i] != null)
                    beacons[i].Bind(playerController, flameMeter, this);
            }
        }

        if (playerController != null)
            playerController.BindGameplaySystems(windController);

        if (flameMeter != null)
            flameMeter.BindGameplaySystems(playerController, windController);

        if (routeDirector != null)
            routeDirector.Bind(playerController, flameMeter, hudView);

        ValidateChallengeSetup();
        ReturnToMainMenu();
    }

    private void ValidateChallengeSetup()
    {
        if (windController == null)
            Debug.LogError("GameManager: WindController is not assigned; Grey Wind events cannot run.", this);

        if (routeDirector == null)
            Debug.LogError("GameManager: RouteDirector is not assigned; route choice cannot run.", this);
    }

    private void Update()
    {
        if (_state != GameState.Playing)
            return;

        _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);

        if (hudView != null)
            hudView.SetTimer(_remainingTime);

        if (_remainingTime <= 0f)
        {
            FinishMatch(true);
            return;
        }

        UpdateLivingSparks();
    }

    /// <summary>
    /// Repairs the essential Player hierarchy switches before a round starts.
    /// This protects gameplay if the Player object, movement script, physics body,
    /// or collider was accidentally disabled in the Inspector.
    /// </summary>
    private void EnsurePlayerMovementReady()
    {
        if (playerController == null)
            return;

        GameObject playerObject = playerController.gameObject;
        if (!playerObject.activeSelf)
            playerObject.SetActive(true);

        if (!playerController.enabled)
            playerController.enabled = true;

        Rigidbody2D playerBody = playerObject.GetComponent<Rigidbody2D>();
        if (playerBody != null)
            playerBody.simulated = true;

        Collider2D playerCollider = playerObject.GetComponent<Collider2D>();
        if (playerCollider != null)
            playerCollider.enabled = true;
    }

    /// <summary>Starts a fresh 180-second session from the main menu or result screen.</summary>
    public void StartGame()
    {
        if (_state == GameState.Playing)
            return;

        EnsurePlayerMovementReady();
        Time.timeScale = 1f;
        if (endingFlowController != null)
            endingFlowController.Hide();

        RandomizeFuelPickupPositions();
        ResetGameplayObjects();

        _remainingTime = matchDuration;
        livingSparksSpawnCountdown = UnityEngine.Random.Range(25f, 35f);
        livingSparksWarningShown = false;
        ChangeState(GameState.Playing);

        if (mainMenuController != null)
            mainMenuController.HideMainMenu();

        if (pauseMenuController != null)
            pauseMenuController.HidePanels();

        if (hudView != null)
        {
            hudView.ShowGameplay();
            hudView.SetTimer(_remainingTime);
        }

        if (playerController != null)
            playerController.SetGameplayActive(true);

        if (flameMeter != null)
            flameMeter.SetRunning(true);

        if (windController != null)
            windController.SetRunning(true);

        SetChallengeSystemsRunning(true);
    }

    /// <summary>Restarts the round with all resources and world pickups restored.</summary>
    public void Retry()
    {
        if (_state == GameState.Playing)
            return;

        StartGame();
    }

    public void PauseGame()
    {
        if (_state != GameState.Playing)
            return;

        Time.timeScale = 0f;
        ChangeState(GameState.Paused);

        if (pauseMenuController != null)
            pauseMenuController.ShowPause();
    }

    public void ResumeGame()
    {
        if (_state != GameState.Paused)
            return;

        Time.timeScale = 1f;
        ChangeState(GameState.Playing);

        if (pauseMenuController != null)
            pauseMenuController.HidePanels();
    }

    /// <summary>Returns to the in-scene main menu without reloading or duplicating objects.</summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (endingFlowController != null)
            endingFlowController.Hide();

        ResetGameplayObjects();
        _remainingTime = matchDuration;
        ChangeState(GameState.MainMenu);

        if (pauseMenuController != null)
            pauseMenuController.HidePanels();

        if (hudView != null)
            hudView.ShowMainMenu();

        if (mainMenuController != null)
            mainMenuController.ShowMainMenu();
    }

    private void RandomizeFuelPickupPositions()
    {
        if (fuelPickups == null || fuelPickups.Length == 0)
            return;

        Vector2 playerStart = playerController != null
            ? (Vector2)playerController.transform.position
            : Vector2.zero;
        Vector2[] assignedPositions = new Vector2[fuelPickups.Length];
        int assignedCount = 0;

        for (int i = 0; i < fuelPickups.Length; i++)
        {
            FuelPickup pickup = fuelPickups[i];
            if (pickup == null)
                continue;

            Vector2 candidate = pickup.transform.position;
            bool foundValidPosition = false;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                candidate = new Vector2(
                    UnityEngine.Random.Range(fuelSpawnMin.x, fuelSpawnMax.x),
                    UnityEngine.Random.Range(fuelSpawnMin.y, fuelSpawnMax.y));

                if (Vector2.Distance(candidate, playerStart) < playerSpawnClearance)
                    continue;

                bool overlapsAnotherPickup = false;
                for (int j = 0; j < assignedCount; j++)
                {
                    if (Vector2.Distance(candidate, assignedPositions[j]) < minimumPickupSpacing)
                    {
                        overlapsAnotherPickup = true;
                        break;
                    }
                }

                if (!overlapsAnotherPickup)
                {
                    foundValidPosition = true;
                    break;
                }
            }

            if (!foundValidPosition)
                Debug.LogWarning("GameManager: could not find a fully separated fuel pickup spawn; using the last candidate.", pickup);

            pickup.transform.position = new Vector3(candidate.x, candidate.y, pickup.transform.position.z);
            assignedPositions[assignedCount++] = candidate;
        }
    }

    private void ResetGameplayObjects()
    {
        ClearLivingSparks();
        livingSparksSpawnCountdown = 0f;
        livingSparksWarningShown = false;
        SetChallengeSystemsRunning(false);
        if (playerController != null)
        {
            playerController.SetGameplayActive(false);
            playerController.ResetPosition();
        }

        if (flameMeter != null)
        {
            flameMeter.SetRunning(false);
            flameMeter.ResetMeter();
        }

        if (windController != null)
        {
            windController.SetRunning(false);
            windController.ResetCycle();
        }

        if (fuelPickups != null)
        {
            for (int i = 0; i < fuelPickups.Length; i++)
            {
                if (fuelPickups[i] != null)
                    fuelPickups[i].ResetPickup();
            }
        }

        if (coverZones != null)
        {
            for (int i = 0; i < coverZones.Length; i++)
            {
                if (coverZones[i] != null)
                    coverZones[i].ResetZone();
            }
        }

        if (beacons != null)
        {
            for (int i = 0; i < beacons.Length; i++)
            {
                if (beacons[i] != null)
                    beacons[i].ResetBeacon();
            }
        }
    }

    private void SetChallengeSystemsRunning(bool isRunning)
    {
        if (routeDirector != null)
            routeDirector.SetRunning(isRunning);
    }

    private void UpdateLivingSparks()
    {
        if (livingSparksSpawnCountdown > 0f)
        {
            livingSparksSpawnCountdown -= Time.deltaTime;
            if (!livingSparksWarningShown && livingSparksSpawnCountdown <= 2f)
            {
                livingSparksWarningShown = true;
                if (hudView != null)
                    hudView.ShowPickupNotification("ВПЕРЕДИ: ЖИВЫЕ ИСКРЫ", 2.5f);
            }
        }

        // A missed group keeps the next event queued until it expires.
        if (livingSparksSpawnCountdown > 0f || activeLivingSparks != null)
            return;

        SpawnLivingSparks();
        livingSparksSpawnCountdown = UnityEngine.Random.Range(35f, 45f);
        livingSparksWarningShown = false;
    }

    private void SpawnLivingSparks()
    {
        if (playerController == null || flameMeter == null)
            return;

        if (livingSparksGlowSprite == null || livingSparksCoreSprite == null || livingSparksMaterial == null)
        {
            Debug.LogError("GameManager: assign the existing Last Ember spark sprites and FlameSparkUnlit material.", this);
            return;
        }

        if (!TryGetVisibleLivingSparksPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning("GameManager: could not find a visible camera position for Living Sparks.", this);
            return;
        }

        GameObject group = new GameObject("LivingSparks_Group");
        group.transform.position = spawnPosition;

        CircleCollider2D trigger = group.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 1.85f;

        activeLivingSparks = group.AddComponent<LivingSparksPickup>();
        activeLivingSparks.Initialize(
            flameMeter, hudView, LivingSparksFuelReward, LivingSparksLifetime,
            livingSparksGlowSprite, livingSparksCoreSprite, livingSparksMaterial);
        if (hudView != null)
            hudView.ShowPickupNotification("ЖИВЫЕ ИСКРЫ ПОЯВИЛИСЬ — СОБЕРИ ИХ!", 2.5f);
    }

    private bool TryGetVisibleLivingSparksPosition(out Vector3 position)
    {
        position = Vector3.zero;
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
            return false;

        float cameraDepth = Mathf.Abs(gameplayCamera.transform.position.z);
        Vector3 bottomLeft = gameplayCamera.ViewportToWorldPoint(
            new Vector3(0.10f, 0.12f, cameraDepth));
        Vector3 topRight = gameplayCamera.ViewportToWorldPoint(
            new Vector3(0.90f, 0.88f, cameraDepth));

        const float edgePadding = 1.05f;
        float minX = bottomLeft.x + edgePadding;
        float maxX = topRight.x - edgePadding;
        float minY = bottomLeft.y + edgePadding;
        float maxY = topRight.y - edgePadding;
        if (maxX <= minX || maxY <= minY)
            return false;

        Vector2 playerPosition = playerController.transform.position;
        const float preferredForwardGap = 2.6f;
        const float minimumPlayerGap = 1.55f;

        float spawnX = playerPosition.x + preferredForwardGap;
        if (spawnX > maxX)
            spawnX = playerPosition.x - preferredForwardGap;

        spawnX = Mathf.Clamp(spawnX, minX, maxX);
        if (Mathf.Abs(spawnX - playerPosition.x) < minimumPlayerGap)
        {
            if (playerPosition.x + minimumPlayerGap <= maxX)
                spawnX = playerPosition.x + minimumPlayerGap;
            else if (playerPosition.x - minimumPlayerGap >= minX)
                spawnX = playerPosition.x - minimumPlayerGap;
        }

        float spawnY = Mathf.Clamp(
            playerPosition.y + UnityEngine.Random.Range(-0.9f, 0.9f),
            minY,
            maxY);

        if (Vector2.Distance(playerPosition, new Vector2(spawnX, spawnY)) < minimumPlayerGap)
        {
            float upperY = playerPosition.y + minimumPlayerGap;
            float lowerY = playerPosition.y - minimumPlayerGap;
            if (upperY <= maxY)
                spawnY = upperY;
            else if (lowerY >= minY)
                spawnY = lowerY;
        }

        position = new Vector3(spawnX, spawnY, 0f);
        return true;
    }

    private void ClearLivingSparks()
    {
        if (activeLivingSparks != null)
            Destroy(activeLivingSparks.gameObject);
        activeLivingSparks = null;
    }

    private void HandleFlameDepleted()
    {
        FinishMatch(false);
    }

    private void FinishMatch(bool didWin)
    {
        if (_state != GameState.Playing)
            return;

        Time.timeScale = 1f;
        ClearLivingSparks();

        if (playerController != null)
            playerController.SetGameplayActive(false);

        if (flameMeter != null)
            flameMeter.SetRunning(false);

        if (windController != null)
            windController.SetRunning(false);

        SetChallengeSystemsRunning(false);

        if (pauseMenuController != null)
            pauseMenuController.HidePanels();

        ChangeState(didWin ? GameState.Won : GameState.Lost);

        if (didWin && endingFlowController != null)
        {
            if (hudView != null)
                hudView.HideForEnding();

            endingFlowController.ShowChoice(ElapsedTime);
        }
        else if (hudView != null)
        {
            // A depleted flame keeps the existing loss/result flow unchanged.
            hudView.ShowResult(didWin, ElapsedTime);
        }
    }

    private void ChangeState(GameState nextState)
    {
        if (_state == nextState)
            return;

        _state = nextState;
        StateChanged?.Invoke(_state);
    }

    private void OnDestroy()
    {
        if (flameMeter != null)
            flameMeter.FlameDepleted -= HandleFlameDepleted;

        ClearLivingSparks();
        Time.timeScale = 1f;
    }

    private void OnValidate()
    {
        matchDuration = Mathf.Max(1f, matchDuration);
    }
}
