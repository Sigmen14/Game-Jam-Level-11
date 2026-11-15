using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PauseMenuSetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string SettingsPrefabPath = "Assets/Prefabs/UI/UI_SettingsPanel.prefab";
    private const string MenuPrefabPath = "Assets/Prefabs/UI/UI_Menu.prefab";
    private const string CancelActionReferencePath = "Assets/Settings/InputActionReferences/UI_Cancel.asset";
    private const string PausePrefabPath = "Assets/Prefabs/UI/UI_PausePanel.prefab";
    private const string EmberSheetPath = "Assets/Art/LastFlame/PauseEmbers.png";
    private const string EmberClipPath = "Assets/Art/LastFlame/PauseEmbers.anim";
    private const string EmberControllerPath = "Assets/Art/LastFlame/PauseEmbers.controller";
    private const float EmberFramesPerSecond = 12f;
    private const int SheetColumns = 4;
    private const int SheetRows = 4;
    private const int FrameWidth = 48;
    private const int FrameHeight = 32;

    [MenuItem("Game Jam/Setup Pause Menu")]
    public static void Setup()
    {
        if (!EnsureMasterVolumeControlsInSettingsPrefab() || !WireMainMenuSettingsPrefab())
        {
            EditorUtility.DisplayDialog(
                "Pause Menu",
                "Не удалось подготовить три ползунка громкости. Проверьте UI_SettingsPanel.prefab и UI_Menu.prefab.",
                "OK");
            return;
        }

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

        RuntimeAnimatorController emberController = PrepareEmberAnimationController(emberFrames);
        if (emberController == null)
        {
            EditorUtility.DisplayDialog("Pause Menu", "Не удалось создать Animator для анимации огоньков.", "OK");
            return;
        }

        GameObject oldPauseRoot = FindDirectChild(canvas.transform, "UI_PausePanel");
        if (oldPauseRoot != null)
            Object.DestroyImmediate(oldPauseRoot);

        GameObject generatedPausePanel = BuildPausePanel(emberFrames, emberController);
        PrefabUtility.SaveAsPrefabAsset(generatedPausePanel, PausePrefabPath);
        Object.DestroyImmediate(generatedPausePanel);
        AssetDatabase.SaveAssets();

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        GameObject pauseRoot = PrefabUtility.InstantiatePrefab(pausePrefab, canvas.transform) as GameObject;

        GameObject oldSettingsRoot = FindDirectChild(canvas.transform, "UI_SettingsPanel_Pause");
        if (oldSettingsRoot != null)
            Object.DestroyImmediate(oldSettingsRoot);

        GameObject settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
        if (settingsPrefab == null)
        {
            EditorUtility.DisplayDialog("Pause Menu", "Не найден существующий UI_SettingsPanel.prefab.", "OK");
            return;
        }

        GameObject settingsRoot = PrefabUtility.InstantiatePrefab(settingsPrefab, canvas.transform) as GameObject;
        settingsRoot.name = "UI_SettingsPanel_Pause";

        Button resume = FindButton(pauseRoot, "ПРОДОЛЖИТЬ");
        Button settings = FindButton(pauseRoot, "НАСТРОЙКИ");
        Button restart = FindButton(pauseRoot, "РЕСТАРТ");
        Button menu = FindButton(pauseRoot, "ВЫЙТИ В МЕНЮ");
        Button back = FindButtonByText(settingsRoot, "НАЗАД");

        AudioSettingsController audioSettings = canvas.GetComponent<AudioSettingsController>();
        Slider masterSlider = settingsRoot.transform.Find("MasterSlider")?.GetComponent<Slider>();
        Slider sfxSlider = settingsRoot.transform.Find("SfxSlider")?.GetComponent<Slider>();
        Slider musicSlider = settingsRoot.transform.Find("MusicSlider")?.GetComponent<Slider>();
        Text masterValueText = settingsRoot.transform.Find("MasterSliderValue")?.GetComponent<Text>();
        Text sfxValueText = settingsRoot.transform.Find("SfxSliderValue")?.GetComponent<Text>();
        Text musicValueText = settingsRoot.transform.Find("MusicSliderValue")?.GetComponent<Text>();

        if (resume == null || settings == null || restart == null || menu == null || back == null ||
            audioSettings == null || masterSlider == null || sfxSlider == null || musicSlider == null ||
            masterValueText == null || sfxValueText == null || musicValueText == null)
        {
            EditorUtility.DisplayDialog(
                "Pause Menu",
                "Не найдены кнопки паузы или элементы существующей панели настроек.",
                "OK");
            return;
        }

        // Reuse the AudioSettingsController and audio source arrays already configured on UI_HUD.
        SerializedObject audioSerialized = new SerializedObject(audioSettings);
        audioSerialized.FindProperty("masterSlider").objectReferenceValue = masterSlider;
        audioSerialized.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        audioSerialized.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        audioSerialized.FindProperty("masterValueText").objectReferenceValue = masterValueText;
        audioSerialized.FindProperty("sfxValueText").objectReferenceValue = sfxValueText;
        audioSerialized.FindProperty("musicValueText").objectReferenceValue = musicValueText;
        audioSerialized.ApplyModifiedPropertiesWithoutUndo();

        ClearPersistentListeners(masterSlider.onValueChanged);
        ClearPersistentListeners(sfxSlider.onValueChanged);
        ClearPersistentListeners(musicSlider.onValueChanged);
        UnityEventTools.AddPersistentListener(masterSlider.onValueChanged, audioSettings.SetMasterVolume);
        UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, audioSettings.SetSfxVolume);
        UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, audioSettings.SetMusicVolume);
        back.onClick = new Button.ButtonClickedEvent();

        PauseMenuController controller = manager.GetComponent<PauseMenuController>();
        if (controller == null)
            controller = Undo.AddComponent<PauseMenuController>(manager.gameObject);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("gameManager").objectReferenceValue = manager;
        InputActionReference cancelAction = AssetDatabase.LoadAssetAtPath<InputActionReference>(CancelActionReferencePath);
        SerializedProperty cancelActionProperty = serialized.FindProperty("cancelAction");
        if (cancelActionProperty != null)
            cancelActionProperty.objectReferenceValue = cancelAction;
        serialized.FindProperty("pauseRoot").objectReferenceValue = pauseRoot;
        serialized.FindProperty("settingsRoot").objectReferenceValue = settingsRoot;
        serialized.FindProperty("resumeButton").objectReferenceValue = resume;
        serialized.FindProperty("settingsButton").objectReferenceValue = settings;
        serialized.FindProperty("restartButton").objectReferenceValue = restart;
        serialized.FindProperty("menuButton").objectReferenceValue = menu;
        serialized.FindProperty("settingsBackButton").objectReferenceValue = back;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // GameManager owns pause state and must hold the same controller reference.
        SerializedObject managerSerialized = new SerializedObject(manager);
        SerializedProperty pauseControllerProperty = managerSerialized.FindProperty("pauseMenuController");
        if (pauseControllerProperty == null)
            throw new System.InvalidOperationException("GameManager is missing its pauseMenuController reference.");
        pauseControllerProperty.objectReferenceValue = controller;
        managerSerialized.ApplyModifiedPropertiesWithoutUndo();

        controller.enabled = true;
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(manager);

        pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Pause Menu", "Готово: пауза, перезапуск, выход в меню и три настройки громкости подключены.", "OK");
    }

    private static bool EnsureMasterVolumeControlsInSettingsPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
        if (contents == null)
        {
            Debug.LogError("PauseMenuSetup: cannot load " + SettingsPrefabPath);
            return false;
        }

        try
        {
            Transform root = contents.transform;
            Transform sfxSliderSource = root.Find("SfxSlider");
            Transform sfxValueSource = root.Find("SfxSliderValue");
            Transform sfxLabelSource = root.Find("SfxLabel");
            if (sfxSliderSource == null || sfxValueSource == null || sfxLabelSource == null)
            {
                Debug.LogError("PauseMenuSetup: the settings prefab is missing its existing SFX controls.");
                return false;
            }

            Transform masterSlider = root.Find("MasterSlider");
            if (masterSlider == null)
            {
                GameObject sliderCopy = Object.Instantiate(sfxSliderSource.gameObject, root, false);
                sliderCopy.name = "MasterSlider";
                masterSlider = sliderCopy.transform;
            }

            Transform masterValue = root.Find("MasterSliderValue");
            if (masterValue == null)
            {
                GameObject valueCopy = Object.Instantiate(sfxValueSource.gameObject, root, false);
                valueCopy.name = "MasterSliderValue";
                masterValue = valueCopy.transform;
            }

            Transform masterLabel = root.Find("MasterLabel");
            if (masterLabel == null)
            {
                GameObject labelCopy = Object.Instantiate(sfxLabelSource.gameObject, root, false);
                labelCopy.name = "MasterLabel";
                masterLabel = labelCopy.transform;
            }

            Slider masterControl = masterSlider.GetComponent<Slider>();
            if (masterControl == null)
                return false;

            masterControl.SetValueWithoutNotify(1f);
            Text masterValueText = masterValue.GetComponent<Text>();
            Text masterLabelText = masterLabel.GetComponent<Text>();
            if (masterValueText == null || masterLabelText == null)
                return false;

            masterValueText.text = "100%";
            masterLabelText.text = "ОБЩАЯ ГРОМКОСТЬ";

            SetChildY(root, "Title", 190f);
            SetChildY(root, "MasterLabel", 120f);
            SetChildY(root, "MasterSlider", 75f);
            SetChildY(root, "MasterSliderValue", 75f);
            SetChildY(root, "SfxLabel", -5f);
            SetChildY(root, "SfxSlider", -45f);
            SetChildY(root, "SfxSliderValue", -45f);
            SetChildY(root, "MusicLabel", -90f);
            SetChildY(root, "MusicSlider", -130f);
            SetChildY(root, "MusicSliderValue", -130f);
            SetChildY(root, "BackButton", -245f);

            PrefabUtility.SaveAsPrefabAsset(contents, SettingsPrefabPath);
            AssetDatabase.SaveAssets();
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static bool WireMainMenuSettingsPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        if (contents == null)
        {
            Debug.LogError("PauseMenuSetup: cannot load " + MenuPrefabPath);
            return false;
        }

        try
        {
            AudioSettingsController audioSettings = contents.GetComponent<AudioSettingsController>();
            Transform settingsRoot = contents.transform.Find("UI_SettingsPanel");
            if (audioSettings == null || settingsRoot == null)
            {
                Debug.LogError("PauseMenuSetup: menu prefab is missing its AudioSettingsController or settings panel.");
                return false;
            }

            Slider masterSlider = settingsRoot.Find("MasterSlider")?.GetComponent<Slider>();
            Text masterValueText = settingsRoot.Find("MasterSliderValue")?.GetComponent<Text>();
            if (masterSlider == null || masterValueText == null)
                return false;

            SerializedObject serialized = new SerializedObject(audioSettings);
            serialized.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            serialized.FindProperty("masterValueText").objectReferenceValue = masterValueText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ClearPersistentListeners(masterSlider.onValueChanged);
            UnityEventTools.AddPersistentListener(masterSlider.onValueChanged, audioSettings.SetMasterVolume);

            PrefabUtility.SaveAsPrefabAsset(contents, MenuPrefabPath);
            AssetDatabase.SaveAssets();
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void SetChildY(Transform root, string childName, float y)
    {
        Transform child = root.Find(childName);
        if (child == null)
            return;

        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null)
            return;

        Vector2 position = rect.anchoredPosition;
        position.y = y;
        rect.anchoredPosition = position;
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

    private static RuntimeAnimatorController PrepareEmberAnimationController(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
            return null;

        // Rebuild both assets so the generated animation always matches the current sprite sheet.
        AssetDatabase.DeleteAsset(EmberControllerPath);
        AssetDatabase.DeleteAsset(EmberClipPath);

        AnimationClip clip = new AnimationClip
        {
            name = "PauseEmbers",
            frameRate = EmberFramesPerSecond
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / EmberFramesPerSecond,
                value = frames[i]
            };
        }

        keyframes[frames.Length] = new ObjectReferenceKeyframe
        {
            time = frames.Length / EmberFramesPerSecond,
            value = frames[0]
        };

        EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve(
            string.Empty, typeof(Image), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        AssetDatabase.CreateAsset(clip, EmberClipPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(EmberControllerPath);
        if (controller == null)
            return null;

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.AddState("Embers");
        state.motion = clip;
        stateMachine.defaultState = state;

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
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

    private static GameObject BuildPausePanel(Sprite[] emberFrames, RuntimeAnimatorController emberController)
    {
        GameObject root = new GameObject("UI_PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.035f, 0.88f);

        GameObject card = CreateUIObject("PauseCard", root.transform, typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(1040f, 1360f);
        card.GetComponent<Image>().color = new Color(0.07f, 0.11f, 0.13f, 1f);

        CreateEmberArtwork(card.transform, emberFrames, emberController);
        CreateText("ПАУЗА", card.transform, 84, new Vector2(0f, 290f), new Vector2(880f, 144f));
        CreateButton("ПРОДОЛЖИТЬ", card.transform, new Vector2(0f, 100f));
        CreateButton("НАСТРОЙКИ", card.transform, new Vector2(0f, -100f));
        CreateButton("РЕСТАРТ", card.transform, new Vector2(0f, -300f));
        CreateButton("ВЫЙТИ В МЕНЮ", card.transform, new Vector2(0f, -500f));
        return root;
    }

    private static void CreateEmberArtwork(Transform parent, Sprite[] frames, RuntimeAnimatorController emberController)
    {
        // Use Unity built-in animation components so the Editor setup has no dependency
        // on a custom runtime script type that may not be available in the Editor assembly.
        GameObject go = CreateUIObject("PauseEmbers", parent, typeof(Image));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 500f);
        rect.sizeDelta = new Vector2(540f, 360f);

        Image image = go.GetComponent<Image>();
        image.sprite = frames[0];
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Animator animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = emberController;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
    }

    private static GameObject CreateButton(string label, Transform parent, Vector2 position)
    {
        GameObject go = CreateUIObject(label, parent, typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(720f, 152f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.27f, 0.29f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.86f, 0.82f, 1f);
        colors.highlightedColor = new Color(1f, 0.72f, 0.35f, 1f);
        colors.pressedColor = new Color(0.78f, 0.42f, 0.18f, 1f);
        button.colors = colors;

        CreateText(label, go.transform, label == "ВЫЙТИ В МЕНЮ" ? 44 : 52,
            Vector2.zero, new Vector2(680f, 132f));
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