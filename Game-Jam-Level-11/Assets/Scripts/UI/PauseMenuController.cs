using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Controls pre-authored pause/settings panels and delegates state changes to GameManager.</summary>
[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private InputActionReference cancelAction;

    private bool ownsCancelAction;

    private void Awake()
    {
        string missing = string.Empty;
        if (gameManager == null) missing += "GameManager, ";
        if (pauseRoot == null) missing += "Pause Root, ";
        if (settingsRoot == null) missing += "Settings Root, ";
        if (resumeButton == null) missing += "Resume button, ";
        if (settingsButton == null) missing += "Settings button, ";
        if (restartButton == null) missing += "Restart button, ";
        if (settingsBackButton == null) missing += "Settings Back button, ";

        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogError("PauseMenuController: assign references in the Inspector: " + missing.TrimEnd(' ', ','), this);
            enabled = false;
            return;
        }

        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(OpenSettings);
        restartButton.onClick.AddListener(Restart);
        settingsBackButton.onClick.AddListener(ReturnToPause);

        if (menuButton != null)
            menuButton.onClick.AddListener(ReturnToMainMenu);

        HidePanels();
    }

    private void OnEnable()
    {
        InputAction action = cancelAction != null ? cancelAction.action : null;
        if (action != null && !action.enabled)
        {
            action.Enable();
            ownsCancelAction = true;
        }
    }

    private void Update()
    {
        InputAction action = cancelAction != null ? cancelAction.action : null;
        bool cancelPressed = action != null && action.enabled
            ? action.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (!cancelPressed || gameManager == null)
            return;

        if (gameManager.State == GameManager.GameState.Playing)
        {
            gameManager.PauseGame();
        }
        else if (gameManager.State == GameManager.GameState.Paused)
        {
            if (settingsRoot.activeSelf)
                ShowPause();
            else
                gameManager.ResumeGame();
        }
    }

    public void ShowPause()
    {
        if (gameManager != null && gameManager.State != GameManager.GameState.Paused)
            return;

        SetPanel(settingsRoot, false);
        SetPanel(pauseRoot, true);
    }

    public void HidePanels()
    {
        SetPanel(settingsRoot, false);
        SetPanel(pauseRoot, false);
    }

    private void Resume()
    {
        if (gameManager != null)
            gameManager.ResumeGame();
    }

    private void OpenSettings()
    {
        if (gameManager == null || gameManager.State != GameManager.GameState.Paused)
            return;

        SetPanel(pauseRoot, false);
        SetPanel(settingsRoot, true);
    }

    private void ReturnToPause()
    {
        if (gameManager == null || gameManager.State != GameManager.GameState.Paused)
            return;

        ShowPause();
    }

    private void Restart()
    {
        if (gameManager != null && gameManager.State == GameManager.GameState.Paused)
            gameManager.Retry();
    }

    private void ReturnToMainMenu()
    {
        if (gameManager != null && gameManager.State == GameManager.GameState.Paused)
            gameManager.ReturnToMainMenu();
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }

    private void OnDisable()
    {
        if (ownsCancelAction && cancelAction != null && cancelAction.action != null)
            cancelAction.action.Disable();

        ownsCancelAction = false;
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(ReturnToPause);
        if (menuButton != null) menuButton.onClick.RemoveListener(ReturnToMainMenu);
    }
}
