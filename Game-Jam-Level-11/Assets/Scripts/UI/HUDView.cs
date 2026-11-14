using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates existing uGUI components only. The canvas, panels, buttons and texts
/// are authored as prefab assets and assigned through the Inspector.
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

    [Header("Panels")]
    [SerializeField] private GameObject hudContentRoot;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text resultTimeText;

    private GameManager _gameManager;
    private FlameMeter _flameMeter;
    private WindController _windController;

    public void Bind(GameManager gameManager, FlameMeter flameMeter, WindController windController)
    {
        if (_flameMeter != null)
        {
            _flameMeter.ValueChanged -= SetFlame;
        }

        if (_windController != null)
        {
            _windController.WarningStateChanged -= SetWindWarning;
        }

        _gameManager = gameManager;
        _flameMeter = flameMeter;
        _windController = windController;

        if (_flameMeter != null)
        {
            _flameMeter.ValueChanged += SetFlame;
            SetFlame(_flameMeter.Current, _flameMeter.Maximum);
        }

        if (_windController != null)
        {
            _windController.WarningStateChanged += SetWindWarning;
            SetWindWarning(_windController.IsWarning);
        }

    }

    public void ShowReady()
    {
        SetActive(hudContentRoot, false);
        SetActive(startPanel, true);
        SetActive(resultPanel, false);
        SetWindWarning(false);
    }

    public void ShowGameplay()
    {
        SetActive(startPanel, false);
        SetActive(resultPanel, false);
        SetActive(hudContentRoot, true);
        SetWindWarning(false);
    }

    public void ShowResult(bool didWin, float elapsedSeconds)
    {
        SetActive(hudContentRoot, true);
        SetActive(startPanel, false);
        SetActive(resultPanel, true);

        if (resultTitleText != null)
        {
            resultTitleText.text = didWin ? "ПОБЕДА" : "ОГОНЁК ПОГАС";
        }

        if (resultTimeText != null)
        {
            resultTimeText.text = "Время: " + FormatTime(elapsedSeconds);
        }

        SetWindWarning(false);
    }

    public void SetFlame(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (flameSlider != null)
        {
            flameSlider.value = normalized;
        }

        if (flameValueText != null)
        {
            flameValueText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(maximum);
        }

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
        {
            timerText.text = FormatTime(secondsRemaining);
        }
    }

    public void SetWindWarning(bool isVisible)
    {
        SetActive(windWarningRoot, isVisible);

        if (windWarningText != null)
        {
            windWarningText.text = "ВЕТЕР ПОДНИМАЕТСЯ";
        }
    }

    public void OnStartButtonClicked()
    {
        HandleStartClicked();
    }

    public void OnRetryButtonClicked()
    {
        HandleRetryClicked();
    }

    private void HandleStartClicked()
    {
        if (_gameManager != null)
        {
            _gameManager.StartGame();
        }
    }

    private void HandleRetryClicked()
    {
        if (_gameManager != null)
        {
            _gameManager.Retry();
        }
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainder.ToString("00");
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (_flameMeter != null)
        {
            _flameMeter.ValueChanged -= SetFlame;
        }

        if (_windController != null)
        {
            _windController.WarningStateChanged -= SetWindWarning;
        }
    }
}
