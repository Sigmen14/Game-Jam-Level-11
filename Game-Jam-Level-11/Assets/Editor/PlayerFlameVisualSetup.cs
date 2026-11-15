#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the flame feedback visuals to the existing Player prefab and wires the
/// scene-only WindController and FuelPickup references without editing YAML.
/// Safe to run more than once.
/// </summary>
public static class PlayerFlameVisualSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string SparkMaterialPath = "Assets/Art/LastFlame/FlameSparkUnlit.mat";
    private const string SparkTexturePath = "Assets/Art/LastFlame/FlameSparkTexture.asset";

    [MenuItem("Game Jam/Setup Player Flame Visual Feedback")]
    public static void SetupPlayerFlameVisualFeedback()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Player Flame Visual Feedback",
                "Stop Play Mode before configuring the Player prefab and Main scene.",
                "OK");
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            ConfigurePlayerPrefab(prefabContents);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // The current Main scene can be missing the in-scene menu. In that state
        // GameManager never calls StartGame, so PlayerController correctly remains
        // inactive and no keyboard input can move the player.
        GameFlowUISetup.Setup();

        Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        PlayerController player = FindInScene<PlayerController>(mainScene);
        WindController wind = FindInScene<WindController>(mainScene);

        if (player == null || wind == null)
        {
            throw new System.InvalidOperationException(
                "Main.unity must contain the existing Player and GameSystems/WindController objects.");
        }

        FlameVisualFeedback feedback = player.GetComponent<FlameVisualFeedback>();
        if (feedback == null)
        {
            throw new System.InvalidOperationException(
                "FlameVisualFeedback was not added to the Player prefab. Reimport scripts and run Setup again.");
        }

        List<FuelPickup> pickups = new List<FuelPickup>();
        foreach (GameObject sceneRoot in mainScene.GetRootGameObjects())
        {
            pickups.AddRange(sceneRoot.GetComponentsInChildren<FuelPickup>(true));
        }

        pickups = pickups
            .Where(pickup => pickup != null)
            .OrderBy(pickup => pickup.name)
            .ToList();

        SetObjectReference(feedback, "windController", wind);
        SetObjectArray(feedback, "fuelPickups", pickups.ConvertAll(pickup => (Object)pickup).ToArray());
        PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);

        EditorSceneManager.MarkSceneDirty(mainScene);
        EditorSceneManager.SaveScene(mainScene);
        AssetDatabase.SaveAssets();

        ValidateGameplayInputWiring(player, wind, feedback);
        ValidateParticleSetup(feedback);

        EditorUtility.DisplayDialog(
            "Player Flame Visual Feedback",
            "Готово: UI_Menu связан с GameManager, WASD/стрелки найдены, ParticleSystem включён и назначен огненный URP-материал. Настройки интенсивности доступны в FlameVisualFeedback на Player.",
            "OK");
        Debug.Log("Player flame visual feedback setup completed.", feedback);
    }

    private static void ConfigurePlayerPrefab(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            throw new System.InvalidOperationException("Player.prefab could not be loaded.");
        }

        PlayerController playerController = playerRoot.GetComponent<PlayerController>();
        FlameMeter flameMeter = playerRoot.GetComponent<FlameMeter>();
        SpriteRenderer playerRenderer = playerRoot.GetComponent<SpriteRenderer>();

        if (playerController == null || flameMeter == null || playerRenderer == null)
        {
            throw new System.InvalidOperationException(
                "Player.prefab must keep its existing PlayerController, FlameMeter and SpriteRenderer components.");
        }

        Transform lightTransform = playerRoot.transform.Find("FlameLight");
        Light2D flameLight = lightTransform != null ? lightTransform.GetComponent<Light2D>() : null;
        if (flameLight == null)
        {
            SerializedObject meterObject = new SerializedObject(flameMeter);
            SerializedProperty lightProperty = meterObject.FindProperty("flameLight");
            flameLight = lightProperty != null ? lightProperty.objectReferenceValue as Light2D : null;
        }

        if (flameLight == null)
        {
            throw new System.InvalidOperationException(
                "The existing FlameLight object or FlameMeter light reference was not found.");
        }

        Transform coreTransform = playerRoot.transform.Find("FlameCore");
        GameObject coreObject;
        if (coreTransform == null)
        {
            coreObject = new GameObject("FlameCore");
            coreTransform = coreObject.transform;
            coreTransform.SetParent(playerRoot.transform, false);
        }
        else
        {
            coreObject = coreTransform.gameObject;
        }

        coreTransform.localPosition = new Vector3(0f, 0.035f, 0f);
        coreTransform.localRotation = Quaternion.identity;
        coreTransform.localScale = new Vector3(0.62f, 0.88f, 1f);

        SpriteRenderer flameRenderer = coreObject.GetComponent<SpriteRenderer>();
        if (flameRenderer == null)
        {
            flameRenderer = coreObject.AddComponent<SpriteRenderer>();
        }

        flameRenderer.sprite = playerRenderer.sprite;
        flameRenderer.sharedMaterial = playerRenderer.sharedMaterial;
        flameRenderer.color = new Color(1f, 0.88f, 0.58f, 0.96f);
        flameRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        flameRenderer.sortingOrder = playerRenderer.sortingOrder + 1;

        Transform sparksTransform = playerRoot.transform.Find("FlameSparks");
        GameObject sparksObject;
        if (sparksTransform == null)
        {
            sparksObject = new GameObject("FlameSparks");
            sparksTransform = sparksObject.transform;
            sparksTransform.SetParent(playerRoot.transform, false);
        }
        else
        {
            sparksObject = sparksTransform.gameObject;
        }

        sparksTransform.localPosition = Vector3.zero;
        sparksTransform.localRotation = Quaternion.identity;
        sparksTransform.localScale = Vector3.one;

        ParticleSystem sparks = sparksObject.GetComponent<ParticleSystem>();
        if (sparks == null)
        {
            sparks = sparksObject.AddComponent<ParticleSystem>();
        }

        // ParticleSystem is a Component, not a Behaviour: activate its GameObject
        // and enable the Emission module instead of setting a nonexistent enabled property.
        sparksObject.SetActive(true);

        ParticleSystem.MainModule main = sparks.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.78f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.032f, 0.072f);
        main.startColor = Color.white;
        main.gravityModifier = 0.10f;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
        emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparks.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = sparks.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -0.8f),
                new Keyframe(0.72f, 0.48f, -0.8f, -2.2f),
                new Keyframe(1f, 0.03f, -1.2f, 0f)));

        ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.StretchedBillboard;
        particleRenderer.lengthScale = 2.25f;
        particleRenderer.speedScale = 0.24f;
        particleRenderer.cameraVelocityScale = 0f;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        particleRenderer.sortingOrder = playerRenderer.sortingOrder + 2;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sharedMaterial = CreateParticleMaterial();

        FlameVisualFeedback feedback = playerRoot.GetComponent<FlameVisualFeedback>();
        if (feedback == null)
        {
            feedback = playerRoot.AddComponent<FlameVisualFeedback>();
        }

        SetObjectReference(feedback, "flameMeter", flameMeter);
        SetObjectReference(feedback, "playerController", playerController);
        SetObjectReference(feedback, "flameRenderer", flameRenderer);
        SetObjectReference(feedback, "flameLight", flameLight);
        SetObjectReference(feedback, "sparkParticles", sparks);
        SetObjectReference(feedback, "windController", null);
        SetObjectArray(feedback, "fuelPickups", new Object[0]);

        EditorUtility.SetDirty(playerRoot);
    }

    private static Material CreateParticleMaterial()
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
        {
            throw new System.InvalidOperationException(
                "URP particle shader 'Universal Render Pipeline/Particles/Unlit' was not found. " +
                "Check the URP package before configuring flame particles.");
        }

        Texture2D sparkTexture = CreateSparkTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        if (material == null)
        {
            material = new Material(particleShader)
            {
                name = "FlameSparkUnlit"
            };
            AssetDatabase.CreateAsset(material, SparkMaterialPath);
        }
        else
        {
            material.shader = particleShader;
        }

        material.SetTexture("_BaseMap", sparkTexture);
        material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_EmissionMap"))
            material.SetTexture("_EmissionMap", sparkTexture);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", new Color(1.8f, 0.58f, 0.08f, 1f));

        // Transparent additive blending gives the soft texture a warm halo on
        // the dark game background. The particle vertex color supplies its palette.
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_BlendOp", (float)BlendOp.Add);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Texture2D CreateSparkTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkTexturePath);
        if (texture != null)
        {
            return texture;
        }

        const int width = 48;
        const int height = 128;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "FlameSparkTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        Color[] pixels = new Color[width * height];
        Color edgeColor = new Color(1f, 0.12f, 0.012f, 1f);
        Color bodyColor = new Color(1f, 0.43f, 0.035f, 1f);
        Color coreColor = new Color(1f, 0.96f, 0.58f, 1f);

        for (int y = 0; y < height; y++)
        {
            float along = (y + 0.5f) / height;
            float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * along), 0.62f);
            float halfWidth = Mathf.Lerp(0.045f, 0.46f, taper);

            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs(((x + 0.5f) / width) * 2f - 1f);
                float distance = across / Mathf.Max(0.001f, halfWidth);

                float bodyAlpha = 1f - Mathf.SmoothStep(0.56f, 1.05f, distance);
                float softGlow = 1f - Mathf.SmoothStep(0.72f, 1.72f, distance);
                float alpha = Mathf.Clamp01(
                    Mathf.Max(bodyAlpha, softGlow * 0.26f) * Mathf.Pow(taper, 0.58f));

                float bodyBlend = Mathf.Clamp01(1f - distance * 0.78f);
                Color color = Color.Lerp(edgeColor, bodyColor, bodyBlend);
                float coreBlend = 1f - Mathf.SmoothStep(0.08f, 0.58f, distance);
                color = Color.Lerp(color, coreColor, coreBlend * 0.92f);
                pixels[y * width + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, SparkTexturePath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    private static void ValidateGameplayInputWiring(
        PlayerController player, WindController wind, FlameVisualFeedback feedback)
    {
        if (player == null || wind == null || feedback == null)
        {
            throw new System.InvalidOperationException(
                "Player, WindController or FlameVisualFeedback reference is missing in Main.unity.");
        }

        Scene scene = player.gameObject.scene;
        MainMenuController menu = FindInScene<MainMenuController>(scene);
        GameManager manager = FindInScene<GameManager>(scene);
        if (menu == null || manager == null)
        {
            throw new System.InvalidOperationException(
                "Main.unity has no working in-scene menu. Game Flow UI setup did not create UI_Menu.");
        }

        SerializedObject managerObject = new SerializedObject(manager);
        SerializedProperty menuReference = managerObject.FindProperty("mainMenuController");
        if (menuReference == null || menuReference.objectReferenceValue != menu)
        {
            throw new System.InvalidOperationException(
                "GameManager.mainMenuController is not connected. Player input will remain inactive until StartGame is called.");
        }

        SerializedProperty playerReference = managerObject.FindProperty("playerController");
        if (playerReference == null || playerReference.objectReferenceValue != player)
        {
            throw new System.InvalidOperationException(
                "GameManager.playerController does not reference the scene Player. StartGame cannot activate the intended player.");
        }

        SerializedObject menuObject = new SerializedObject(menu);
        SerializedProperty menuManager = menuObject.FindProperty("gameManager");
        if (menuManager == null || menuManager.objectReferenceValue != manager)
        {
            throw new System.InvalidOperationException(
                "UI_Menu.MainMenuController.gameManager is not connected to the scene GameManager.");
        }

        Button playButton = FindButtonInHierarchy(menu.transform, "PlayButton");
        if (playButton == null)
        {
            throw new System.InvalidOperationException(
                "UI_Menu has no PlayButton. The StartGame entry point cannot be reached.");
        }

        bool playIsWired = false;
        for (int i = 0; i < playButton.onClick.GetPersistentEventCount(); i++)
        {
            if (playButton.onClick.GetPersistentTarget(i) == menu &&
                playButton.onClick.GetPersistentMethodName(i) == nameof(MainMenuController.PlayGame))
            {
                playIsWired = true;
                break;
            }
        }

        if (!playIsWired)
        {
            for (int i = playButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(playButton.onClick, i);
            }

            UnityEventTools.AddPersistentListener(playButton.onClick, menu.PlayGame);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playButton);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        SerializedObject playerObject = new SerializedObject(player);
        SerializedProperty actionsProperty = playerObject.FindProperty("inputActions");
        InputActionAsset actions = actionsProperty != null
            ? actionsProperty.objectReferenceValue as InputActionAsset
            : null;
        InputActionMap playerMap = actions != null
            ? actions.FindActionMap("Player", false)
            : null;
        InputAction moveAction = playerMap != null
            ? playerMap.FindAction("Move", false)
            : null;

        if (moveAction == null)
        {
            throw new System.InvalidOperationException(
                "PlayerController has no Player/Move action. Assign the InputSystem_Actions asset.");
        }

        string[] requiredBindings =
        {
            "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
            "<Keyboard>/upArrow", "<Keyboard>/leftArrow",
            "<Keyboard>/downArrow", "<Keyboard>/rightArrow"
        };

        List<string> missingBindings = new List<string>();
        for (int i = 0; i < requiredBindings.Length; i++)
        {
            bool found = moveAction.bindings.Any(binding =>
                string.Equals(binding.path, requiredBindings[i], System.StringComparison.OrdinalIgnoreCase));
            if (!found)
                missingBindings.Add(requiredBindings[i]);
        }

        if (missingBindings.Count > 0)
        {
            throw new System.InvalidOperationException(
                "Player/Move is missing keyboard bindings: " + string.Join(", ", missingBindings.ToArray()));
        }
    }

    private static void ValidateParticleSetup(FlameVisualFeedback feedback)
    {
        SerializedObject feedbackObject = new SerializedObject(feedback);
        SerializedProperty particlesProperty = feedbackObject.FindProperty("sparkParticles");
        ParticleSystem particles = particlesProperty != null
            ? particlesProperty.objectReferenceValue as ParticleSystem
            : null;

        if (particles == null || !particles.gameObject.activeInHierarchy)
        {
            throw new System.InvalidOperationException(
                "FlameSparks ParticleSystem is missing or its GameObject is inactive. The spark object must remain active.");
        }

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        Shader expectedShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null ||
            renderer.sharedMaterial.shader != expectedShader ||
            renderer.sharedMaterial.GetTexture("_BaseMap") == null ||
            renderer.renderMode != ParticleSystemRenderMode.StretchedBillboard)
        {
            throw new System.InvalidOperationException(
                "FlameSparks is not using the generated fire texture and URP additive material.");
        }

        if (!particles.emission.enabled)
        {
            throw new System.InvalidOperationException(
                "FlameSparks emission module is disabled. Keep the ParticleSystem enabled.");
        }
    }

    private static Button FindButtonInHierarchy(Transform root, string buttonName)
    {
        if (root == null)
            return null;

        if (root.name == buttonName)
        {
            Button button = root.GetComponent<Button>();
            if (button != null)
                return button;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Button found = FindButtonInHierarchy(root.GetChild(i), buttonName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            T component = sceneRoot.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException(
                target.GetType().Name + " is missing serialized field '" + propertyName + "'.");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
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
        EditorUtility.SetDirty(target);
    }
}
#endif
