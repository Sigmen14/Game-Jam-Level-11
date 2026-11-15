using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the pre-authored main-menu prefab in the gameplay scene.
/// The GameManager owns state transitions; this component only switches menu panels.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private MainMenuFireAtmosphere fireAtmosphere;
    [SerializeField] private string gameplaySceneName = "Main";

    private Button playButton;
    private Button settingsButton;
    private Button exitButton;
    private Button backButton;
    private bool _playTransitionPending;

    private void Awake()
    {
        if (fireAtmosphere == null)
            fireAtmosphere = GetComponent<MainMenuFireAtmosphere>();

        // These buttons had empty serialized OnClick lists in the prefab.
        // Wire them here so resizing/replacing their visual children cannot break game flow.
        playButton = FindAndBindButton(mainMenuPanel, "PlayButton", PlayGame);
        settingsButton = FindAndBindButton(mainMenuPanel, "SettingsButton", OpenSettings);
        exitButton = FindAndBindButton(mainMenuPanel, "ExitButton", ExitGame);
        backButton = FindAndBindButton(settingsPanel, "BackButton", CloseSettings);
    }

    public void PlayGame()
    {
        if (_playTransitionPending)
            return;

        if (fireAtmosphere != null)
        {
            _playTransitionPending = true;
            fireAtmosphere.PlayStartFlash(() =>
            {
                _playTransitionPending = false;
                StartGameImmediately();
            });
            return;
        }

        StartGameImmediately();
    }

    private void StartGameImmediately()
    {
        // The standalone Menu scene intentionally has no GameManager. Find one when
        // the menu is embedded in Main, otherwise transfer to the gameplay scene.
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            gameManager.StartGame();
            return;
        }

        if (SceneManager.GetActiveScene().name == gameplaySceneName)
        {
            Debug.LogError(
                "MainMenuController: no GameManager was found in Main.unity. Run Game Jam > Setup Game Flow UI.",
                this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError(
                "MainMenuController: gameplay scene '" + gameplaySceneName +
                "' is not in Build Settings. Add Assets/Scenes/" + gameplaySceneName + ".unity.",
                this);
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ShowMainMenu()
    {
        SetPanel(settingsPanel, false);
        SetPanel(mainMenuPanel, true);

        if (fireAtmosphere != null)
            fireAtmosphere.SetAtmosphereVisible(true);
    }

    public void HideMainMenu()
    {
        SetPanel(settingsPanel, false);
        SetPanel(mainMenuPanel, false);

        if (fireAtmosphere != null)
            fireAtmosphere.SetAtmosphereVisible(false);
    }

    public void OpenSettings()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(settingsPanel, true);
    }

    public void CloseSettings()
    {
        SetPanel(settingsPanel, false);
        SetPanel(mainMenuPanel, true);
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        Debug.Log("Exit pressed. Application.Quit closes a built player; Play Mode stays open.", this);
#endif
    }

    private static Button FindAndBindButton(
        GameObject panel,
        string buttonObjectName,
        UnityAction callback)
    {
        if (panel == null)
        {
            Debug.LogWarning(
                "MainMenuController: panel is not assigned; cannot bind " + buttonObjectName + ".",
                panel);
            return null;
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null || candidate.gameObject.name != buttonObjectName)
                continue;

            candidate.onClick.RemoveListener(callback);
            candidate.onClick.AddListener(callback);
            return candidate;
        }

        Debug.LogWarning(
            "MainMenuController: button '" + buttonObjectName + "' was not found under " + panel.name + ".",
            panel);
        return null;
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(PlayGame);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitGame);
        if (backButton != null)
            backButton.onClick.RemoveListener(CloseSettings);
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }
}
