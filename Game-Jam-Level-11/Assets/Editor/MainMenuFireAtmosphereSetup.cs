using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the fire ambience to the existing UI_Menu prefab using Unity Editor APIs.
/// No scene YAML is edited and no EventSystem or menu is created.
/// </summary>
public static class MainMenuFireAtmosphereSetup
{
    private const string MenuPrefabPath = "Assets/Prefabs/UI/UI_Menu.prefab";
    private const string SoftCirclePath = "Assets/Art/LastFlame/SoftCircle.asset";

    [MenuItem("Game Jam/Setup Main Menu Fire Atmosphere")]
    public static void Setup()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath))
        {
            EditorUtility.DisplayDialog(
                "Main Menu Fire Atmosphere",
                "Не найден UI_Menu.prefab. Проверь, что проект и ветка загружены полностью.",
                "OK");
            return;
        }

        Sprite softCircle = AssetDatabase.LoadAssetAtPath<Sprite>(SoftCirclePath);
        if (softCircle == null)
        {
            EditorUtility.DisplayDialog(
                "Main Menu Fire Atmosphere",
                "Не найден спрайт SoftCircle.asset. Эффект не был применён.",
                "OK");
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            MainMenuController controller = prefabContents.GetComponent<MainMenuController>();
            if (controller == null)
                controller = prefabContents.GetComponentInChildren<MainMenuController>(true);

            Canvas canvas = prefabContents.GetComponent<Canvas>();
            if (canvas == null)
                canvas = prefabContents.GetComponentInChildren<Canvas>(true);

            if (controller == null || canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Main Menu Fire Atmosphere",
                    "В UI_Menu.prefab не найдены существующие Canvas или MainMenuController.",
                    "OK");
                return;
            }

            Transform previousAtmosphere = prefabContents.transform.Find("FireAtmosphere");
            if (previousAtmosphere != null)
                Object.DestroyImmediate(previousAtmosphere.gameObject);

            Transform previousFlash = prefabContents.transform.Find("FireTransitionFlash");
            if (previousFlash != null)
                Object.DestroyImmediate(previousFlash.gameObject);

            GameObject atmosphereObject = new GameObject("FireAtmosphere", typeof(RectTransform));
            RectTransform atmosphereRect = atmosphereObject.GetComponent<RectTransform>();
            atmosphereRect.SetParent(prefabContents.transform, false);
            Stretch(atmosphereRect);
            atmosphereRect.SetAsFirstSibling();

            CreateGlow(
                "DistantHearthGlow",
                atmosphereRect,
                softCircle,
                new Color(1f, 0.25f, 0.055f, 0.2f),
                new Vector2(1120f, 570f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 105f));

            CreateGlow(
                "HearthCoreGlow",
                atmosphereRect,
                softCircle,
                new Color(1f, 0.48f, 0.12f, 0.15f),
                new Vector2(560f, 260f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 105f));

            List<Image> floatingLights = new List<Image>();
            for (int i = 0; i < 24; i++)
            {
                float size = 10f + (i % 5) * 4f;
                float alpha = 0.34f + (i % 4) * 0.11f;
                Color color = new Color(1f, 0.55f + (i % 3) * 0.12f, 0.2f, alpha);
                floatingLights.Add(CreateParticle(
                    "FloatingLight_" + i.ToString("00"),
                    atmosphereRect,
                    softCircle,
                    color,
                    new Vector2(size, size)));
            }

            List<Image> risingSparks = new List<Image>();
            for (int i = 0; i < 14; i++)
            {
                float width = 3f + (i % 3);
                float height = 12f + (i % 4) * 5f;
                float alpha = 0.55f + (i % 3) * 0.12f;
                Color color = new Color(1f, 0.38f + (i % 4) * 0.12f, 0.08f, alpha);
                Image spark = CreateParticle(
                    "RisingSpark_" + i.ToString("00"),
                    atmosphereRect,
                    softCircle,
                    color,
                    new Vector2(width, height));
                risingSparks.Add(spark);
            }

            List<Image> brightEmbers = new List<Image>();
            for (int i = 0; i < 5; i++)
            {
                float size = 11f + (i % 3) * 4f;
                Color color = i % 2 == 0
                    ? new Color(1f, 0.78f, 0.34f, 0.98f)
                    : new Color(1f, 0.43f, 0.12f, 0.92f);
                brightEmbers.Add(CreateParticle(
                    "BrightEmber_" + i.ToString("00"),
                    atmosphereRect,
                    softCircle,
                    color,
                    new Vector2(size, size)));
            }

            GameObject flashObject = new GameObject(
                "FireTransitionFlash",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.SetParent(prefabContents.transform, false);
            Stretch(flashRect);
            flashRect.SetAsLastSibling();

            Image transitionFlash = flashObject.GetComponent<Image>();
            transitionFlash.sprite = null;
            transitionFlash.color = new Color(1f, 0.31f, 0.07f, 0f);
            transitionFlash.raycastTarget = false;
            transitionFlash.maskable = false;

            MainMenuFireAtmosphere atmosphere =
                prefabContents.GetComponent<MainMenuFireAtmosphere>();
            if (atmosphere == null)
                atmosphere = prefabContents.AddComponent<MainMenuFireAtmosphere>();

            SerializedObject atmosphereObjectSerialized = new SerializedObject(atmosphere);
            SetObject(atmosphereObjectSerialized, "atmosphereRoot", atmosphereRect);
            SetObject(atmosphereObjectSerialized, "hearthGlow",
                FindImage(atmosphereRect, "DistantHearthGlow"));
            SetObject(atmosphereObjectSerialized, "hearthCore",
                FindImage(atmosphereRect, "HearthCoreGlow"));
            SetObject(atmosphereObjectSerialized, "transitionFlash", transitionFlash);
            SetArray(atmosphereObjectSerialized, "floatingLights", floatingLights);
            SetArray(atmosphereObjectSerialized, "risingSparks", risingSparks);
            SetArray(atmosphereObjectSerialized, "brightEmbers", brightEmbers);
            atmosphereObjectSerialized.FindProperty("floatingLightCount").intValue = 22;
            atmosphereObjectSerialized.FindProperty("risingSparkCount").intValue = 12;
            atmosphereObjectSerialized.FindProperty("brightEmberCount").intValue = 4;
            atmosphereObjectSerialized.FindProperty("speedMultiplier").floatValue = 1f;
            atmosphereObjectSerialized.FindProperty("brightnessMultiplier").floatValue = 1f;
            atmosphereObjectSerialized.FindProperty("glowStrength").floatValue = 1f;
            atmosphereObjectSerialized.FindProperty("transitionDuration").floatValue = 0.72f;
            atmosphereObjectSerialized.FindProperty("flashPeakAlpha").floatValue = 0.32f;
            atmosphereObjectSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSerialized = new SerializedObject(controller);
            SerializedProperty atmosphereProperty = controllerSerialized.FindProperty("fireAtmosphere");
            if (atmosphereProperty != null)
            {
                atmosphereProperty.objectReferenceValue = atmosphere;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(atmosphere);
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, MenuPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Main Menu Fire Atmosphere",
                "Готово! Атмосфера добавлена в существующий UI_Menu.prefab. " +
                "Частицы находятся под панелями меню, а вспышка — поверх UI и не блокирует нажатия.",
                "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void CreateGlow(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 size,
        Vector2 anchor,
        Vector2 position)
    {
        Image image = CreateParticle(name, parent, sprite, color, size);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }

    private static Image CreateParticle(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 size)
    {
        GameObject particleObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = particleObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = particleObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        image.maskable = false;
        return image;
    }

    private static Image FindImage(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject serializedObject, string propertyName, List<Image> values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.ClearArray();
        for (int i = 0; i < values.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
