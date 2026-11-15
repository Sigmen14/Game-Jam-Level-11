using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks discovered story fragments and presents them without pausing gameplay.
/// The catalog and every UI object are authored by the Editor setup tool.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryMemoryManager : MonoBehaviour
{
    public const string PlayerPrefsKey = "LastEmber.StoryMemoryFragments.v1";

    [Header("Story data")]
    [SerializeField] private StoryMemoryCatalog catalog;

    [Header("Discovery notification")]
    [SerializeField] private CanvasGroup toastCanvasGroup;
    [SerializeField] private Image dimOverlay;
    [SerializeField] private Text toastSymbol;
    [SerializeField] private Text toastTitle;
    [SerializeField] private Text toastBody;
    [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float holdDuration = 2.25f;
    [SerializeField, Range(0f, 0.5f)] private float dimAmount = 0.18f;

    [Header("Memory journal")]
    [SerializeField] private Button openJournalButton;
    [SerializeField] private Text progressLabel;
    [SerializeField] private GameObject journalRoot;
    [SerializeField] private CanvasGroup journalCanvasGroup;
    [SerializeField] private Text journalContent;
    [SerializeField] private Button closeJournalButton;

    [Header("Audio")]
    [SerializeField] private AudioSource memoryAudioSource;
    [SerializeField] private AudioClip memoryChime;

    private readonly HashSet<string> collectedIds = new HashSet<string>();
    private bool progressLoaded;
    private bool journalIsOpen;
    private float toastDimAlpha;
    private Coroutine toastRoutine;

    public int CollectedCount
    {
        get
        {
            EnsureProgressLoaded();
            return collectedIds.Count;
        }
    }

    private void Awake()
    {
        EnsureProgressLoaded();

        if (openJournalButton != null)
            openJournalButton.onClick.AddListener(OpenJournal);

        if (closeJournalButton != null)
            closeJournalButton.onClick.AddListener(CloseJournal);

        if (toastCanvasGroup != null)
        {
            toastCanvasGroup.alpha = 0f;
            toastCanvasGroup.interactable = false;
            toastCanvasGroup.blocksRaycasts = false;
        }

        if (journalRoot != null)
            journalRoot.SetActive(false);

        if (journalCanvasGroup != null)
        {
            journalCanvasGroup.interactable = true;
            journalCanvasGroup.blocksRaycasts = true;
        }

        UpdateJournal();
        UpdateProgressLabel();
        ApplyDim();
    }

    /// <summary>Returns true when this fragment was found in a previous or current session.</summary>
    public bool HasFragment(string fragmentId)
    {
        EnsureProgressLoaded();
        return !string.IsNullOrEmpty(fragmentId) && collectedIds.Contains(fragmentId);
    }

    /// <summary>
    /// Stores a newly discovered fragment and plays a brief notification.
    /// Duplicate discoveries are ignored; ordinary fuel pickups remain unchanged.
    /// </summary>
    public bool TryCollect(string fragmentId)
    {
        EnsureProgressLoaded();

        if (catalog == null || !catalog.TryGetFragment(fragmentId, out StoryMemoryEntry entry))
        {
            Debug.LogWarning("StoryMemoryManager: fragment '" + fragmentId + "' is missing from the catalog.", this);
            return false;
        }

        if (!collectedIds.Add(fragmentId))
            return false;

        SaveProgress();
        UpdateProgressLabel();
        UpdateJournal();

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        if (toastSymbol != null)
            toastSymbol.text = entry.Symbol;

        if (toastTitle != null)
            toastTitle.text = entry.Title;

        if (toastBody != null)
            toastBody.text = entry.Body;

        if (memoryAudioSource != null && memoryChime != null)
            memoryAudioSource.PlayOneShot(memoryChime);

        if (toastCanvasGroup != null && dimOverlay != null)
            toastRoutine = StartCoroutine(ShowDiscoveryToast());

        return true;
    }

    public void OpenJournal()
    {
        if (journalRoot == null)
            return;

        journalIsOpen = true;
        UpdateJournal();
        journalRoot.SetActive(true);

        if (journalCanvasGroup != null)
        {
            journalCanvasGroup.alpha = 1f;
            journalCanvasGroup.interactable = true;
            journalCanvasGroup.blocksRaycasts = true;
        }

        if (dimOverlay != null)
            dimOverlay.raycastTarget = true;

        ApplyDim();
    }

    public void CloseJournal()
    {
        journalIsOpen = false;

        if (journalRoot != null)
            journalRoot.SetActive(false);

        if (dimOverlay != null)
            dimOverlay.raycastTarget = false;

        ApplyDim();
    }

    private IEnumerator ShowDiscoveryToast()
    {
        toastCanvasGroup.alpha = 0f;
        toastDimAlpha = 0f;
        ApplyDim();

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            toastCanvasGroup.alpha = t;
            toastDimAlpha = dimAmount * t;
            ApplyDim();
            yield return null;
        }

        toastCanvasGroup.alpha = 1f;
        toastDimAlpha = dimAmount;
        ApplyDim();

        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            toastCanvasGroup.alpha = 1f - t;
            toastDimAlpha = dimAmount * (1f - t);
            ApplyDim();
            yield return null;
        }

        toastCanvasGroup.alpha = 0f;
        toastDimAlpha = 0f;
        ApplyDim();
        toastRoutine = null;
    }

    private void EnsureProgressLoaded()
    {
        if (progressLoaded)
            return;

        progressLoaded = true;
        collectedIds.Clear();

        string stored = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(stored))
            return;

        string[] ids = stored.Split('|');
        for (int i = 0; i < ids.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
                collectedIds.Add(ids[i]);
        }
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetString(PlayerPrefsKey, string.Join("|", collectedIds));
        PlayerPrefs.Save();
    }

    private void UpdateProgressLabel()
    {
        if (progressLabel == null)
            return;

        int total = catalog != null ? catalog.Count : 0;
        progressLabel.text = "✦ ПАМЯТЬ  " + CollectedCount + "/" + total;
    }

    private void UpdateJournal()
    {
        if (journalContent == null || catalog == null)
            return;

        StringBuilder builder = new StringBuilder();
        builder.Append("НАЙДЕНО: ").Append(CollectedCount).Append(" / ").Append(catalog.Count);
        builder.Append("\n\n");

        StoryMemoryEntry[] entries = catalog.Entries;
        if (entries == null)
        {
            journalContent.text = builder.ToString();
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            StoryMemoryEntry entry = entries[i];
            if (entry == null)
                continue;

            builder.Append((i + 1).ToString("00")).Append("  ");
            if (HasFragment(entry.Id))
            {
                builder.Append(entry.Symbol).Append("  ").Append(entry.Title).Append('\n');
                builder.Append(entry.Body).Append("\n\n");
            }
            else
            {
                builder.Append("???").Append('\n');
                builder.Append("Это воспоминание ещё не найдено.\n\n");
            }
        }

        journalContent.text = builder.ToString();
    }

    private void ApplyDim()
    {
        if (dimOverlay == null)
            return;

        Color color = dimOverlay.color;
        color.a = journalIsOpen ? 0.52f : toastDimAlpha;
        dimOverlay.color = color;
    }

    private void OnDestroy()
    {
        if (openJournalButton != null)
            openJournalButton.onClick.RemoveListener(OpenJournal);

        if (closeJournalButton != null)
            closeJournalButton.onClick.RemoveListener(CloseJournal);
    }
}
