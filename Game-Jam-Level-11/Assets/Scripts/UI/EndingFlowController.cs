using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coordinates the outcome prelude, post-victory choice and the selected ending.
/// All presentation stays inside the existing UI_HUD Canvas and authored view.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingFlowController : MonoBehaviour
{
    [Header("Экран выбора")]
    [SerializeField] private GameObject choiceRoot;
    [SerializeField] private Text choiceDescriptionText;
    [SerializeField] private Button rekindleWorldButton;
    [SerializeField] private Button keepEmberButton;

    [Header("Экран концовки")]
    [SerializeField] private GameObject sequenceRoot;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private EndingSequenceView sequenceView;

    private GameManager gameManager;
    private bool choiceCommitted;
    private bool outcomePreludeActive;
    private bool outcomePreludeCompleted;
    private bool outcomeWasVictory;
    private float outcomeSurvivedSeconds;
    private float skipInputEnabledAt;
    private Coroutine outcomeRoutine;
    private Coroutine outcomePlaybackRoutine;
    private Action defeatPreludeCompleted;

    private void Update()
    {
        if (!outcomePreludeActive || Time.unscaledTime < skipInputEnabledAt)
            return;

        // The prelude can be skipped with the same inputs as the opening prologue.
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            SkipOutcomePrelude();
    }

    public void Bind(GameManager manager)
    {
        if (gameManager == manager)
            return;

        UnbindButtons();
        gameManager = manager;

        if (rekindleWorldButton != null)
            rekindleWorldButton.onClick.AddListener(ChooseRekindleWorld);

        if (keepEmberButton != null)
            keepEmberButton.onClick.AddListener(ChooseKeepEmber);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void ShowVictoryPrelude(float survivedSeconds)
    {
        BeginOutcomePrelude(true, survivedSeconds, null);
    }

    public void ShowDefeatPrelude(float survivedSeconds, Action onCompleted)
    {
        BeginOutcomePrelude(false, survivedSeconds, onCompleted);
    }

    private void BeginOutcomePrelude(bool didWin, float survivedSeconds, Action onCompleted)
    {
        GameManager.GameState expectedState = didWin
            ? GameManager.GameState.Won
            : GameManager.GameState.Lost;

        if (gameManager == null || gameManager.State != expectedState)
        {
            if (!didWin)
                onCompleted?.Invoke();
            return;
        }

        // FinishMatch already guards duplicate transitions; this second guard also
        // prevents two calls from starting parallel presentation coroutines.
        if (outcomePreludeActive || outcomePreludeCompleted)
            return;

        StopAllCoroutines();
        outcomeRoutine = null;
        outcomePlaybackRoutine = null;
        choiceCommitted = false;
        outcomeWasVictory = didWin;
        outcomeSurvivedSeconds = survivedSeconds;
        defeatPreludeCompleted = onCompleted;
        outcomePreludeActive = true;
        skipInputEnabledAt = Time.unscaledTime + 0.3f;

        SetActive(choiceRoot, false);
        SetActive(sequenceRoot, true);

        // The ending's own menu button is not part of the prelude; defeat uses
        // the established HUD result panel and victory uses the two-choice panel.
        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(false);

        if (sequenceView == null)
        {
            outcomePreludeActive = false;
            outcomePreludeCompleted = true;
            SetActive(sequenceRoot, false);

            if (didWin)
                ShowChoice(survivedSeconds);
            else
            {
                Action callback = defeatPreludeCompleted;
                defeatPreludeCompleted = null;
                callback?.Invoke();
            }

            return;
        }

        outcomeRoutine = StartCoroutine(PlayOutcomePreludeRoutine(didWin));
    }

    private IEnumerator PlayOutcomePreludeRoutine(bool didWin)
    {
        bool sequenceCompleted = false;
        outcomePlaybackRoutine = StartCoroutine(
            RunOutcomeView(didWin, () => sequenceCompleted = true));

        // A failsafe keeps the player from being trapped if a missing view reference
        // or an interrupted nested animation prevents its normal completion.
        float elapsed = 0f;
        const float safetyTimeout = 20f;
        while (!sequenceCompleted && elapsed < safetyTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!sequenceCompleted)
        {
            Debug.LogWarning(
                "EndingFlowController: outcome prelude reached its safety timeout; continuing to the result flow.",
                this);
            if (outcomePlaybackRoutine != null)
                StopCoroutine(outcomePlaybackRoutine);

            if (sequenceView != null)
            {
                sequenceView.StopOutcomeAmbience();
                sequenceView.ResetVisuals();
            }
        }

        outcomePlaybackRoutine = null;
        outcomeRoutine = null;
        CompleteOutcomePrelude();
    }

    private IEnumerator RunOutcomeView(bool didWin, Action onCompleted)
    {
        yield return sequenceView.PlayOutcomePrelude(didWin);
        onCompleted?.Invoke();
    }

    private void SkipOutcomePrelude()
    {
        if (!outcomePreludeActive)
            return;

        if (outcomeRoutine != null)
        {
            StopCoroutine(outcomeRoutine);
            outcomeRoutine = null;
        }

        if (outcomePlaybackRoutine != null)
        {
            StopCoroutine(outcomePlaybackRoutine);
            outcomePlaybackRoutine = null;
        }

        if (sequenceView != null)
        {
            sequenceView.StopOutcomeAmbience();
            sequenceView.ResetVisuals();
        }

        CompleteOutcomePrelude();
    }

    private void CompleteOutcomePrelude()
    {
        if (!outcomePreludeActive)
            return;

        outcomePreludeActive = false;
        outcomePreludeCompleted = true;
        SetActive(sequenceRoot, false);

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        if (outcomeWasVictory)
        {
            // The old choice and both existing ending animations remain unchanged.
            ShowChoice(outcomeSurvivedSeconds);
            return;
        }

        Action callback = defeatPreludeCompleted;
        defeatPreludeCompleted = null;

        if (gameManager != null && gameManager.State == GameManager.GameState.Lost)
            callback?.Invoke();
    }

    /// <summary>Opens the existing two-choice screen after the 180-second victory scene.</summary>
    public void ShowChoice(float survivedSeconds)
    {
        if (gameManager == null || gameManager.State != GameManager.GameState.Won)
            return;

        StopAllCoroutines();
        outcomeRoutine = null;
        outcomePlaybackRoutine = null;
        outcomePreludeActive = false;
        choiceCommitted = false;

        // Hide the cinematic before restoring its cached visuals to avoid a flash.
        SetActive(sequenceRoot, false);
        if (sequenceView != null)
            sequenceView.ResetVisuals();

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        if (choiceDescriptionText != null)
        {
            choiceDescriptionText.text =
                "Ты выдержал " + FormatTime(survivedSeconds) + " и разбудил древний маяк.\n\n" +
                "Но маяк не создаёт огонь. Он посылает зов другим источникам пламени. " +
                "Вдали кто-то всё ещё хранит свет.\n\n" +
                "Что станет с последней искрой?";
        }

        SetActive(choiceRoot, true);
    }

    /// <summary>Hides all ending UI when a new match or the main menu is entered.</summary>
    public void Hide()
    {
        StopAllCoroutines();
        outcomeRoutine = null;
        outcomePlaybackRoutine = null;
        outcomePreludeActive = false;
        outcomePreludeCompleted = false;
        defeatPreludeCompleted = null;
        choiceCommitted = false;

        if (sequenceView != null)
        {
            sequenceView.StopOutcomeAmbience();
            sequenceView.ResetVisuals();
        }

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        SetActive(choiceRoot, false);
        SetActive(sequenceRoot, false);
    }

    private void ChooseRekindleWorld()
    {
        SelectChoice(EndingStatistics.Choice.RekindleWorld);
    }

    private void ChooseKeepEmber()
    {
        SelectChoice(EndingStatistics.Choice.KeepEmber);
    }

    private void SelectChoice(EndingStatistics.Choice choice)
    {
        if (choiceCommitted || gameManager == null ||
            gameManager.State != GameManager.GameState.Won)
        {
            return;
        }

        if (sequenceView == null)
        {
            Debug.LogError("EndingFlowController: EndingSequenceView is not assigned.", this);
            return;
        }

        choiceCommitted = true;
        EndingStatistics.RecordChoice(choice);

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        SetActive(choiceRoot, false);
        SetActive(sequenceRoot, true);
        StartCoroutine(PlaySelectedEnding(choice));
    }

    private IEnumerator PlaySelectedEnding(EndingStatistics.Choice choice)
    {
        yield return sequenceView.PlayEnding(choice, EndingStatistics.FormatSummary());
    }

    private void ReturnToMainMenu()
    {
        if (gameManager != null)
            gameManager.ReturnToMainMenu();
    }

    private void UnbindButtons()
    {
        if (rekindleWorldButton != null)
            rekindleWorldButton.onClick.RemoveListener(ChooseRekindleWorld);

        if (keepEmberButton != null)
            keepEmberButton.onClick.RemoveListener(ChooseKeepEmber);

        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
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
        StopAllCoroutines();
        outcomeRoutine = null;
        outcomePlaybackRoutine = null;
        if (sequenceView != null)
            sequenceView.StopOutcomeAmbience();
        UnbindButtons();
    }
}
