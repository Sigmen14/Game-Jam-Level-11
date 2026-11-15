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

        ReturnToMainMenu();
    }

    private void Update()
    {
        if (_state != GameState.Playing)
            return;

        _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);

        if (hudView != null)
            hudView.SetTimer(_remainingTime);

        if (_remainingTime <= 0f)
            FinishMatch(true);
    }

    /// <summary>Starts a fresh 180-second session from the main menu or result screen.</summary>
    public void StartGame()
    {
        if (_state == GameState.Playing)
            return;

        Time.timeScale = 1f;
        if (endingFlowController != null)
            endingFlowController.Hide();

        ResetGameplayObjects();

        _remainingTime = matchDuration;
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

    private void ResetGameplayObjects()
    {
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

    private void HandleFlameDepleted()
    {
        FinishMatch(false);
    }

    private void FinishMatch(bool didWin)
    {
        if (_state != GameState.Playing)
            return;

        Time.timeScale = 1f;

        if (playerController != null)
            playerController.SetGameplayActive(false);

        if (flameMeter != null)
            flameMeter.SetRunning(false);

        if (windController != null)
            windController.SetRunning(false);

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

        Time.timeScale = 1f;
    }

    private void OnValidate()
    {
        matchDuration = Mathf.Max(1f, matchDuration);
    }
}
