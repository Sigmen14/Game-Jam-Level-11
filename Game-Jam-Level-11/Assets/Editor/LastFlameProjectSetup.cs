#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time authoring utility. Run from the Unity Editor menu to create/save all
/// prefab assets and the Main scene. No code in this file runs in a player build.
/// </summary>
public static class LastFlameProjectSetup
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string PickupPrefabPath = "Assets/Prefabs/Pickups/PickupRoot.prefab";
    private const string CoverPrefabPath = "Assets/Prefabs/Environment/EnvironmentRoot.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/UI_HUD.prefab";
    private const string FlameWidgetPath = "Assets/Prefabs/UI/UI_FlameMeter.prefab";
    private const string TimerWidgetPath = "Assets/Prefabs/UI/UI_Timer.prefab";
    private const string WindWidgetPath = "Assets/Prefabs/UI/UI_WindWarning.prefab";
    private const string StartPanelPath = "Assets/Prefabs/UI/UI_StartPanel.prefab";
    private const string ResultPanelPath = "Assets/Prefabs/UI/UI_ResultPanel.prefab";
    private const string ArtFolder = "Assets/Art/LastFlame";
    private const string SettingsFolder = "Assets/Settings/InputActionReferences";

    private static Sprite _circleSprite;
    private static Sprite _squareSprite;
    private static Material _litMaterial;
    private static Font _font;

    [MenuItem("Game Jam/Build Last Flame MVP")]
    public static void BuildLastFlameMvp()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Last Flame", "Stop Play Mode before building the scene and prefabs.", "OK");
            return;
        }

        try
        {
            EnsureProjectFolders();
            AssetDatabase.Refresh();

            _circleSprite = CreateSpriteAsset(ArtFolder + "/SoftCircle.asset", true);
            _squareSprite = CreateSpriteAsset(ArtFolder + "/SoftSquare.asset", false);
            _litMaterial = CreateLitMaterial();
            _font = LoadBuiltinFont();

            GameObject playerPrefab = CreatePlayerPrefab();
            GameObject pickupPrefab = CreatePickupPrefab();
            GameObject coverPrefab = CreateCoverPrefab();

            GameObject flameWidget = CreateFlameWidget();
            GameObject timerWidget = CreateTimerWidget();
            GameObject windWidget = CreateWindWidget();
            GameObject startPanel = CreatePanelPrefab(
                StartPanelPath,
                "ПОСЛЕДНИЙ ОГОНЁК",
                "Продержись 180 секунд. Собирай топливо и не дай пламени погаснуть.",
                "WASD — движение     SPACE — защита (медленнее)",
                "НАЧАТЬ");
            GameObject resultPanel = CreatePanelPrefab(
                ResultPanelPath,
                "ОГОНЁК ПОГАС",
                "Каждая искра имеет значение.",
                "",
                "ЕЩЁ РАЗ");

            GameObject hudPrefab = CreateHudPrefab(
                flameWidget, timerWidget, windWidget, startPanel, resultPanel);

            BuildMainScene(playerPrefab, pickupPrefab, coverPrefab, hudPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Last Flame",
                "Main scene and prefab assets have been authored. Open Assets/Scenes/Main.unity and press Play.",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Last Flame — setup failed", exception.Message, "OK");
        }
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder(SettingsFolder);
        EnsureFolder("Assets/Editor");
        EnsureFolder("Assets/Scripts/Core");
        EnsureFolder("Assets/Scripts/UI");
        EnsureFolder("Assets/Prefabs/UI");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Sprite CreateSpriteAsset(string path, bool circle)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    if (circle)
                    {
                        float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                        float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = 1f - Mathf.SmoothStep(0.88f, 1f, distance);
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                64f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }

        return sprite;
    }

    private static Material CreateLitMaterial()
    {
        const string materialPath = ArtFolder + "/LastFlameSpriteLit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
        {
            Debug.LogWarning("Sprite-Lit-Default shader was not found. Unity's default SpriteRenderer material will be used.");
            return null;
        }

        material = new Material(shader) { name = "LastFlameSpriteLit" };
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font == null)
        {
            throw new System.InvalidOperationException("Unity's built-in UI font could not be loaded.");
        }

        return font;
    }

    private static GameObject CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        root.tag = "Player";

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = _circleSprite;
        renderer.color = new Color(1f, 0.67f, 0.22f, 1f);
        renderer.sortingOrder = 10;
        renderer.sharedMaterial = _litMaterial;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.23f;

        GameObject protection = new GameObject("ProtectionVisual");
        protection.transform.SetParent(root.transform, false);
        protection.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
        SpriteRenderer protectionRenderer = protection.AddComponent<SpriteRenderer>();
        protectionRenderer.sprite = _circleSprite;
        protectionRenderer.color = new Color(0.25f, 0.8f, 1f, 0.28f);
        protectionRenderer.sortingOrder = 9;
        protectionRenderer.sharedMaterial = _litMaterial;
        protection.SetActive(false);

        GameObject lightObject = new GameObject("FlameLight");
        lightObject.transform.SetParent(root.transform, false);
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.68f, 0.3f, 1f);
        light.intensity = 1.6f;
        light.pointLightInnerRadius = 1.2f;
        light.pointLightOuterRadius = 7.5f;
        light.falloffIntensity = 0.65f;

        PlayerController playerController = root.AddComponent<PlayerController>();
        FlameMeter flameMeter = root.AddComponent<FlameMeter>();

        SetObjectReference(playerController, "body", body);
        InputActionAsset playerInputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (playerInputAsset == null)
        {
            throw new System.InvalidOperationException("InputSystem_Actions.inputactions could not be loaded. Wait for Unity to finish importing assets, then run setup again.");
        }

        SetObjectReference(playerController, "inputActions", playerInputAsset);
        SetObjectReference(playerController, "protectionVisual", protection);

        SetObjectReference(flameMeter, "flameLight", light);
        SetObjectReference(flameMeter, "playerController", playerController);

        return SavePrefabAndDestroy(root, PlayerPrefabPath);
    }

    private static GameObject CreatePickupPrefab()
    {
        GameObject root = new GameObject("PickupRoot");

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = _circleSprite;
        renderer.color = new Color(1f, 0.72f, 0.18f, 1f);
        renderer.sortingOrder = 6;
        renderer.sharedMaterial = _litMaterial;

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.34f;

        FuelPickup pickup = root.AddComponent<FuelPickup>();
        SetObjectReference(pickup, "pickupCollider", collider);
        SetObjectReference(pickup, "pickupVisual", renderer);
        SetFloat(pickup, "fuelAmount", 25f);
        SetFloat(pickup, "respawnDelay", 8f);

        return SavePrefabAndDestroy(root, PickupPrefabPath);
    }

    private static GameObject CreateCoverPrefab()
    {
        GameObject root = new GameObject("EnvironmentRoot");

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = _squareSprite;
        renderer.color = new Color(0.25f, 0.3f, 0.39f, 1f);
        renderer.sortingOrder = 4;
        renderer.sharedMaterial = _litMaterial;
        root.transform.localScale = new Vector3(1.2f, 0.75f, 1f);

        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(3.2f, 2.0f);

        CoverZone coverZone = root.AddComponent<CoverZone>();
        SetString(coverZone, "playerTag", "Player");

        return SavePrefabAndDestroy(root, CoverPrefabPath);
    }

    private static GameObject CreateFlameWidget()
    {
        GameObject root = CreateRectRoot("UI_FlameMeter", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(340f, 86f));

        Image background = root.AddComponent<Image>();
        background.sprite = _squareSprite;
        background.color = new Color(0.035f, 0.055f, 0.085f, 0.9f);

        CreateText(root.transform, "Caption", "ЗАПАС ОГНЯ", 18, TextAnchor.UpperLeft,
            new Color(1f, 0.83f, 0.57f, 1f),
            new Vector2(14f, -8f), new Vector2(190f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Text valueText = CreateText(root.transform, "Value", "75 / 100", 16, TextAnchor.UpperRight,
            Color.white, new Vector2(-14f, -8f), new Vector2(112f, 26f),
            new Vector2(1f, 1f), new Vector2(1f, 1f));

        GameObject sliderRoot = CreateRectRoot("FlameSlider", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(14f, -46f), new Vector2(312f, 24f));
        sliderRoot.transform.SetParent(root.transform, false);

        Image sliderBackground = sliderRoot.AddComponent<Image>();
        sliderBackground.sprite = _squareSprite;
        sliderBackground.color = new Color(0.12f, 0.14f, 0.18f, 1f);

        GameObject fillObject = CreateRectRoot("Fill", Vector2.zero, Vector2.one,
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fillObject.transform.SetParent(sliderRoot.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.sprite = _squareSprite;
        fillImage.color = new Color(1f, 0.67f, 0.18f, 1f);

        Slider slider = sliderRoot.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.75f;
        slider.fillRect = fillRect;
        slider.targetGraphic = sliderBackground;
        slider.interactable = false;

        GameObject prefab = SavePrefabAndDestroy(root, FlameWidgetPath);
        return prefab;
    }

    private static GameObject CreateTimerWidget()
    {
        GameObject root = CreateRectRoot("UI_Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(170f, 58f));

        Image background = root.AddComponent<Image>();
        background.sprite = _squareSprite;
        background.color = new Color(0.035f, 0.055f, 0.085f, 0.9f);

        CreateText(root.transform, "Time", "03:00", 28, TextAnchor.MiddleCenter,
            new Color(1f, 0.91f, 0.76f, 1f), Vector2.zero, new Vector2(160f, 52f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        return SavePrefabAndDestroy(root, TimerWidgetPath);
    }

    private static GameObject CreateWindWidget()
    {
        GameObject root = CreateRectRoot("UI_WindWarning", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(360f, 54f));

        Image background = root.AddComponent<Image>();
        background.sprite = _squareSprite;
        background.color = new Color(0.75f, 0.22f, 0.08f, 0.9f);

        CreateText(root.transform, "Warning", "ВЕТЕР ПОДНИМАЕТСЯ", 20, TextAnchor.MiddleCenter,
            Color.white, Vector2.zero, new Vector2(350f, 50f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        return SavePrefabAndDestroy(root, WindWidgetPath);
    }

    private static GameObject CreatePanelPrefab(
        string path, string title, string detail, string controls, string buttonText)
    {
        GameObject root = CreateRectRoot(Path.GetFileNameWithoutExtension(path),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image shade = root.AddComponent<Image>();
        shade.sprite = _squareSprite;
        shade.color = new Color(0.015f, 0.025f, 0.045f, 0.94f);

        CreateText(root.transform, "Title", title, 52, TextAnchor.MiddleCenter,
            new Color(1f, 0.77f, 0.38f, 1f), new Vector2(0f, 140f), new Vector2(1300f, 100f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        CreateText(root.transform, "Detail", detail, 25, TextAnchor.MiddleCenter,
            new Color(0.9f, 0.92f, 0.96f, 1f), new Vector2(0f, 38f), new Vector2(1100f, 80f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        if (!string.IsNullOrEmpty(controls))
        {
            CreateText(root.transform, "Controls", controls, 20, TextAnchor.MiddleCenter,
                new Color(0.66f, 0.75f, 0.84f, 1f), new Vector2(0f, -48f), new Vector2(1100f, 54f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        GameObject buttonObject = CreateRectRoot("ActionButton", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -160f), new Vector2(270f, 66f));
        buttonObject.transform.SetParent(root.transform, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = _squareSprite;
        buttonImage.color = new Color(0.95f, 0.55f, 0.16f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        CreateText(buttonObject.transform, "Label", buttonText, 22, TextAnchor.MiddleCenter,
            new Color(0.08f, 0.06f, 0.04f, 1f), Vector2.zero, new Vector2(260f, 62f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        return SavePrefabAndDestroy(root, path);
    }

    private static GameObject CreateHudPrefab(
        GameObject flameWidget,
        GameObject timerWidget,
        GameObject windWidget,
        GameObject startPanel,
        GameObject resultPanel)
    {
        GameObject root = CreateRectRoot("UI_HUD", Vector2.zero, Vector2.one,
            new Vector2(0f, 0f), Vector2.zero, Vector2.zero);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject hudContent = CreateRectRoot("HUD_Content", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hudContent.transform.SetParent(root.transform, false);

        GameObject flameInstance = InstantiatePrefabUnder(flameWidget, hudContent.transform);
        GameObject timerInstance = InstantiatePrefabUnder(timerWidget, hudContent.transform);
        GameObject windInstance = InstantiatePrefabUnder(windWidget, hudContent.transform);
        GameObject startInstance = InstantiatePrefabUnder(startPanel, root.transform);
        GameObject resultInstance = InstantiatePrefabUnder(resultPanel, root.transform);
        resultInstance.SetActive(false);

        HUDView view = root.AddComponent<HUDView>();
        Slider flameSlider = flameInstance.GetComponentInChildren<Slider>(true);
        Image flameFill = flameInstance.transform.Find("FlameSlider/Fill").GetComponent<Image>();
        Text flameValue = flameInstance.transform.Find("Value").GetComponent<Text>();
        Text timerText = timerInstance.GetComponentInChildren<Text>(true);
        Text windText = windInstance.GetComponentInChildren<Text>(true);
        Text resultTitle = resultInstance.transform.Find("Title").GetComponent<Text>();
        Text resultTime = resultInstance.transform.Find("Detail").GetComponent<Text>();
        Button startButton = startInstance.GetComponentInChildren<Button>(true);
        Button retryButton = resultInstance.GetComponentInChildren<Button>(true);

        SetObjectReference(view, "flameSlider", flameSlider);
        SetObjectReference(view, "flameFillImage", flameFill);
        SetObjectReference(view, "flameValueText", flameValue);
        SetObjectReference(view, "timerText", timerText);
        SetObjectReference(view, "windWarningRoot", windInstance);
        SetObjectReference(view, "windWarningText", windText);
        SetObjectReference(view, "hudContentRoot", hudContent);
        SetObjectReference(view, "startPanel", startInstance);
        SetObjectReference(view, "resultPanel", resultInstance);
        SetObjectReference(view, "resultTitleText", resultTitle);
        SetObjectReference(view, "resultTimeText", resultTime);
        SetObjectReference(view, "startButton", startButton);
        SetObjectReference(view, "retryButton", retryButton);

        GameObject prefab = SavePrefabAndDestroy(root, HudPrefabPath);
        return prefab;
    }

    private static void BuildMainScene(
        GameObject playerPrefab,
        GameObject pickupPrefab,
        GameObject coverPrefab,
        GameObject hudPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "EventSystem")
            {
                Object.DestroyImmediate(root);
            }
        }

        CreateCameraAndGlobalLight();
        GameObject levelObject = BuildRoom();
        Transform levelRoot = levelObject.transform;

        GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerObject.name = "Player";
        playerObject.transform.position = Vector3.zero;
        PlayerController player = playerObject.GetComponent<PlayerController>();
        FlameMeter flameMeter = playerObject.GetComponent<FlameMeter>();

        GameObject systemsRoot = new GameObject("GameSystems");
        WindController wind = systemsRoot.AddComponent<WindController>();
        SetFloat(wind, "intervalBetweenGusts", 12f);
        SetFloat(wind, "warningDuration", 1.2f);
        SetFloat(wind, "gustDuration", 2f);

        SetObjectReference(flameMeter, "playerController", player);
        SetObjectReference(flameMeter, "windController", wind);

        Vector3[] fuelPositions =
        {
            new Vector3(-6.0f, 3.0f, 0f),
            new Vector3(6.0f, 3.0f, 0f),
            new Vector3(-6.0f, -3.0f, 0f),
            new Vector3(6.0f, -3.0f, 0f)
        };

        FuelPickup[] pickups = new FuelPickup[fuelPositions.Length];
        for (int i = 0; i < fuelPositions.Length; i++)
        {
            GameObject pickupObject = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab);
            pickupObject.transform.SetParent(levelRoot, false);
            pickupObject.name = "FuelPoint_" + (i + 1).ToString("00");
            pickupObject.transform.position = fuelPositions[i];
            FuelPickup pickup = pickupObject.GetComponent<FuelPickup>();
            SetObjectReference(pickup, "flameMeter", flameMeter);
            pickups[i] = pickup;
        }

        Vector3[] coverPositions =
        {
            new Vector3(-3.9f, 1.8f, 0f),
            new Vector3(3.9f, 1.8f, 0f),
            new Vector3(-3.9f, -1.8f, 0f),
            new Vector3(3.9f, -1.8f, 0f)
        };

        for (int i = 0; i < coverPositions.Length; i++)
        {
            GameObject coverObject = (GameObject)PrefabUtility.InstantiatePrefab(coverPrefab);
            coverObject.transform.SetParent(levelRoot, false);
            coverObject.name = "Cover_" + (i + 1).ToString("00");
            coverObject.transform.position = coverPositions[i];

            CoverZone cover = coverObject.GetComponent<CoverZone>();
            UnityEventTools.AddPersistentListener(cover.CoverStateChanged, player.SetInCover);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cover);
        }

        GameObject hudObject = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
        hudObject.name = "UI_HUD";
        HUDView hudView = hudObject.GetComponent<HUDView>();

        GameManager gameManager = systemsRoot.AddComponent<GameManager>();
        SetFloat(gameManager, "matchDuration", 180f);
        SetObjectReference(gameManager, "playerController", player);
        SetObjectReference(gameManager, "flameMeter", flameMeter);
        SetObjectReference(gameManager, "windController", wind);
        SetObjectReference(gameManager, "hudView", hudView);
        SetObjectArray(gameManager, "fuelPickups", pickups);

        ConfigureEventSystem();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputAsset != null)
        {
            EditorBuildSettings.AddConfigObject("com.unity.input.settings.actions", inputAsset, true);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = systemsRoot;
    }

    private static void CreateCameraAndGlobalLight()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.008f, 0.014f, 0.028f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraObject.AddComponent<AudioListener>();

        GameObject globalLightObject = new GameObject("Global Light 2D");
        Light2D globalLight = globalLightObject.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.color = new Color(0.53f, 0.61f, 0.78f, 1f);
        globalLight.intensity = 0.14f;
    }

    private static GameObject BuildRoom()
    {
        GameObject level = new GameObject("Level");

        GameObject floor = CreateWorldSprite("RoomFloor", level.transform, Vector3.zero,
            new Vector3(16.3f, 9.2f, 1f), new Color(0.19f, 0.23f, 0.31f, 1f), _squareSprite, -10);
        floor.transform.position = new Vector3(0f, 0f, 1f);

        CreateWall(level.transform, "Wall_Top", new Vector3(0f, 4.75f, 0f), new Vector2(17.1f, 0.42f));
        CreateWall(level.transform, "Wall_Bottom", new Vector3(0f, -4.75f, 0f), new Vector2(17.1f, 0.42f));
        CreateWall(level.transform, "Wall_Left", new Vector3(-8.35f, 0f, 0f), new Vector2(0.42f, 9.9f));
        CreateWall(level.transform, "Wall_Right", new Vector3(8.35f, 0f, 0f), new Vector2(0.42f, 9.9f));
        return level;
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector2 size)
    {
        GameObject wall = CreateWorldSprite(name, parent, position,
            new Vector3(size.x, size.y, 1f), new Color(0.12f, 0.16f, 0.23f, 1f), _squareSprite, 2);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private static GameObject CreateWorldSprite(
        string name, Transform parent, Vector3 position, Vector3 scale, Color color, Sprite sprite, int sortingOrder)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = _litMaterial;
        return gameObject;
    }

    private static void ConfigureEventSystem()
    {
        GameObject eventSystemObject = GameObject.Find("EventSystem");
        if (eventSystemObject == null)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        }

        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
        if (eventSystem == null)
        {
            eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            Object.DestroyImmediate(legacyModule);
        }

        InputSystemUIInputModule module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        if (module == null)
        {
            module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (asset == null)
        {
            Debug.LogError("InputSystem_Actions.inputactions could not be loaded.");
            return;
        }

        SetObjectReference(module, "m_ActionsAsset", asset);
        SetInputModuleReference(module, "m_PointAction", "UI/Point");
        SetInputModuleReference(module, "m_MoveAction", "UI/Navigate");
        SetInputModuleReference(module, "m_SubmitAction", "UI/Submit");
        SetInputModuleReference(module, "m_CancelAction", "UI/Cancel");
        SetInputModuleReference(module, "m_LeftClickAction", "UI/Click");
        SetInputModuleReference(module, "m_MiddleClickAction", "UI/MiddleClick");
        SetInputModuleReference(module, "m_RightClickAction", "UI/RightClick");
        SetInputModuleReference(module, "m_ScrollWheelAction", "UI/ScrollWheel");
        SetInputModuleReference(module, "m_TrackedDevicePositionAction", "UI/TrackedDevicePosition");
        SetInputModuleReference(module, "m_TrackedDeviceOrientationAction", "UI/TrackedDeviceOrientation");
    }

    private static void SetInputModuleReference(InputSystemUIInputModule module, string propertyName, string actionPath)
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputAction action = asset != null ? asset.FindAction(actionPath, false) : null;
        if (action == null)
        {
            return;
        }

        string safeName = actionPath.Replace("/", "_");
        string referencePath = SettingsFolder + "/" + safeName + ".asset";
        InputActionReference reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(referencePath);
        if (reference == null)
        {
            reference = InputActionReference.Create(action);
            reference.name = safeName;
            AssetDatabase.CreateAsset(reference, referencePath);
        }

        SetObjectReference(module, propertyName, reference);
    }

    private static GameObject CreateRectRoot(
        string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return gameObject;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        int fontSize,
        TextAnchor alignment,
        Color color,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = CreateRectRoot(name, anchorMin, anchorMax, new Vector2(0.5f, 0.5f),
            anchoredPosition, size);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject InstantiatePrefabUnder(GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static GameObject SavePrefabAndDestroy(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        if (target == null)
        {
            throw new System.ArgumentNullException(nameof(target), "Cannot assign a serialized reference on a null object.");
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException(
                target.GetType().Name + " is missing serialized field '" + propertyName + "'.");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException(
                target.GetType().Name + " is missing serialized field '" + propertyName + "'.");
        }

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException(
                target.GetType().Name + " is missing serialized field '" + propertyName + "'.");
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static void SetObjectArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            throw new System.InvalidOperationException(
                target.GetType().Name + " is missing serialized array '" + propertyName + "'.");
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
#endif
