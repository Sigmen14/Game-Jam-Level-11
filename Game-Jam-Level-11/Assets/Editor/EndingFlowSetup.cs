#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotently authors the story-ending interface inside the existing UI_HUD prefab.
/// It reuses the current Canvas and EventSystem and never creates gameplay objects.
/// </summary>
public static class EndingFlowSetup
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string HudPrefabPath = "Assets/Prefabs/UI/UI_HUD.prefab";
    private const string GlowSpritePath = "Assets/Art/LastFlame/MenuButtonGlow.png";
    private const string SparkSpritePath = "Assets/Art/LastFlame/MenuButtonSpark.png";
    private const int ButtonSparkCount = 6;

    [MenuItem("Game Jam/Setup Last Ember Story Endings")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Last Ember",
                "Остановите Play Mode перед настройкой сюжетных концовок.",
                "OK");
            return;
        }

        try
        {
            ConfigureHudPrefab();

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameManager manager = FindInScene<GameManager>(scene);
            EndingFlowController endingFlow = FindInScene<EndingFlowController>(scene);

            if (manager == null || endingFlow == null)
            {
                throw new System.InvalidOperationException(
                    "В Main.unity должны находиться существующие GameManager и UI_HUD с EndingFlowController.");
            }

            SetObjectReference(manager, "endingFlowController", endingFlow);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Last Ember",
                "Сюжетный экран выбора и обе концовки настроены в существующем HUD. Сцена Main сохранена.",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Last Ember — ошибка настройки", exception.Message, "OK");
        }
    }

    /// <summary>Called by the existing Game Flow setup so the new UI is part of the standard setup.</summary>
    public static void ConfigureHudPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        if (root == null)
            throw new System.InvalidOperationException("Не удалось открыть " + HudPrefabPath + ".");

        try
        {
            if (root.GetComponent<Canvas>() == null)
                throw new System.InvalidOperationException(
                    "UI_HUD.prefab должен содержать существующий Canvas. Новый Canvas не создаётся.");

            // The existing HUD Canvas root was serialized with a zero transform scale.
            // Restore its normal scale so both the pre-existing HUD and the authored
            // story panels remain visible in the existing screen-space canvas.
            root.transform.localScale = Vector3.one;

            Sprite uiSprite = LoadUiSprite();
            Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/LastFlame/SoftCircle.asset");
            if (circleSprite == null)
                circleSprite = uiSprite;

            Sprite glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
            if (glowSprite == null)
                glowSprite = circleSprite;

            Sprite sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SparkSpritePath);
            if (sparkSprite == null)
                sparkSprite = glowSprite;

            Font font = LoadBuiltinFont();

            GameObject choiceRoot = GetOrCreatePanel(
                root.transform,
                "EndingChoicePanel",
                uiSprite,
                new Color(0.012f, 0.022f, 0.038f, 1f));

            GameObject sequenceRoot = GetOrCreatePanel(
                root.transform,
                "EndingSequencePanel",
                uiSprite,
                new Color(0.005f, 0.012f, 0.026f, 0.94f));

            ChoiceReferences choice = ConfigureChoicePanel(
                choiceRoot.transform, uiSprite, circleSprite, glowSprite, sparkSprite, font);

            SequenceReferences sequence = ConfigureSequencePanel(
                sequenceRoot.transform, uiSprite, circleSprite, glowSprite, font);

            EndingFlowController controller = root.GetComponent<EndingFlowController>();
            if (controller == null)
                controller = root.AddComponent<EndingFlowController>();

            EndingSequenceView sequenceView = root.GetComponent<EndingSequenceView>();
            if (sequenceView == null)
                sequenceView = root.AddComponent<EndingSequenceView>();

            SetObjectReference(controller, "choiceRoot", choiceRoot);
            SetObjectReference(controller, "choiceDescriptionText", choice.descriptionText);
            SetObjectReference(controller, "rekindleWorldButton", choice.rekindleButton);
            SetObjectReference(controller, "keepEmberButton", choice.keepEmberButton);
            SetObjectReference(controller, "sequenceRoot", sequenceRoot);
            SetObjectReference(controller, "mainMenuButton", sequence.mainMenuButton);
            SetObjectReference(controller, "sequenceView", sequenceView);

            SetObjectReference(sequenceView, "atmosphere", sequence.atmosphere);
            SetObjectReference(sequenceView, "beaconCore", sequence.beaconCore);
            SetObjectReference(sequenceView, "ember", sequence.ember);
            SetObjectReference(sequenceView, "emberGlow", sequence.emberGlow);
            SetObjectReference(sequenceView, "signalWave", sequence.signalWave);
            SetObjectReference(sequenceView, "signalWaveImage", sequence.signalWaveImage);
            SetObjectArray(sequenceView, "distantBeacons", sequence.distantBeacons);
            SetObjectReference(sequenceView, "resultContentGroup", sequence.resultContentGroup);
            SetObjectReference(sequenceView, "endingTitleText", sequence.endingTitleText);
            SetObjectReference(sequenceView, "endingBodyText", sequence.endingBodyText);
            SetObjectReference(sequenceView, "statisticsText", sequence.statisticsText);

            choiceRoot.SetActive(false);
            sequenceRoot.SetActive(false);
            choiceRoot.transform.SetAsLastSibling();
            sequenceRoot.transform.SetAsLastSibling();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(sequenceView);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private struct ChoiceReferences
    {
        public Text descriptionText;
        public Button rekindleButton;
        public Button keepEmberButton;
    }

    private struct SequenceReferences
    {
        public Image atmosphere;
        public Image beaconCore;
        public RectTransform ember;
        public Image emberGlow;
        public RectTransform signalWave;
        public Image signalWaveImage;
        public Image[] distantBeacons;
        public CanvasGroup resultContentGroup;
        public Text endingTitleText;
        public Text endingBodyText;
        public Text statisticsText;
        public Button mainMenuButton;
    }

    private static ChoiceReferences ConfigureChoicePanel(
        Transform root,
        Sprite uiSprite,
        Sprite circleSprite,
        Sprite glowSprite,
        Sprite sparkSprite,
        Font font)
    {
        CreateText(
            root, "EndingChoiceTitle", "ОГОНЬ, КОТОРЫЙ НЕ ОДИН",
            font, 46, TextAnchor.MiddleCenter, new Color(1f, 0.78f, 0.38f, 1f),
            new Vector2(0f, 390f), new Vector2(1250f, 92f));

        ChoiceReferences references = new ChoiceReferences();

        references.descriptionText = CreateText(
            root, "EndingChoiceDescription",
            "Маяк не рождает огонь. Он посылает зов тем, кто хранит свой свет вдали.",
            font, 25, TextAnchor.MiddleCenter, new Color(0.91f, 0.86f, 0.75f, 1f),
            new Vector2(0f, 280f), new Vector2(1280f, 118f));

        CreateImage(
            root, "ChoiceBeaconGlow", glowSprite,
            new Color(1f, 0.40f, 0.08f, 0.23f),
            new Vector2(0f, 74f), new Vector2(280f, 280f));

        CreateImage(
            root, "ChoiceBeaconTower", uiSprite,
            new Color(0.045f, 0.075f, 0.105f, 1f),
            new Vector2(0f, 5f), new Vector2(66f, 205f));

        CreateImage(
            root, "ChoiceBeaconDeck", uiSprite,
            new Color(0.14f, 0.17f, 0.20f, 1f),
            new Vector2(0f, 115f), new Vector2(112f, 18f));

        CreateImage(
            root, "ChoiceBeaconLantern", uiSprite,
            new Color(0.27f, 0.20f, 0.12f, 1f),
            new Vector2(0f, 151f), new Vector2(64f, 54f));

        CreateImage(
            root, "ChoiceBeaconCore", circleSprite,
            new Color(1f, 0.78f, 0.30f, 1f),
            new Vector2(0f, 151f), new Vector2(28f, 28f));

        CreateImage(
            root, "ChoiceBeaconRoof", uiSprite,
            new Color(0.20f, 0.17f, 0.13f, 1f),
            new Vector2(0f, 184f), new Vector2(92f, 12f));

        CreateImage(
            root, "ChoiceLastEmberGlow", glowSprite,
            new Color(1f, 0.30f, 0.06f, 0.62f),
            new Vector2(0f, -80f), new Vector2(110f, 110f));

        CreateImage(
            root, "ChoiceLastEmber", circleSprite,
            new Color(1f, 0.77f, 0.27f, 1f),
            new Vector2(0f, -80f), new Vector2(27f, 27f));

        references.rekindleButton = CreateFireButton(
            root, "RekindleWorldButton", "Разжечь мир",
            new Vector2(0f, -235f), new Vector2(440f, 76f),
            uiSprite, glowSprite, sparkSprite, font);

        references.keepEmberButton = CreateFireButton(
            root, "KeepEmberButton", "Сохранить искру",
            new Vector2(0f, -335f), new Vector2(440f, 76f),
            uiSprite, glowSprite, sparkSprite, font);

        return references;
    }

    private static SequenceReferences ConfigureSequencePanel(
        Transform root,
        Sprite uiSprite,
        Sprite circleSprite,
        Sprite glowSprite,
        Font font)
    {
        SequenceReferences references = new SequenceReferences();

        references.atmosphere = root.GetComponent<Image>();
        if (references.atmosphere == null)
            references.atmosphere = root.gameObject.AddComponent<Image>();

        references.atmosphere.sprite = uiSprite;
        references.atmosphere.type = Image.Type.Simple;
        references.atmosphere.color = new Color(0.005f, 0.012f, 0.026f, 0.94f);
        references.atmosphere.raycastTarget = true;

        CreateImage(
            root, "BeaconTowerBody", uiSprite,
            new Color(0.035f, 0.055f, 0.082f, 1f),
            new Vector2(0f, 36f), new Vector2(60f, 240f));

        CreateImage(
            root, "BeaconTowerDeck", uiSprite,
            new Color(0.13f, 0.15f, 0.17f, 1f),
            new Vector2(0f, 151f), new Vector2(116f, 18f));

        CreateImage(
            root, "BeaconLanternRoom", uiSprite,
            new Color(0.22f, 0.16f, 0.10f, 1f),
            new Vector2(0f, 184f), new Vector2(66f, 54f));

        CreateImage(
            root, "BeaconLanternRoof", uiSprite,
            new Color(0.17f, 0.15f, 0.12f, 1f),
            new Vector2(0f, 217f), new Vector2(96f, 12f));

        references.beaconCore = CreateImage(
            root, "BeaconCore", circleSprite,
            new Color(1f, 0.48f, 0.12f, 1f),
            new Vector2(0f, 184f), new Vector2(27f, 27f));

        references.signalWaveImage = CreateImage(
            root, "SignalWave", glowSprite,
            new Color(1f, 0.66f, 0.20f, 0f),
            new Vector2(0f, 184f), new Vector2(150f, 150f));
        references.signalWave = references.signalWaveImage.rectTransform;
        references.signalWaveImage.raycastTarget = false;

        references.distantBeacons = new Image[20];
        float[] xPositions =
        {
            -850f, -735f, -620f, -500f, -385f,
            -270f, -145f, -25f, 105f, 225f,
            350f, 470f, 590f, 710f, 835f,
            -790f, -420f, -70f, 320f, 650f
        };
        float[] yPositions =
        {
            -250f, -270f, -235f, -265f, -245f,
            -280f, -255f, -272f, -248f, -270f,
            -242f, -265f, -238f, -268f, -245f,
            -315f, -325f, -335f, -320f, -330f
        };

        for (int i = 0; i < references.distantBeacons.Length; i++)
        {
            float size = i < 15 ? 18f + (i % 3) * 4f : 12f + (i % 2) * 3f;
            references.distantBeacons[i] = CreateImage(
                root,
                "DistantBeacon_" + i.ToString("00"),
                circleSprite,
                new Color(1f, 0.64f, 0.24f, 0f),
                new Vector2(xPositions[i], yPositions[i]),
                new Vector2(size, size));
        }

        references.emberGlow = CreateImage(
            root, "LastEmberGlow", glowSprite,
            new Color(1f, 0.34f, 0.08f, 0.82f),
            new Vector2(0f, -80f), new Vector2(118f, 118f));

        Image emberImage = CreateImage(
            root, "LastEmber", circleSprite,
            new Color(1f, 0.78f, 0.30f, 1f),
            new Vector2(0f, -80f), new Vector2(30f, 30f));
        references.ember = emberImage.rectTransform;

        GameObject resultRoot = GetOrCreateRectObject(root, "EndingResultContent");
        StretchToParent(resultRoot.GetComponent<RectTransform>());
        references.resultContentGroup = resultRoot.GetComponent<CanvasGroup>();
        if (references.resultContentGroup == null)
            references.resultContentGroup = resultRoot.AddComponent<CanvasGroup>();

        references.endingTitleText = CreateText(
            resultRoot.transform, "EndingResultTitle", string.Empty,
            font, 42, TextAnchor.MiddleCenter, new Color(1f, 0.80f, 0.42f, 1f),
            new Vector2(0f, -130f), new Vector2(1320f, 78f));

        references.endingBodyText = CreateText(
            resultRoot.transform, "EndingResultBody", string.Empty,
            font, 26, TextAnchor.MiddleCenter, new Color(0.97f, 0.91f, 0.79f, 1f),
            new Vector2(0f, -230f), new Vector2(1420f, 110f));

        references.statisticsText = CreateText(
            resultRoot.transform, "EndingStatistics",
            "СТАТИСТИКА ПУТЕШЕСТВИЯ",
            font, 18, TextAnchor.MiddleCenter, new Color(0.80f, 0.72f, 0.58f, 1f),
            new Vector2(0f, -355f), new Vector2(1180f, 98f));

        references.mainMenuButton = CreateFireButton(
            resultRoot.transform, "EndingMainMenuButton", "В ГЛАВНОЕ МЕНЮ",
            new Vector2(0f, -475f), new Vector2(390f, 68f),
            uiSprite, glowSprite, glowSprite, font);

        resultRoot.SetActive(false);
        references.signalWaveImage.gameObject.SetActive(true);
        return references;
    }

    private static GameObject GetOrCreatePanel(
        Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject panel = GetOrCreateRectObject(parent, name);
        StretchToParent(panel.GetComponent<RectTransform>());

        Image image = panel.GetComponent<Image>();
        if (image == null)
            image = panel.AddComponent<Image>();

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = true;

        if (panel.GetComponent<CanvasGroup>() == null)
            panel.AddComponent<CanvasGroup>();

        return panel;
    }

    private static GameObject GetOrCreateRectObject(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing.GetComponent<RectTransform>() == null)
                throw new System.InvalidOperationException(name + " должен использовать RectTransform.");
            return existing.gameObject;
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Image CreateImage(
        Transform parent, string name, Sprite sprite, Color color,
        Vector2 position, Vector2 size)
    {
        GameObject imageObject = GetOrCreateRectObject(parent, name);
        Image image = imageObject.GetComponent<Image>();
        if (image == null)
            image = imageObject.AddComponent<Image>();

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
        SetCenteredRect(image.rectTransform, position, size);
        return image;
    }

    private static Text CreateText(
        Transform parent, string name, string value, Font font, int size,
        TextAnchor alignment, Color color, Vector2 position, Vector2 rectSize)
    {
        GameObject textObject = GetOrCreateRectObject(parent, name);
        Text text = textObject.GetComponent<Text>();
        if (text == null)
            text = textObject.AddComponent<Text>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(12, size - 8);
        text.resizeTextMaxSize = size;
        SetCenteredRect(text.rectTransform, position, rectSize);
        return text;
    }

    private static Button CreateFireButton(
        Transform parent, string name, string label, Vector2 position, Vector2 size,
        Sprite uiSprite, Sprite glowSprite, Sprite sparkSprite, Font font)
    {
        GameObject buttonObject = GetOrCreateRectObject(parent, name);
        Image background = buttonObject.GetComponent<Image>();
        if (background == null)
            background = buttonObject.AddComponent<Image>();

        background.sprite = uiSprite;
        background.type = Image.Type.Simple;
        background.color = new Color(0.24f, 0.105f, 0.045f, 1f);
        background.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.32f, 0.13f, 0.045f, 1f);
        colors.highlightedColor = new Color(0.62f, 0.25f, 0.055f, 1f);
        colors.pressedColor = new Color(0.88f, 0.43f, 0.075f, 1f);
        colors.selectedColor = new Color(0.52f, 0.21f, 0.045f, 1f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.interactable = true;
        SetCenteredRect(buttonObject.GetComponent<RectTransform>(), position, size);

        ConfigureFireAnimation(button, glowSprite, sparkSprite);
        Text labelText = CreateText(
            buttonObject.transform, "Label", label, font, 25,
            TextAnchor.MiddleCenter, new Color(1f, 0.89f, 0.70f, 1f),
            Vector2.zero, size - new Vector2(28f, 10f));
        labelText.raycastTarget = false;

        return button;
    }

    private static void ConfigureFireAnimation(Button button, Sprite glowSprite, Sprite sparkSprite)
    {
        MenuButtonFireAnimation animation = button.GetComponent<MenuButtonFireAnimation>();
        if (animation == null)
            animation = button.gameObject.AddComponent<MenuButtonFireAnimation>();

        Image glow = CreateImage(
            button.transform, "FireGlow", glowSprite,
            new Color(1f, 0.30f, 0.035f, 0f),
            Vector2.zero, Vector2.zero);
        StretchWithPadding(glow.rectTransform, new Vector2(-24f, -18f));

        Image flash = CreateImage(
            button.transform, "FireFlash", glowSprite,
            new Color(1f, 0.78f, 0.28f, 0f),
            Vector2.zero, Vector2.zero);
        StretchWithPadding(flash.rectTransform, new Vector2(-12f, -8f));

        Image[] sparks = new Image[ButtonSparkCount];
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = CreateImage(
                button.transform,
                "FireSpark_" + i.ToString("00"),
                sparkSprite,
                new Color(1f, i % 2 == 0 ? 0.78f : 0.42f, 0.12f, 0f),
                Vector2.zero,
                new Vector2(10f, 10f));
        }

        SerializedObject serialized = new SerializedObject(animation);
        SetObjectReference(serialized, "glowImage", glow);
        SetObjectReference(serialized, "flashImage", flash);
        SerializedProperty sparksProperty = serialized.FindProperty("sparkImages");
        if (sparksProperty == null)
            throw new System.InvalidOperationException("MenuButtonFireAnimation не содержит sparkImages.");

        sparksProperty.arraySize = sparks.Length;
        for (int i = 0; i < sparks.Length; i++)
            sparksProperty.GetArrayElementAtIndex(i).objectReferenceValue = sparks[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animation);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void StretchWithPadding(RectTransform rect, Vector2 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadUiSprite()
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite == null)
            sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        if (sprite == null)
            throw new System.InvalidOperationException("Не удалось загрузить стандартный спрайт интерфейса Unity.");

        return sprite;
    }

    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (font == null)
            throw new System.InvalidOperationException("Не удалось загрузить встроенный шрифт Unity.");

        return font;
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

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SetObjectReference(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObjectReference(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException(
                "Не найдено сериализованное поле " + propertyName + ".");

        property.objectReferenceValue = value;
    }

    private static void SetObjectArray<T>(Object target, string propertyName, T[] values) where T : Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException(
                "Не найдено сериализованное поле " + propertyName + ".");

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
#endif
