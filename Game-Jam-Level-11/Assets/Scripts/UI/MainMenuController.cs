using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private string gameplaySceneName = "Main";

    public void PlayGame()
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
    }

    public void HideMainMenu()
    {
        SetPanel(settingsPanel, false);
        SetPanel(mainMenuPanel, false);
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

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
            panel.SetActive(active);
    }
}
