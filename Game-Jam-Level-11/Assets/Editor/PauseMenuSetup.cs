using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PauseMenuSetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string SettingsPrefabPath = "Assets/Prefabs/UI/UI_SettingsPanel.prefab";
    private const string PausePrefabPath = "Assets/Prefabs/UI/UI_PausePanel.prefab";
    private const string EmberSheetPath = "Assets/Art/LastFlame/PauseEmbers.png";
    private const int SheetColumns = 4;
    private const int SheetRows = 4;
    private const int FrameWidth = 48;
    private const int FrameHeight = 32;
    private const string EmberAnimationTypeName = "PauseEmberAnimation";

    [MenuItem("Game Jam/Setup Pause Menu")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameManager manager = FindInScene<GameManager>(scene);
        Canvas canvas = FindInScene<Canvas>(scene);
        if (manager == null || canvas == null)
        {
            EditorUtility.DisplayDialog("Pause Menu", "В Main должны существовать GameManager и Canvas UI_HUD.", "OK");
            return;
        }

        Sprite[] emberFrames = PrepareEmberFrames();
        if (emberFrames == null || emberFrames.Length != SheetColumns * SheetRows)
        {
            EditorUtility.DisplayDialog("Pause Menu", "Не удалось подготовить 16 кадров анимации PauseEmbers.", "OK");
            return;
        }

        GameObject oldPauseRoot = FindDirectChild(canvas.transform, "UI_PausePanel");
        if (oldPauseRoot != null)
            Object.DestroyImmediate(oldPauseRoot);

        GameObject generatedPausePanel = BuildPausePanel(emberFrames);
        PrefabUtility.SaveAsPrefabAsset(generatedPausePanel, PausePrefabPath);
        Object.DestroyImmediate(generatedPausePanel);
        AssetDatabase.SaveAssets();

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        GameObject pauseRoot = PrefabUtility.InstantiatePrefab(pausePrefab, canvas.transform) as GameObject;

        GameObject settingsRoot = FindDirectChild(canvas.transform, "UI_SettingsPanel_Pause");
        if (settingsRoot == null)
        {
            GameObject settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (settingsPrefab == null)
            {
                EditorUtility.DisplayDialog("Pause Menu", "Не найден существующий UI_SettingsPanel.prefab.", "OK");
                return;
            }

            settingsRoot = PrefabUtility.InstantiatePrefab(settingsPrefab, canvas.transform) as GameObject;
            settingsRoot.name = "UI_SettingsPanel_Pause";
        }

        Button resume = FindButton(pauseRoot, "ИГРАТЬ");
        Button settings = FindButton(pauseRoot, "НАСТРОЙКИ");
        Button restart = FindButton(pauseRoot, "РЕСТАРТ");
        Button menu = FindButton(pauseRoot, "ВЫЙТИ В МЕНЮ");
        Button back = FindButtonByText(settingsRoot, "НАЗАД");

        AudioSettingsController audioSettings = canvas.GetComponent<AudioSettingsController>();
        Slider sfxSlider = settingsRoot.transform.Find("SfxSlider")?.GetComponent<Slider>();
        Slider musicSlider = settingsRoot.transform.Find("MusicSlider")?.GetComponent<Slider>();
        Text sfxValueText = settingsRoot.transform.Find("SfxSliderValue")?.GetComponent<Text>();
        Text musicValueText = settingsRoot.transform.Find("MusicSliderValue")?.GetComponent<Text>();

        if (resume == null || settings == null || restart == null || menu == null || back == null ||
            audioSettings == null || sfxSlider == null || musicSlider == null ||
            sfxValueText == null || musicValueText == null)
        {
            EditorUtility.DisplayDialog(
                "Pause Menu",
                "Не найдены кнопки паузы или элементы существующей панели настроек.",
                "OK");
            return;
        }

        // Reuse the AudioSettingsController and audio source arrays already configured on UI_HUD.
        SerializedObject audioSerialized = new SerializedObject(audioSettings);
        audioSerialized.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        audioSerialized.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        audioSerialized.FindProperty("sfxValueText").objectReferenceValue = sfxValueText;
        audioSerialized.FindProperty("musicValueText").objectReferenceValue = musicValueText;
        audioSerialized.ApplyModifiedPropertiesWithoutUndo();

        ClearPersistentListeners(sfxSlider.onValueChanged);
        ClearPersistentListeners(musicSlider.onValueChanged);
        UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, audioSettings.SetSfxVolume);
        UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, audioSettings.SetMusicVolume);
        back.onClick = new Button.ButtonClickedEvent();

        PauseMenuController controller = manager.GetComponent<PauseMenuController>();
        if (controller == null)
            controller = Undo.AddComponent<PauseMenuController>(manager.gameObject);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("gameManager").objectReferenceValue = manager;
        serialized.FindProperty("pauseRoot").objectReferenceValue = pauseRoot;
        serialized.FindProperty("settingsRoot").objectReferenceValue = settingsRoot;
        serialized.FindProperty("resumeButton").objectReferenceValue = resume;
        serialized.FindProperty("settingsButton").objectReferenceValue = settings;
        serialized.FindProperty("restartButton").objectReferenceValue = restart;
        serialized.FindProperty("menuButton").objectReferenceValue = menu;
        serialized.FindProperty("settingsBackButton").objectReferenceValue = back;
        serialized.FindProperty("menuSceneName").stringValue = "Menu";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        controller.enabled = true;
        EditorUtility.SetDirty(controller);

        pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Pause Menu", "Пауза обновлена: добавлен выход в меню и анимация угольков.", "OK");
    }

    private static Sprite[] PrepareEmberFrames()
    {
        GenerateEmberSpriteSheet();
        AssetDatabase.ImportAsset(EmberSheetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(EmberSheetPath) as TextureImporter;
        if (importer == null)
            return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 32f;

        importer.SaveAndReimport();
        importer = AssetImporter.GetAtPath(EmberSheetPath) as TextureImporter;
        if (importer == null)
            return null;

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] spriteRects = new SpriteRect[SheetColumns * SheetRows];
        List<SpriteNameFileIdPair> nameFileIdPairs = new List<SpriteNameFileIdPair>();
        for (int row = 0; row < SheetRows; row++)
        {
            for (int column = 0; column < SheetColumns; column++)
            {
                int index = row * SheetColumns + column;
                SpriteRect spriteRect = new SpriteRect
                {
                    name = "PauseEmbers_" + index.ToString("00"),
                    spriteID = GUID.Generate(),
                    rect = new Rect(
                        column * FrameWidth,
                        (SheetRows - 1 - row) * FrameHeight,
                        FrameWidth,
                        FrameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };

                spriteRects[index] = spriteRect;
                nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID));
            }
        }

        dataProvider.SetSpriteRects(spriteRects);
        ISpriteNameFileIdDataProvider nameFileIdDataProvider =
            dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdDataProvider == null)
        {
            Debug.LogError("PauseEmbers: sprite name/file ID data provider is unavailable.");
            return null;
        }

        nameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
        dataProvider.Apply();

        AssetImporter spriteImporter = dataProvider.targetObject as AssetImporter;
        if (spriteImporter != null)
            spriteImporter.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(EmberSheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }

    /// <summary>Creates a small, hand-authored pixel-art ember loop as an Editor asset.</summary>
    private static void GenerateEmberSpriteSheet()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(EmberSheetPath));

        int textureWidth = SheetColumns * FrameWidth;
        int textureHeight = SheetRows * FrameHeight;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.name = "PauseEmbers";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 outerGlow = new Color32(105, 42, 16, 105);
        Color32 middleGlow = new Color32(196, 75, 20, 165);
        Color32 emberOrange = new Color32(255, 153, 48, 232);
        Color32 hotCore = new Color32(255, 236, 170, 255);

        int[] baseX = { 5, 13, 22, 30, 39, 42 };
        int[] baseY = { 21, 9, 25, 12, 7, 24 };
        float[] power = { 0.90f, 0.70f, 0.85f, 0.95f, 0.75f, 0.88f };
        float[] phase = { 0.2f, 1.2f, 2.0f, 2.9f, 4.1f, 5.0f };
        Color32[] pixels = new Color32[textureWidth * textureHeight];

        for (int frame = 0; frame < SheetColumns * SheetRows; frame++)
        {
            float angle = 2f * Mathf.PI * frame / (SheetColumns * SheetRows);
            int frameColumn = frame % SheetColumns;
            int frameRow = SheetRows - 1 - frame / SheetColumns;
            int offsetX = frameColumn * FrameWidth;
            int offsetY = frameRow * FrameHeight;

            for (int particle = 0; particle < baseX.Length; particle++)
            {
                float xMotion = 2.4f * Mathf.Sin(angle + phase[particle])
                    + 0.8f * Mathf.Sin(2f * angle + phase[particle]);
                float yMotion = -1.5f * Mathf.Sin(angle + phase[particle] * 0.7f)
                    + 0.7f * Mathf.Cos(2f * angle + phase[particle]);
                float flicker = 0.7f + 0.3f *
                    (0.5f + 0.5f * Mathf.Sin(2f * angle + phase[particle] * 1.5f));

                int x = Mathf.RoundToInt(baseX[particle] + xMotion);
                int y = Mathf.RoundToInt(baseY[particle] + yMotion);
                int outerRadius = power[particle] * flicker > 0.7f ? 3 : 2;

                DrawPixelDiamond(pixels, textureWidth, textureHeight,
                    offsetX + x, offsetY + y, outerRadius, outerGlow);
                DrawPixelDiamond(pixels, textureWidth, textureHeight,
                    offsetX + x, offsetY + y, 2, middleGlow);
                DrawPixelDiamond(pixels, textureWidth, textureHeight,
                    offsetX + x, offsetY + y, 1, emberOrange);
                SetPixel(pixels, textureWidth, textureHeight,
                    offsetX + x, offsetY + y, hotCore);

                // Tiny detached sparks and short tails give the embers a living, uneven silhouette.
                if ((particle == 0 || particle == 3 || particle == 5) && frame % 4 >= 1 && frame % 4 <= 2)
                    SetPixel(pixels, textureWidth, textureHeight, offsetX + x + 4, offsetY + y - 3, emberOrange);

                if ((particle == 1 || particle == 2 || particle == 4) && frame % 3 == 0)
                    SetPixel(pixels, textureWidth, textureHeight, offsetX + x - 1, offsetY + y + 4, middleGlow);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(EmberSheetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    private static void DrawPixelDiamond(
        Color32[] pixels, int width, int height, int centerX, int centerY, int radius, Color32 color)
    {
        for (int x = -radius; x <= radius; x++)
        {
            int verticalRadius = radius - Mathf.Abs(x);
            for (int y = -verticalRadius; y <= verticalRadius; y++)
                SetPixel(pixels, width, height, centerX + x, centerY + y, color);
        }
    }

    private static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        pixels[y * width + x] = color;
    }

    private static GameObject BuildPausePanel(Sprite[] emberFrames)
    {
        GameObject root = new GameObject("UI_PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.035f, 0.88f);

        GameObject card = CreateUIObject("PauseCard", root.transform, typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(520f, 680f);
        card.GetComponent<Image>().color = new Color(0.07f, 0.11f, 0.13f, 1f);

        CreateEmberArtwork(card.transform, emberFrames);
        CreateText("ПАУЗА", card.transform, 42, new Vector2(0f, 145f), new Vector2(440f, 72f));
        CreateButton("ИГРАТЬ", card.transform, new Vector2(0f, 40f));
        CreateButton("НАСТРОЙКИ", card.transform, new Vector2(0f, -60f));
        CreateButton("РЕСТАРТ", card.transform, new Vector2(0f, -160f));
        CreateButton("ВЫЙТИ В МЕНЮ", card.transform, new Vector2(0f, -260f));
        return root;
    }

    private static void CreateEmberArtwork(Transform parent, Sprite[] frames)
    {
        System.Type animationType = FindLoadedType(EmberAnimationTypeName);
        if (animationType == null)
            throw new System.InvalidOperationException(
                "PauseEmberAnimation script is missing or has compile errors. Check Assets/Scripts/UI/PauseEmberAnimation.cs.");

        GameObject go = CreateUIObject(
            "PauseEmbers", parent,
            typeof(Image), animationType);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 250f);
        rect.sizeDelta = new Vector2(270f, 180f);

        Image image = go.GetComponent<Image>();
        image.sprite = frames[0];
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Component animationComponent = go.GetComponent(animationType);
        SerializedObject animation = new SerializedObject(animationComponent);
        animation.FindProperty("targetImage").objectReferenceValue = image;
        SerializedProperty frameArray = animation.FindProperty("frames");
        frameArray.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        animation.FindProperty("framesPerSecond").floatValue = 12f;
        animation.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateButton(string label, Transform parent, Vector2 position)
    {
        GameObject go = CreateUIObject(label, parent, typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 76f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.27f, 0.29f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.86f, 0.82f, 1f);
        colors.highlightedColor = new Color(1f, 0.72f, 0.35f, 1f);
        colors.pressedColor = new Color(0.78f, 0.42f, 0.18f, 1f);
        button.colors = colors;

        CreateText(label, go.transform, label == "ВЫЙТИ В МЕНЮ" ? 22 : 26,
            Vector2.zero, new Vector2(340f, 66f));
        return go;
    }

    private static void CreateText(string value, Transform parent, int size, Vector2 position, Vector2 dimensions)
    {
        GameObject go = CreateUIObject(value + "_Text", parent, typeof(Text));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static System.Type FindLoadedType(string fullName)
    {
        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static void ClearPersistentListeners(UnityEngine.Events.UnityEventBase unityEvent)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(unityEvent, i);
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName)
                return parent.GetChild(i).gameObject;
        return null;
    }

    private static Button FindButton(GameObject root, string label)
    {
        return FindButtonByText(root, label);
    }

    private static Button FindButtonByText(GameObject root, string label)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null && text.text.Trim().ToUpperInvariant() == label)
                return button;
        }
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}