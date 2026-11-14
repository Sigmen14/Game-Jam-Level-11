#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastEmber.Editor
{
    /// <summary>
    /// One-time, non-destructive editor utility for the initial project scaffold.
    /// All GameObjects/components created here are created in the Unity Editor,
    /// saved as assets/scenes, and are never created by game runtime code.
    /// </summary>
    public static class ProjectSetupGenerator
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
        private const string FuelPrefabPath = "Assets/Prefabs/Gameplay/PF_FuelPickup.prefab";
        private const string CoverPrefabPath = "Assets/Prefabs/Gameplay/PF_Cover.prefab";

        private const string HudPrefabPath = "Assets/Prefabs/UI/UI_HUD.prefab";
        private const string StartPrefabPath = "Assets/Prefabs/UI/UI_StartPanel.prefab";
        private const string ResultPrefabPath = "Assets/Prefabs/UI/UI_ResultPanel.prefab";

        [MenuItem("Game Jam/Project Setup/Create Missing Setup Assets")]
        public static void CreateMissingSetupAssets()
        {
            EnsureFolder("Assets/Editor");
            EnsureFolder("Assets/Art/Sprites");
            EnsureFolder("Assets/Art/Materials");
            EnsureFolder("Assets/Audio/Music");
            EnsureFolder("Assets/Audio/SFX");
            EnsureFolder("Assets/Prefabs/Player");
            EnsureFolder("Assets/Prefabs/Gameplay");
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Scripts/Core");
            EnsureFolder("Assets/Scripts/Player");
            EnsureFolder("Assets/Scripts/Gameplay");
            EnsureFolder("Assets/Scripts/UI");
            EnsureFolder("Assets/Scenes");

            GameObject playerPrefab = EnsurePrefab(PlayerPrefabPath, CreatePlayerPrefab);
            GameObject fuelPrefab = EnsurePrefab(FuelPrefabPath, CreateFuelPrefab);
            GameObject coverPrefab = EnsurePrefab(CoverPrefabPath, CreateCoverPrefab);

            GameObject hudPrefab = EnsurePrefab(HudPrefabPath, CreateHudPrefab);
            GameObject startPrefab = EnsurePrefab(StartPrefabPath, CreateStartPanelPrefab);
            GameObject resultPrefab = EnsurePrefab(ResultPrefabPath, CreateResultPanelPrefab);

            AssetDatabase.SaveAssets();

            bool mainSceneCreated = CreateMainSceneIfMissing(
                playerPrefab,
                hudPrefab,
                startPrefab,
                resultPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string sceneMessage = mainSceneCreated
                ? "Created Main.unity from a copy of SampleScene and added it to Build Settings."
                : "Main.unity already exists (or SampleScene is unavailable); the existing scene was left untouched.";

            Debug.Log(
                "[Last Ember] Project setup finished. Missing prefabs were created; existing assets were skipped. " +
                sceneMessage);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("Folder path must be inside Assets: " + folderPath);
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static GameObject EnsurePrefab(string assetPath, Func<GameObject> factory)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                Debug.Log("[Last Ember] Preserved existing prefab: " + assetPath);
                return existing;
            }

            GameObject temporaryRoot = factory();
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, assetPath);
                if (saved == null)
                {
                    throw new InvalidOperationException("Unity could not save prefab: " + assetPath);
                }

                Debug.Log("[Last Ember] Created prefab: " + assetPath);
            }
            finally
            {
                // This destroys only the temporary editor object used to author the asset.
                // It is not part of the game's runtime loop.
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject root = new GameObject("PF_Player");

            SpriteRenderer body = root.AddComponent<SpriteRenderer>();
            body.color = new Color(1f, 0.72f, 0.24f, 1f);
            body.sortingOrder = 10;

            Rigidbody2D body2D = root.AddComponent<Rigidbody2D>();
            body2D.gravityScale = 0f;
            body2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            body2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            body2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;

            GameObject guardVisual = new GameObject("GuardVisual");
            guardVisual.transform.SetParent(root.transform, false);
            guardVisual.transform.localScale = Vector3.one * 1.45f;
            SpriteRenderer guardRenderer = guardVisual.AddComponent<SpriteRenderer>();
            guardRenderer.color = new Color(0.45f, 0.85f, 1f, 0.55f);
            guardRenderer.sortingOrder = 9;
            guardVisual.SetActive(false);

            GameObject lightObject = new GameObject("FlameLight");
            lightObject.transform.SetParent(root.transform, false);
            Light2D flameLight = lightObject.AddComponent<Light2D>();
            flameLight.lightType = Light2D.LightType.Point;
            flameLight.color = new Color(1f, 0.68f, 0.28f, 1f);
            flameLight.intensity = 1f;
            flameLight.pointLightInnerRadius = 1.25f;
            flameLight.pointLightOuterRadius = 3f;

            return root;
        }

        private static GameObject CreateFuelPrefab()
        {
            GameObject root = new GameObject("PF_FuelPickup");

            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.color = new Color(1f, 0.82f, 0.2f, 1f);
            spriteRenderer.sortingOrder = 5;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.42f;

            return root;
        }

        private static GameObject CreateCoverPrefab()
        {
            GameObject root = new GameObject("PF_Cover");

            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.color = new Color(0.24f, 0.22f, 0.2f, 1f);
            spriteRenderer.sortingOrder = 2;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.8f, 1f);

            return root;
        }

        private static GameObject CreateHudPrefab()
        {
            GameObject root = CreatePanelRoot("UI_HUD", new Color(0.035f, 0.045f, 0.065f, 0.84f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -18f);
            rootRect.sizeDelta = new Vector2(-48f, 118f);

            CreateText(
                root.transform,
                "FlameLabel",
                "ОГОНЬ",
                new Vector2(0.05f, 0.56f),
                new Vector2(0.48f, 0.9f),
                24,
                TextAnchor.MiddleLeft,
                Color.white);

            GameObject barTrack = CreateImage(
                "FlameBar",
                root.transform,
                new Vector2(0.05f, 0.2f),
                new Vector2(0.55f, 0.5f),
                new Color(0.12f, 0.13f, 0.16f, 1f));

            GameObject barFill = CreateImage(
                "FlameFill",
                barTrack.transform,
                Vector2.zero,
                Vector2.one,
                new Color(1f, 0.58f, 0.16f, 1f));

            Image fillImage = barFill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0.75f;

            CreateText(
                root.transform,
                "TimeText",
                "00:00 / 03:00",
                new Vector2(0.68f, 0.42f),
                new Vector2(0.96f, 0.86f),
                28,
                TextAnchor.MiddleRight,
                Color.white);

            GameObject warning = CreateText(
                root.transform,
                "WindWarning",
                "ВНИМАНИЕ: ВЕТЕР!",
                new Vector2(0.32f, 0.02f),
                new Vector2(0.68f, 0.36f),
                22,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.38f, 0.24f, 1f));
            warning.SetActive(false);

            return root;
        }

        private static GameObject CreateStartPanelPrefab()
        {
            GameObject root = CreatePanelRoot("UI_StartPanel", new Color(0.025f, 0.035f, 0.055f, 0.94f));
            SetCenteredPanelRect(root.GetComponent<RectTransform>(), new Vector2(760f, 500f));

            CreateText(
                root.transform,
                "TitleText",
                "ПОСЛЕДНИЙ ОГОНЁК",
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.93f),
                42,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.75f, 0.35f, 1f));

            CreateText(
                root.transform,
                "InstructionsText",
                "WASD / стрелки — движение\nУдерживай SPACE — прикрыть пламя\nСобирай топливо и переживи 180 секунд",
                new Vector2(0.1f, 0.36f),
                new Vector2(0.9f, 0.7f),
                23,
                TextAnchor.MiddleCenter,
                Color.white);

            CreateButton(
                root.transform,
                "StartButton",
                "НАЧАТЬ",
                new Vector2(0.28f, 0.12f),
                new Vector2(0.72f, 0.29f));

            return root;
        }

        private static GameObject CreateResultPanelPrefab()
        {
            GameObject root = CreatePanelRoot("UI_ResultPanel", new Color(0.025f, 0.035f, 0.055f, 0.95f));
            SetCenteredPanelRect(root.GetComponent<RectTransform>(), new Vector2(760f, 460f));

            CreateText(
                root.transform,
                "ResultTitleText",
                "ИГРА ОКОНЧЕНА",
                new Vector2(0.08f, 0.68f),
                new Vector2(0.92f, 0.91f),
                40,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.75f, 0.35f, 1f));

            CreateText(
                root.transform,
                "SurvivalTimeText",
                "Время выживания: 00:00",
                new Vector2(0.1f, 0.45f),
                new Vector2(0.9f, 0.65f),
                25,
                TextAnchor.MiddleCenter,
                Color.white);

            CreateButton(
                root.transform,
                "RetryButton",
                "ЕЩЁ РАЗ",
                new Vector2(0.28f, 0.12f),
                new Vector2(0.72f, 0.31f));

            return root;
        }

        private static GameObject CreatePanelRoot(string name, Color color)
        {
            GameObject root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            Image image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            return root;
        }

        private static void SetCenteredPanelRect(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static GameObject CreateImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return imageObject;
        }

        private static GameObject CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));

            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = GetBuiltinFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return textObject;
        }

        private static GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.72f, 0.32f, 0.12f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            CreateText(
                buttonObject.transform,
                "Label",
                label,
                Vector2.zero,
                Vector2.one,
                24,
                TextAnchor.MiddleCenter,
                Color.white);

            return buttonObject;
        }

        private static Font GetBuiltinFont()
        {
            // Unity 6 uses LegacyRuntime.ttf as the built-in font for legacy uGUI Text.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Debug.LogWarning("[Last Ember] Built-in LegacyRuntime font was not found. Assign a font to the generated UI Text components.");
            }

            return font;
        }

        private static bool CreateMainSceneIfMissing(
            GameObject playerPrefab,
            GameObject hudPrefab,
            GameObject startPrefab,
            GameObject resultPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) != null)
            {
                Debug.Log("[Last Ember] Existing Main.unity was preserved without changes.");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) == null)
            {
                Debug.LogWarning("[Last Ember] SampleScene.unity was not found. Main.unity was not created.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Last Ember] Scene creation was cancelled so unsaved scene changes remain untouched.");
                return false;
            }

            if (!AssetDatabase.CopyAsset(SampleScenePath, MainScenePath))
            {
                Debug.LogError("[Last Ember] Could not copy SampleScene.unity to Main.unity.");
                return false;
            }

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            GameObject mainRoot = new GameObject("Main");
            GameObject[] originalRoots = scene.GetRootGameObjects();
            foreach (GameObject originalRoot in originalRoots)
            {
                if (originalRoot != mainRoot)
                {
                    originalRoot.transform.SetParent(mainRoot.transform, true);
                }
            }

            GameObject level = CreateChild("Level", mainRoot.transform);
            CreateChild("Walls", level.transform);

            GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
            if (playerInstance != null)
            {
                playerInstance.name = "Player";
                playerInstance.transform.SetParent(mainRoot.transform, false);
                playerInstance.transform.position = Vector3.zero;
            }

            GameObject gameSystems = CreateChild("GameSystems", mainRoot.transform);
            CreateChild("GameManager", gameSystems.transform);
            CreateChild("WindController", gameSystems.transform);

            GameObject uiRoot = CreateChild("UI", mainRoot.transform);
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(uiRoot.transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            InstantiateUiPrefab(hudPrefab, canvas.transform, "UI_HUD", false, scene);
            InstantiateUiPrefab(startPrefab, canvas.transform, "UI_StartPanel", true, scene);
            InstantiateUiPrefab(resultPrefab, canvas.transform, "UI_ResultPanel", false, scene);

            if (!SceneContainsEventSystem(scene))
            {
                GameObject eventSystemObject = CreateChild("EventSystem", mainRoot.transform);
                eventSystemObject.AddComponent<EventSystem>();
                // The Input System module assigns its default UI actions when enabled.
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[Last Ember] Main.unity was created but could not be saved.");
                return false;
            }

            AddSceneToBuildSettings(MainScenePath);
            Debug.Log("[Last Ember] Created Main.unity. SampleScene.unity remains unchanged.");
            return true;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void InstantiateUiPrefab(
            GameObject prefab,
            Transform parent,
            string instanceName,
            bool active,
            Scene targetScene)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, targetScene) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[Last Ember] Could not instantiate UI prefab: " + instanceName);
                return;
            }

            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.SetActive(active);
        }

        private static bool SceneContainsEventSystem(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<EventSystem>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            bool alreadyIncluded = scenes.Any(scene => scene.path == scenePath);

            if (!alreadyIncluded)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
