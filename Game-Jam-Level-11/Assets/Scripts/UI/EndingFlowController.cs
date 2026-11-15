using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coordinates the post-victory choice and the selected ending.
/// It only becomes available after GameManager has entered the Won state.
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

    /// <summary>Opens the choice only after the existing 180-second victory condition.</summary>
    public void ShowChoice(float survivedSeconds)
    {
        if (gameManager == null || gameManager.State != GameManager.GameState.Won)
            return;

        StopAllCoroutines();
        choiceCommitted = false;

        if (sequenceView != null)
            sequenceView.ResetVisuals();

        if (choiceDescriptionText != null)
        {
            choiceDescriptionText.text =
                "Ты выдержал " + FormatTime(survivedSeconds) + " и разбудил древний маяк.\n\n" +
                "Но маяк не создаёт огонь. Он посылает зов другим источникам пламени. " +
                "Вдали кто-то всё ещё хранит свет.\n\n" +
                "Что станет с последней искрой?";
        }

        SetActive(choiceRoot, true);
        SetActive(sequenceRoot, false);
    }

    /// <summary>Hides all ending UI when a new match or the main menu is entered.</summary>
    public void Hide()
    {
        StopAllCoroutines();
        choiceCommitted = false;
        SetActive(choiceRoot, false);
        SetActive(sequenceRoot, false);

        if (sequenceView != null)
            sequenceView.ResetVisuals();
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
        UnbindButtons();
    }
}
