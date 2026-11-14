using UnityEngine;

/// <summary>
/// Owns the match state and coordinates already-existing gameplay and UI objects.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Ready,
        Playing,
        Won,
        Lost
    }

    [Header("Match")]
    [SerializeField, Min(1f)] private float matchDuration = 180f;

    [Header("Scene references")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private FlameMeter flameMeter;
    [SerializeField] private WindController windController;
    [SerializeField] private FuelPickup[] fuelPickups;
    [SerializeField] private HUDView hudView;

    public GameState State => _state;
    public float RemainingTime => _remainingTime;
    public float ElapsedTime => Mathf.Max(0f, matchDuration - _remainingTime);

    private GameState _state = GameState.Ready;
    private float _remainingTime;

    private void Awake()
    {
        if (flameMeter != null)
        {
            flameMeter.FlameDepleted += HandleFlameDepleted;
        }
    }

    private void Start()
    {
        if (hudView != null)
        {
            hudView.Bind(this, flameMeter, windController);
        }

        StartGame();
    }

    private void Update()
    {
        if (_state != GameState.Playing)
        {
            return;
        }

        _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);

        if (hudView != null)
        {
            hudView.SetTimer(_remainingTime);
        }

        if (_remainingTime <= 0f)
        {
            FinishMatch(true);
        }
    }

    public void StartGame()
    {
        if (_state == GameState.Playing)
        {
            return;
        }

        ResetGameplayObjects();
        _remainingTime = matchDuration;
        _state = GameState.Playing;

        if (hudView != null)
        {
            hudView.ShowGameplay();
            hudView.SetTimer(_remainingTime);
        }

        if (flameMeter != null)
        {
            flameMeter.SetRunning(true);
        }

        if (playerController != null)
        {
            playerController.SetGameplayActive(true);
        }

        if (windController != null)
        {
            windController.SetRunning(true);
        }
    }

    /// <summary>Restarts the active match using the existing scene objects.</summary>
    public void Retry()
    {
        if (_state == GameState.Playing)
        {
            return;
        }

        StartGame();
    }

    private void ResetToReady()
    {
        _state = GameState.Ready;
        _remainingTime = matchDuration;

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

        ResetPickups();

        if (hudView != null)
        {
            hudView.ShowReady();
            hudView.SetTimer(_remainingTime);
        }
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

        ResetPickups();
    }

    private void ResetPickups()
    {
        if (fuelPickups == null)
        {
            return;
        }

        for (int i = 0; i < fuelPickups.Length; i++)
        {
            if (fuelPickups[i] != null)
            {
                fuelPickups[i].ResetPickup();
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
        {
            return;
        }

        _state = didWin ? GameState.Won : GameState.Lost;

        if (playerController != null)
        {
            playerController.SetGameplayActive(false);
        }

        if (flameMeter != null)
        {
            flameMeter.SetRunning(false);
        }

        if (windController != null)
        {
            windController.SetRunning(false);
        }

        if (hudView != null)
        {
            hudView.ShowResult(didWin, ElapsedTime);
        }
    }

    private void OnDestroy()
    {
        if (flameMeter != null)
        {
            flameMeter.FlameDepleted -= HandleFlameDepleted;
        }
    }

    private void OnValidate()
    {
        matchDuration = Mathf.Max(1f, matchDuration);
    }
}
