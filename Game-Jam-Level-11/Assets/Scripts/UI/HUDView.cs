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
    private Image windWarningBackground;
    private RectTransform windWarningRect;
    private Vector3 windWarningBaseScale = Vector3.one;
    private Color windWarningBaseColor = Color.white;
    private Color windWarningBaseTextColor = Color.white;
    private bool challengeMessageVisible;
    private string challengeMessage;

    public void Bind(GameManager manager, FlameMeter meter, WindController wind)
    {
        if (flameMeter != null) flameMeter.ValueChanged -= SetFlame;
        if (windController != null)
        {
            windController.WarningStateChanged -= HandleWindStateChanged;
            windController.WindActiveStateChanged -= HandleWindStateChanged;
        }

        gameManager = manager;
        flameMeter = meter;
        windController = wind;
        CacheWindWarningVisuals();

        if (flameMeter != null)
        {
            flameMeter.ValueChanged += SetFlame;
            SetFlame(flameMeter.Current, flameMeter.Maximum);
        }

        if (windController != null)
        {
            windController.WarningStateChanged += HandleWindStateChanged;
            windController.WindActiveStateChanged += HandleWindStateChanged;
            RefreshWindWarning();
        }
        else SetWindWarning(false);
    }

    private void Update()
    {
        UpdateWindWarningVisuals();
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
        if (resultTitleText != null) resultTitleText.text = didWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
        if (resultTimeText != null)
            resultTimeText.text = "Время выживания: " + FormatTime(elapsedSeconds);
        SetWindWarning(false);
    }

    public void SetFlame(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (flameSlider != null) flameSlider.value = normalized;
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
        if (timerText != null) timerText.text = FormatTime(secondsRemaining);
    }

    public void SetWindWarning(bool isVisible)
    {
        if (!isVisible)
            challengeMessageVisible = false;
        RefreshWindWarning();
    }

    public void ShowChallengeMessage(string message)
    {
        challengeMessage = message;
        challengeMessageVisible = !string.IsNullOrEmpty(message);
        RefreshWindWarning();
    }

    public void HideChallengeMessage()
    {
        challengeMessageVisible = false;
        RefreshWindWarning();
    }

    private void RefreshWindWarning()
    {
        bool windIsVisible = windController != null &&
            (windController.IsWarning || windController.IsWindActive);
        bool shouldShow = windIsVisible || challengeMessageVisible;
        SetActive(windWarningRoot, shouldShow);

        if (!shouldShow)
            RestoreWindWarningVisuals();
        else if (windWarningText != null)
        {
            string message = windIsVisible ? windController.StatusMessage : string.Empty;
            if (challengeMessageVisible)
                message = string.IsNullOrEmpty(message)
                    ? challengeMessage
                    : message + "\n" + challengeMessage;
            windWarningText.text = message;
        }
    }

    private void HandleWindStateChanged(bool state)
    {
        RefreshWindWarning();
    }

    private void CacheWindWarningVisuals()
    {
        windWarningBackground = windWarningRoot != null ? windWarningRoot.GetComponent<Image>() : null;
        windWarningRect = windWarningRoot != null ? windWarningRoot.GetComponent<RectTransform>() : null;
        if (windWarningBackground != null) windWarningBaseColor = windWarningBackground.color;
        if (windWarningRect != null) windWarningBaseScale = windWarningRect.localScale;
        if (windWarningText != null) windWarningBaseTextColor = windWarningText.color;
    }

    private void UpdateWindWarningVisuals()
    {
        if (windWarningRoot == null) return;
        bool warning = windController != null && windController.IsWarning;
        bool gustActive = windController != null && windController.IsWindActive;
        bool showWindStatus = warning || gustActive;
        bool visible = showWindStatus || challengeMessageVisible;
        SetActive(windWarningRoot, visible);
        if (!visible)
        {
            RestoreWindWarningVisuals();
            return;
        }

        if (windWarningText != null)
        {
            string message = showWindStatus ? windController.StatusMessage : string.Empty;
            if (challengeMessageVisible)
                message = string.IsNullOrEmpty(message)
                    ? challengeMessage
                    : message + "\n" + challengeMessage;
            windWarningText.text = message;
        }
        float pulseRate = gustActive ? 8.5f : (warning ? 4.2f : 2.8f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseRate);
        if (windWarningBackground != null)
        {
            Color darkWind = gustActive
                ? new Color(0.025f, 0.032f, 0.04f, 0.96f)
                : (warning ? new Color(0.075f, 0.045f, 0.028f, 0.93f)
                           : new Color(0.035f, 0.055f, 0.065f, 0.94f));
            Color warmEdge = gustActive
                ? new Color(0.12f, 0.075f, 0.045f, 0.98f)
                : (warning ? new Color(0.20f, 0.085f, 0.025f, 0.96f)
                           : new Color(0.08f, 0.18f, 0.20f, 0.96f));
            windWarningBackground.color = Color.Lerp(darkWind, warmEdge, pulse * 0.48f);
        }
        if (windWarningText != null)
        {
            Color emberGold = new Color(1f, 0.72f, 0.30f, 1f);
            Color brightGold = new Color(1f, 0.90f, 0.58f, 1f);
            windWarningText.color = Color.Lerp(emberGold, brightGold, pulse * 0.65f);
        }
        if (windWarningRect != null)
        {
            float scalePulse = gustActive ? 0.035f : 0.016f;
            windWarningRect.localScale = windWarningBaseScale * (1f + pulse * scalePulse);
        }
    }

    private void RestoreWindWarningVisuals()
    {
        if (windWarningBackground != null) windWarningBackground.color = windWarningBaseColor;
        if (windWarningText != null) windWarningText.color = windWarningBaseTextColor;
        if (windWarningRect != null) windWarningRect.localScale = windWarningBaseScale;
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
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private void OnDestroy()
    {
        if (flameMeter != null) flameMeter.ValueChanged -= SetFlame;
        if (windController != null)
        {
            windController.WarningStateChanged -= HandleWindStateChanged;
            windController.WindActiveStateChanged -= HandleWindStateChanged;
        }
    }
}
