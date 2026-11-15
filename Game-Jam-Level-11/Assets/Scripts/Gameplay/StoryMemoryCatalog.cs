using System;
using UnityEngine;

[Serializable]
public sealed class StoryMemoryEntry
{
    [SerializeField] private string id;
    [SerializeField] private string title;

    [TextArea(2, 4)]
    [SerializeField] private string body;

    [SerializeField] private string symbol = "✦";

    public string Id => id;
    public string Title => title;
    public string Body => body;
    public string Symbol => symbol;
}

[CreateAssetMenu(menuName = "Last Ember/Story Memory Catalog", fileName = "StoryMemoryCatalog")]
public sealed class StoryMemoryCatalog : ScriptableObject
{
    [SerializeField] private StoryMemoryEntry[] entries = new StoryMemoryEntry[0];

    public StoryMemoryEntry[] Entries => entries;
    public int Count => entries != null ? entries.Length : 0;

    public bool TryGetFragment(string fragmentId, out StoryMemoryEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(fragmentId) || entries == null)
            return false;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null &&
                string.Equals(entries[i].Id, fragmentId, StringComparison.Ordinal))
            {
                entry = entries[i];
                return true;
            }
        }

        return false;
    }
}
