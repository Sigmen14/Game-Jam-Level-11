#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates the Last Ember memory catalog, notification/journal UI and five
/// collectible story markers in Main.unity. All objects are authored here,
/// never instantiated by the runtime story scripts.
/// </summary>
public static class StoryMemorySetup
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string CatalogPath = "Assets/Story/StoryMemoryCatalog.asset";
    private const string UiPrefabPath = "Assets/Prefabs/UI/UI_StoryMemory.prefab";
    private const string ChimePath = "Assets/Art/LastFlame/StoryMemoryChime.asset";
    private const string StandaloneShardName = "StoryMemory_Shard_05";

    private static readonly FragmentSeed[] DefaultFragments =
    {
        new FragmentSeed("memory_world", "Тёплый мир",
            "Когда-то ночью здесь никогда не было темно. В каждом окне горел свет.", "✧"),
        new FragmentSeed("memory_storm", "Последний шторм",
            "Ветер пришёл без предупреждения. Мы пытались разбудить погасшие маяки — и выпустили его из самой Цепи.", "≋"),
        new FragmentSeed("memory_keeper", "Смотритель",
            "Он остался последним у старого очага. Он знал, что сам не переживёт ночь.", "◈"),
        new FragmentSeed("memory_spark", "Последняя искра",
            "Он сохранил её не для себя. Последняя искра всё ещё отвечала на зов Цепи — и он верил, что однажды она зажжёт остальные огни.", "✶"),
        new FragmentSeed("memory_lighthouse", "Секрет маяка",
            "Маяк не создаёт огонь. Он только помогает тем, кто ещё горит, найти друг друга. И где-то далеко ей уже ответили.", "◎")
    };

    [MenuItem("Game Jam/Story Memory/Setup Story Memory Fragments")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Story Memory", "Остановите Play Mode перед настройкой.", "OK");
            return;
        }

        try
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameManager gameManager = FindInScene<GameManager>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            FuelPickup[] fuelPickups = FindAllInScene<FuelPickup>(scene)
                .Where(pickup => pickup != null &&
                    pickup.gameObject.name.StartsWith("FuelPoint_", StringComparison.Ordinal))
                .OrderBy(pickup => pickup.gameObject.name)
                .Take(4)
                .ToArray();

            if (gameManager == null || canvas == null || fuelPickups.Length != 4)
            {
                EditorUtility.DisplayDialog(
                    "Story Memory",
                    "В Main.unity должны существовать GameManager, Canvas UI_HUD и четыре объекта FuelPoint_01–FuelPoint_04.",
                    "OK");
                return;
            }

            StoryMemoryCatalog catalog = EnsureCatalog();
            AudioClip chime = EnsureChime();

            GameObject uiPrefabRoot = BuildMemoryUiPrefab();
            PrefabUtility.SaveAsPrefabAsset(uiPrefabRoot, UiPrefabPath);
            UnityEngine.Object.DestroyImmediate(uiPrefabRoot);

            GameObject oldUi = FindDirectChild(canvas.transform, "UI_StoryMemory");
            if (oldUi != null)
                UnityEngine.Object.DestroyImmediate(oldUi);

            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            GameObject uiInstance = PrefabUtility.InstantiatePrefab(uiPrefab, scene) as GameObject;
            if (uiInstance == null)
                throw new InvalidOperationException("Не удалось разместить UI_StoryMemory.prefab.");

            uiInstance.name = "UI_StoryMemory";
            uiInstance.transform.SetParent(canvas.transform, false);

            StoryMemoryManager memoryManager = gameManager.GetComponent<StoryMemoryManager>();
            if (memoryManager == null)
                memoryManager = Undo.AddComponent<StoryMemoryManager>(gameManager.gameObject);

            AudioSource audioSource = EnsureAudioSource(gameManager.transform);
            ConfigureManager(memoryManager, catalog, chime, audioSource, uiInstance);

            for (int i = 0; i < fuelPickups.Length; i++)
            {
                StoryMemoryPickup storyPickup = fuelPickups[i].GetComponent<StoryMemoryPickup>();
                if (storyPickup == null)
                    storyPickup = Undo.AddComponent<StoryMemoryPickup>(fuelPickups[i].gameObject);

                TextMesh marker = EnsureMarker(
                    fuelPickups[i].transform,
                    "StoryMemoryMarker",
                    new Vector3(0f, 0.48f, 0f),
                    0.22f);

                ConfigurePickup(storyPickup, DefaultFragments[i].Id, memoryManager, fuelPickups[i], marker, null);
                EditorUtility.SetDirty(fuelPickups[i]);
            }

            SetupStandaloneShard(scene, memoryManager);
            EditorUtility.SetDirty(memoryManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Story Memory",
                "Готово: пять воспоминаний, короткое уведомление, журнал и сохранение прогресса настроены в Main.unity.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Story Memory", "Настройка не завершена. Подробности находятся в Console.", "OK");
        }
    }

    [MenuItem("Game Jam/Story Memory/Reset Story Memory Progress")]
    public static void ResetProgress()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Story Memory", "Остановите Play Mode перед сбросом прогресса.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Reset Story Memory",
                "Удалить локальный прогресс найденных осколков на этой машине?",
                "Сбросить",
                "Отмена"))
            return;

        PlayerPrefs.DeleteKey(StoryMemoryManager.PlayerPrefsKey);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Story Memory", "Локальный прогресс воспоминаний сброшен.", "OK");
    }

    private static StoryMemoryCatalog EnsureCatalog()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Story"))
            AssetDatabase.CreateFolder("Assets", "Story");

        StoryMemoryCatalog catalog = AssetDatabase.LoadAssetAtPath<StoryMemoryCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<StoryMemoryCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        if (entries == null || !entries.isArray)
            throw new InvalidOperationException("StoryMemoryCatalog must contain a serialized entries array.");

        for (int i = 0; i < DefaultFragments.Length; i++)
        {
            FragmentSeed seed = DefaultFragments[i];
            bool exists = false;

            for (int j = 0; j < entries.arraySize; j++)
            {
                SerializedProperty idProperty = entries.GetArrayElementAtIndex(j).FindPropertyRelative("id");
                if (idProperty != null && idProperty.stringValue == seed.Id)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
                continue;

            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty item = entries.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("id").stringValue = seed.Id;
            item.FindPropertyRelative("title").stringValue = seed.Title;
            item.FindPropertyRelative("body").stringValue = seed.Body;
            item.FindPropertyRelative("symbol").stringValue = seed.Symbol;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static AudioClip EnsureChime()
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ChimePath);
        if (clip != null)
            return clip;

        const int sampleRate = 44100;
        const float duration = 0.72f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Exp(-5.8f * time) * Mathf.Clamp01(1f - time / duration);
            float fundamental = Mathf.Sin(2f * Mathf.PI * 659.25f * time) * 0.52f;
            float overtone = Mathf.Sin(2f * Mathf.PI * 987.77f * time) * 0.26f;
            float shimmer = Mathf.Sin(2f * Mathf.PI * 1318.5f * time) * 0.12f;
            samples[i] = (fundamental + overtone + shimmer) * envelope * 0.22f;
        }

        clip = AudioClip.Create("StoryMemoryChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AssetDatabase.CreateAsset(clip, ChimePath);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return clip;
    }

    private static AudioSource EnsureAudioSource(Transform managerRoot)
    {
        Transform audioTransform = managerRoot.Find("StoryMemoryAudio");
        GameObject audioObject;

        if (audioTransform == null)
        {
            audioObject = new GameObject("StoryMemoryAudio");
            audioObject.transform.SetParent(managerRoot, false);
        }
        else
        {
            audioObject = audioTransform.gameObject;
        }

        AudioSource source = audioObject.GetComponent<AudioSource>();
        if (source == null)
            source = Undo.AddComponent<AudioSource>(audioObject);

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0.72f;
        EditorUtility.SetDirty(source);
        return source;
    }

    private static void ConfigureManager(
        StoryMemoryManager manager,
        StoryMemoryCatalog catalog,
        AudioClip chime,
        AudioSource audioSource,
        GameObject uiRoot)
    {
        Transform toast = uiRoot.transform.Find("MemoryToast");
        Transform journal = uiRoot.transform.Find("MemoryJournal");
        Transform badge = uiRoot.transform.Find("MemoryJournalButton");

        if (toast == null || journal == null || badge == null)
            throw new InvalidOperationException("The generated story UI is missing a required panel.");

        SerializedObject serialized = new SerializedObject(manager);
        SetReference(serialized, "catalog", catalog);
        SetReference(serialized, "toastCanvasGroup", toast.GetComponent<CanvasGroup>());
        SetReference(serialized, "dimOverlay", uiRoot.transform.Find("MemoryDim").GetComponent<Image>());
        SetReference(serialized, "toastSymbol", toast.Find("Symbol").GetComponent<Text>());
        SetReference(serialized, "toastTitle", toast.Find("Title").GetComponent<Text>());
        SetReference(serialized, "toastBody", toast.Find("Body").GetComponent<Text>());
        SetReference(serialized, "openJournalButton", badge.GetComponent<Button>());
        SetReference(serialized, "progressLabel", badge.Find("ProgressLabel").GetComponent<Text>());
        SetReference(serialized, "journalRoot", journal.gameObject);
        SetReference(serialized, "journalCanvasGroup", journal.GetComponent<CanvasGroup>());
        SetReference(serialized, "journalContent", journal.Find("Content").GetComponent<Text>());
        SetReference(serialized, "closeJournalButton", journal.Find("CloseButton").GetComponent<Button>());
        SetReference(serialized, "memoryAudioSource", audioSource);
        SetReference(serialized, "memoryChime", chime);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        journal.gameObject.SetActive(false);
        EditorUtility.SetDirty(manager);
    }

    private static void SetupStandaloneShard(Scene scene, StoryMemoryManager manager)
    {
        GameObject shard = FindGameObjectInScene(scene, StandaloneShardName);
        if (shard == null)
        {
            shard = new GameObject(StandaloneShardName);
            GameObject level = FindGameObjectInScene(scene, "Level");
            if (level != null)
                shard.transform.SetParent(level.transform, false);
        }

        shard.transform.position = new Vector3(0f, 3.65f, 0f);
        shard.transform.localScale = Vector3.one;

        CircleCollider2D trigger = shard.GetComponent<CircleCollider2D>();
        if (trigger == null)
            trigger = Undo.AddComponent<CircleCollider2D>(shard);

        trigger.isTrigger = true;
        trigger.radius = 0.58f;

        StoryMemoryPickup storyPickup = shard.GetComponent<StoryMemoryPickup>();
        if (storyPickup == null)
            storyPickup = Undo.AddComponent<StoryMemoryPickup>(shard);

        TextMesh marker = EnsureMarker(shard.transform, "StoryMemoryMarker", Vector3.zero, 0.38f);
        ConfigurePickup(storyPickup, DefaultFragments[4].Id, manager, null, marker, trigger);
        EditorUtility.SetDirty(shard);
    }

    private static void ConfigurePickup(
        StoryMemoryPickup pickup,
        string fragmentId,
        StoryMemoryManager manager,
        FuelPickup fuelPickup,
        TextMesh marker,
        Collider2D standaloneCollider)
    {
        SerializedObject serialized = new SerializedObject(pickup);
        serialized.FindProperty("fragmentId").stringValue = fragmentId;
        SetReference(serialized, "memoryManager", manager);
        SetReference(serialized, "fuelPickup", fuelPickup);
        SetReference(serialized, "markerText", marker);
        SetReference(serialized, "standaloneCollider", standaloneCollider);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pickup);
    }

    private static TextMesh EnsureMarker(Transform parent, string markerName, Vector3 localPosition, float characterSize)
    {
        Transform markerTransform = parent.Find(markerName);
        GameObject markerObject;

        if (markerTransform == null)
        {
            markerObject = new GameObject(markerName);
            markerObject.transform.SetParent(parent, false);
        }
        else
        {
            markerObject = markerTransform.gameObject;
        }

        markerObject.transform.localPosition = localPosition;
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;

        TextMesh marker = markerObject.GetComponent<TextMesh>();
        if (marker == null)
            marker = Undo.AddComponent<TextMesh>(markerObject);

        marker.font = GetBuiltinFont();
        marker.text = "✦";
        marker.anchor = TextAnchor.MiddleCenter;
        marker.alignment = TextAlignment.Center;
        marker.characterSize = characterSize;
        marker.fontSize = 64;
        marker.color = new Color(1f, 0.78f, 0.34f, 1f);

        MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 12;

        EditorUtility.SetDirty(marker);
        return marker;
    }

    private static GameObject BuildMemoryUiPrefab()
    {
        GameObject root = new GameObject("UI_StoryMemory", typeof(RectTransform));
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreateImage("MemoryDim", root.transform, new Color(0.008f, 0.012f, 0.018f, 0f));
        Stretch(dim.rectTransform);

        GameObject toast = CreatePanel(
            "MemoryToast", root.transform, new Vector2(0.5f, 0.79f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700f, 176f), new Color(0.055f, 0.045f, 0.04f, 0.94f));
        CanvasGroup toastGroup = toast.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.interactable = false;
        toastGroup.blocksRaycasts = false;

        CreateText("Symbol", toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-286f, 0f), new Vector2(110f, 112f), "✦", 72,
            new Color(1f, 0.72f, 0.28f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
        CreateText("Overline", toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(82f, 57f), new Vector2(500f, 28f), "ОСКОЛОК ПАМЯТИ", 14,
            new Color(1f, 0.66f, 0.28f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        CreateText("Title", toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(82f, 25f), new Vector2(500f, 38f), "ТЁПЛЫЙ МИР", 24,
            new Color(1f, 0.91f, 0.74f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
        CreateText("Body", toast.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(82f, -35f), new Vector2(500f, 88f), string.Empty, 18,
            new Color(0.91f, 0.88f, 0.82f, 1f), TextAnchor.MiddleLeft, FontStyle.Normal);

        GameObject journal = CreatePanel(
            "MemoryJournal", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(760f, 560f), new Color(0.045f, 0.04f, 0.038f, 0.98f));
        CanvasGroup journalGroup = journal.AddComponent<CanvasGroup>();
        journalGroup.alpha = 1f;
        journalGroup.interactable = true;
        journalGroup.blocksRaycasts = true;

        CreateText("JournalTitle", journal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-10f, 238f), new Vector2(620f, 48f), "ОСКОЛКИ ПАМЯТИ", 27,
            new Color(1f, 0.78f, 0.43f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

        Text journalContent = CreateText(
            "Content", journal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f), new Vector2(670f, 430f), string.Empty, 17,
            new Color(0.92f, 0.89f, 0.83f, 1f), TextAnchor.UpperLeft, FontStyle.Normal);
        journalContent.alignment = TextAnchor.UpperLeft;

        CreateButton("CloseButton", journal.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-22f, -20f), new Vector2(48f, 42f), "×", 28,
            new Color(0.22f, 0.13f, 0.08f, 0.98f));

        GameObject badge = CreateButton("MemoryJournalButton", root.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f),
            new Vector2(184f, 44f), "✦ ПАМЯТЬ  0/5", 16, new Color(0.09f, 0.07f, 0.055f, 0.94f));
        badge.GetComponentInChildren<Text>(true).name = "ProgressLabel";

        journal.SetActive(false);
        return root;
    }

    private static GameObject CreatePanel(
        string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.sprite = GetUiSprite();
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = GetUiSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateButton(
        string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, string label, int fontSize, Color background)
    {
        GameObject buttonObject = CreatePanel(name, parent, anchor, pivot, position, size, background);
        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.68f, 1f);
        colors.pressedColor = new Color(0.88f, 0.65f, 0.38f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            size - new Vector2(16f, 8f), label, fontSize,
            new Color(1f, 0.87f, 0.68f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        text.raycastTarget = false;
        return buttonObject;
    }

    private static Text CreateText(
        string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, string value, int fontSize, Color color,
        TextAnchor alignment, FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = GetBuiltinFont();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite GetUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Missing serialized field '" + propertyName + "'.");
        property.objectReferenceValue = value;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T found = roots[i].GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
                if (transforms[j].name == objectName)
                    return transforms[j].gameObject;
        }
        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName)
                return parent.GetChild(i).gameObject;
        return null;
    }

    private sealed class FragmentSeed
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Body;
        public readonly string Symbol;

        public FragmentSeed(string id, string title, string body, string symbol)
        {
            Id = id;
            Title = title;
            Body = body;
            Symbol = symbol;
        }
    }
}
#endif
