using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the dedicated Menu scene. All panels and buttons are pre-authored
/// in UI_Menu.prefab and are only enabled/disabled here.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private string gameplaySceneName = "Main";

    [Header("Existing menu panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    public void PlayGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError("MainMenuController: gameplay scene '" + gameplaySceneName +
                "' is not in Build Settings. Add Assets/Scenes/" + gameplaySceneName + ".unity.");
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
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
        Debug.Log("Exit button pressed. Application.Quit closes a built player; Play Mode stays open.");
#endif
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
        {
            panel.SetActive(active);
        }
    }
}
