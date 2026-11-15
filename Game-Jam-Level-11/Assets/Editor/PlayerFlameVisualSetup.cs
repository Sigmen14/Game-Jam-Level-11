#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        EditorUtility.DisplayDialog(
            "Player Flame Visual Feedback",
            "The Player prefab and Main.unity are configured. Adjust flame feedback intensity on the Player's FlameVisualFeedback component.",
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

        ParticleSystem.MainModule main = sparks.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = new Color(1f, 0.74f, 0.32f, 1f);

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.enabled = false;

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
            1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
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

        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        EditorUtility.SetDirty(playerRoot);
    }

    private static Material CreateParticleMaterial()
    {
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
        {
            throw new System.InvalidOperationException(
                "URP particle shader 'Universal Render Pipeline/Particles/Unlit' was not found. " +
                "Check that the Universal Render Pipeline package is installed and compiled.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        if (material == null)
        {
            material = new Material(particleShader)
            {
                name = "FlameSparkUnlit"
            };
            AssetDatabase.CreateAsset(material, SparkMaterialPath);
        }
        else if (material.shader != particleShader)
        {
            material.shader = particleShader;
        }

        // Explicit transparent alpha blending avoids the pink error material and
        // keeps per-particle warm colors/fade alpha visible in URP.
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
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
