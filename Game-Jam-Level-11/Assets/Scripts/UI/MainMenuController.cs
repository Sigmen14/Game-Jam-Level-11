using UnityEngine;

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

    public void PlayGame()
    {
        if (gameManager == null)
        {
            Debug.LogError("MainMenuController: assign GameManager in the Inspector.", this);
            return;
        }

        gameManager.StartGame();
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
