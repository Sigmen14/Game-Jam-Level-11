using UnityEngine;

/// <summary>
/// Persistent statistics for the two narrative outcomes.
/// The values survive application restarts and are displayed on every ending screen.
/// </summary>
public static class EndingStatistics
{
    public enum Choice
    {
        None = 0,
        RekindleWorld = 1,
        KeepEmber = 2
    }

    private const string RekindleCountKey = "LastEmber.Endings.RekindleWorld";
    private const string KeepEmberCountKey = "LastEmber.Endings.KeepEmber";
    private const string LastChoiceKey = "LastEmber.Endings.LastChoice";
    private const string TotalChoicesKey = "LastEmber.Endings.TotalChoices";

    public static int RekindleWorldCount => PlayerPrefs.GetInt(RekindleCountKey, 0);
    public static int KeepEmberCount => PlayerPrefs.GetInt(KeepEmberCountKey, 0);
    public static int TotalChoices => PlayerPrefs.GetInt(TotalChoicesKey, 0);
    public static Choice LastChoice => (Choice)PlayerPrefs.GetInt(LastChoiceKey, 0);

    public static void RecordChoice(Choice choice)
    {
        if (choice == Choice.None)
            return;

        string countKey = choice == Choice.RekindleWorld
            ? RekindleCountKey
            : KeepEmberCountKey;

        PlayerPrefs.SetInt(countKey, PlayerPrefs.GetInt(countKey, 0) + 1);
        PlayerPrefs.SetInt(TotalChoicesKey, TotalChoices + 1);
        PlayerPrefs.SetInt(LastChoiceKey, (int)choice);
        PlayerPrefs.Save();
    }

    public static string FormatSummary()
    {
        string lastChoice = LastChoice == Choice.RekindleWorld
            ? "Разжечь мир"
            : LastChoice == Choice.KeepEmber
                ? "Сохранить искру"
                : "ещё не выбран";

        return "СТАТИСТИКА ПУТЕШЕСТВИЯ\n" +
               "Разжечь мир: " + RekindleWorldCount + "\n" +
               "Сохранить искру: " + KeepEmberCount + "\n" +
               "Всего завершений: " + TotalChoices + "\n" +
               "Последний выбор: " + lastChoice;
    }
}
