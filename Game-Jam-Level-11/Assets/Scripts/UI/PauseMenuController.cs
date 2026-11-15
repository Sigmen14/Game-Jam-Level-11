using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the pre-authored pause and settings panels during an active match.</summary>
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
    [SerializeField] private string menuSceneName = "Menu";

    private bool _isPaused;

    private void Awake()
    {
        string missingReferences = string.Empty;
        if (gameManager == null) missingReferences += "GameManager, ";
        if (pauseRoot == null) missingReferences += "Pause Root, ";
        if (settingsRoot == null) missingReferences += "Settings Root, ";
        if (resumeButton == null) missingReferences += "Играть button, ";
        if (settingsButton == null) missingReferences += "Настройки button, ";
        if (restartButton == null) missingReferences += "Рестарт button, ";
        if (menuButton == null) missingReferences += "Выйти в меню button, ";
        if (settingsBackButton == null) missingReferences += "Settings Back button, ";

        if (!string.IsNullOrEmpty(missingReferences))
        {
            missingReferences = missingReferences.TrimEnd(' ', ',');
            Debug.LogError(
                "PauseMenuController: missing references: " + missingReferences +
                ". Stop Play mode and run Game Jam > Setup Pause Menu in the Editor.",
                this);
            enabled = false;
            return;
        }

        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(OpenSettings);
        restartButton.onClick.AddListener(Restart);
        menuButton.onClick.AddListener(ReturnToMenu);
        settingsBackButton.onClick.AddListener(ReturnToPause);

        pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (_isPaused)
        {
            if (settingsRoot.activeSelf)
                ReturnToPause();
            else
                Resume();
            return;
        }

        if (gameManager.State == GameManager.GameState.Playing)
            Pause();
    }

    private void Pause()
    {
        if (gameManager.State != GameManager.GameState.Playing)
            return;

        _isPaused = true;
        Time.timeScale = 0f;
        pauseRoot.SetActive(true);
        settingsRoot.SetActive(false);
    }

    public void Resume()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        settingsRoot.SetActive(false);
        pauseRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OpenSettings()
    {
        if (!_isPaused)
            return;

        pauseRoot.SetActive(false);
        settingsRoot.SetActive(true);
    }

    private void ReturnToPause()
    {
        if (!_isPaused)
            return;

        settingsRoot.SetActive(false);
        pauseRoot.SetActive(true);
    }

    private void ReturnToMenu()
    {
        if (!_isPaused)
            return;

        Time.timeScale = 1f;
        _isPaused = false;
        pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
        SceneManager.LoadScene(menuSceneName);
    }

    private void Restart()
    {
        if (!_isPaused)
            return;

        Time.timeScale = 1f;
        _isPaused = false;
        pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
        gameManager.Retry();
    }

    private void OnDisable()
    {
        if (_isPaused)
            Time.timeScale = 1f;
        _isPaused = false;
    }

    private void OnDestroy()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (menuButton != null) menuButton.onClick.RemoveListener(ReturnToMenu);
        if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(ReturnToPause);
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }
}