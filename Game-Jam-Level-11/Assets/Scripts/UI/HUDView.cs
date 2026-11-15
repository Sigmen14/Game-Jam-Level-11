using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates existing uGUI components only. All widgets and panels are authored
/// in prefab assets and assigned through the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDView : MonoBehaviour
{
    [Header("HUD widgets")]
    [SerializeField] private Slider flameSlider;
    [SerializeField] private Image flameFillImage;
    [SerializeField] private Text flameValueText;
    [SerializeField] private Text timerText;
    [SerializeField] private GameObject windWarningRoot;
    [SerializeField] private Text windWarningText;

    [Header("Gameplay panels")]
    [SerializeField] private GameObject hudContentRoot;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text resultTimeText;

    private GameManager gameManager;
    private FlameMeter flameMeter;
    private WindController windController;

    public void Bind(GameManager manager, FlameMeter meter, WindController wind)
    {
        if (flameMeter != null)
            flameMeter.ValueChanged -= SetFlame;

        if (windController != null)
            windController.WarningStateChanged -= SetWindWarning;

        gameManager = manager;
        flameMeter = meter;
        windController = wind;

        if (flameMeter != null)
        {
            flameMeter.ValueChanged += SetFlame;
            SetFlame(flameMeter.Current, flameMeter.Maximum);
        }

        if (windController != null)
        {
            windController.WarningStateChanged += SetWindWarning;
            SetWindWarning(windController.IsWarning);
        }
    }

    public void ShowMainMenu()
    {
        SetActive(resultPanel, false);
        SetActive(hudContentRoot, false);
        SetWindWarning(false);
        SetTimer(gameManager != null ? gameManager.MatchDuration : 180f);
    }

    public void ShowGameplay()
    {
        SetActive(resultPanel, false);
        SetActive(hudContentRoot, true);
        SetWindWarning(false);
    }

    public void HideForEnding()
    {
        SetActive(hudContentRoot, false);
        SetActive(resultPanel, false);
        SetWindWarning(false);
    }

    public void ShowResult(bool didWin, float elapsedSeconds)
    {
        SetActive(hudContentRoot, true);
        SetActive(resultPanel, true);

        if (resultTitleText != null)
            resultTitleText.text = didWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ";

        if (resultTimeText != null)
            resultTimeText.text = "Время выживания: " + FormatTime(elapsedSeconds);

        SetWindWarning(false);
    }

    public void SetFlame(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (flameSlider != null)
            flameSlider.value = normalized;

        if (flameValueText != null)
            flameValueText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(maximum);

        if (flameFillImage != null)
        {
            flameFillImage.color = Color.Lerp(
                new Color(0.9f, 0.2f, 0.12f, 1f),
                new Color(1f, 0.68f, 0.18f, 1f),
                normalized);
        }
    }

    public void SetTimer(float secondsRemaining)
    {
        if (timerText != null)
            timerText.text = FormatTime(secondsRemaining);
    }

    public void SetWindWarning(bool isVisible)
    {
        SetActive(windWarningRoot, isVisible);

        if (windWarningText != null)
            windWarningText.text = "ВНИМАНИЕ: СКОРО ПОРЫВ ВЕТРА";
    }

    public void OnRetryButtonClicked()
    {
        if (gameManager != null)
            gameManager.Retry();
    }

    public void OnMainMenuButtonClicked()
    {
        if (gameManager != null)
            gameManager.ReturnToMainMenu();
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void OnDestroy()
    {
        if (flameMeter != null)
            flameMeter.ValueChanged -= SetFlame;

        if (windController != null)
            windController.WarningStateChanged -= SetWindWarning;
    }
}
