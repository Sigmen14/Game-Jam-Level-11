#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Idempotently adds fire feedback to the existing menu button prefabs.
/// No scene or prefab YAML is edited by hand; all authoring goes through Unity's prefab API.
/// </summary>
public static class MenuButtonFireAnimationSetup
{
    private const string StartPanelPrefabPath = "Assets/Prefabs/UI/UI_StartPanel.prefab";
    private const string SettingsPanelPrefabPath = "Assets/Prefabs/UI/UI_SettingsPanel.prefab";
    private const string GlowSpritePath = "Assets/Art/LastFlame/MenuButtonGlow.png";
    private const string SparkSpritePath = "Assets/Art/LastFlame/MenuButtonSpark.png";

    private const int SparkCount = 6;

    [MenuItem("Game Jam/Setup Menu Button Animations")]
    public static void SetupMenuButtonAnimations()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Last Ember",
                "Остановите Play Mode перед настройкой анимации кнопок.",
                "OK");
            return;
        }

        try
        {
            Sprite glowSprite = LoadOrCreateSprite(GlowSpritePath, false);
            Sprite sparkSprite = LoadOrCreateSprite(SparkSpritePath, true);
            if (glowSprite == null || sparkSprite == null)
                throw new System.InvalidOperationException("Не удалось создать текстуры свечения и искр.");

            int configuredButtons = 0;
            configuredButtons += SetupPrefab(StartPanelPrefabPath, glowSprite, sparkSprite);
            configuredButtons += SetupPrefab(SettingsPanelPrefabPath, glowSprite, sparkSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Last Ember",
                "Анимация огня настроена для " + configuredButtons +
                " существующих кнопок. OnClick и EventSystem не изменялись.",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Last Ember — ошибка настройки", exception.Message, "OK");
        }
    }

    private static int SetupPrefab(string prefabPath, Sprite glowSprite, Sprite sparkSprite)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabContents == null)
            throw new System.InvalidOperationException("Не удалось открыть prefab: " + prefabPath);

        try
        {
            Button[] buttons = prefabContents.GetComponentsInChildren<Button>(true);
            int configured = 0;

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                ConfigureButton(button, glowSprite, sparkSprite);
                configured++;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            Debug.Log("MenuButtonFireAnimationSetup: configured " + configured +
                      " buttons in " + prefabPath + ".");
            return configured;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    private static void ConfigureButton(Button button, Sprite glowSprite, Sprite sparkSprite)
    {
        Transform buttonTransform = button.transform;
        MenuButtonFireAnimation animation = button.GetComponent<MenuButtonFireAnimation>();
        if (animation == null)
            animation = button.gameObject.AddComponent<MenuButtonFireAnimation>();

        Image glow = GetOrCreateImage(
            buttonTransform,
            "FireGlow",
            glowSprite,
            new Color(1f, 0.28f, 0.045f, 0f),
            false,
            0);

        Image flash = GetOrCreateImage(
            buttonTransform,
            "FireFlash",
            glowSprite,
            new Color(1f, 0.72f, 0.28f, 0f),
            false,
            1);

        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-22f, -18f);
        glowRect.offsetMax = new Vector2(22f, 18f);

        RectTransform flashRect = flash.rectTransform;
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = new Vector2(-12f, -10f);
        flashRect.offsetMax = new Vector2(12f, 10f);

        Image[] sparks = new Image[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            sparks[i] = GetOrCreateImage(
                buttonTransform,
                "FireSpark_" + i.ToString("00"),
                sparkSprite,
                i % 2 == 0
                    ? new Color(1f, 0.72f, 0.28f, 0f)
                    : new Color(1f, 0.35f, 0.08f, 0f),
                true,
                2 + i);

            RectTransform sparkRect = sparks[i].rectTransform;
            sparkRect.anchorMin = new Vector2(0.5f, 0.5f);
            sparkRect.anchorMax = new Vector2(0.5f, 0.5f);
            sparkRect.pivot = new Vector2(0.5f, 0.5f);
            sparkRect.sizeDelta = new Vector2(10f, 10f);
            sparkRect.anchoredPosition = Vector2.zero;
        }

        SerializedObject serialized = new SerializedObject(animation);
        SetObjectReference(serialized, "glowImage", glow);
        SetObjectReference(serialized, "flashImage", flash);

        SerializedProperty sparkProperty = serialized.FindProperty("sparkImages");
        sparkProperty.arraySize = sparks.Length;
        for (int i = 0; i < sparks.Length; i++)
            sparkProperty.GetArrayElementAtIndex(i).objectReferenceValue = sparks[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animation);
        EditorUtility.SetDirty(button.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    private static Image GetOrCreateImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color color,
        bool preserveAspect,
        int siblingIndex)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject;

        if (existing == null)
        {
            imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
        }
        else
        {
            imageObject = existing.gameObject;
            if (imageObject.GetComponent<RectTransform>() == null)
                throw new System.InvalidOperationException(objectName + " must use a RectTransform.");
        }

        Image image = imageObject.GetComponent<Image>();
        if (image == null)
            image = imageObject.AddComponent<Image>();

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        image.maskable = true;
        image.color = color;

        imageObject.transform.SetSiblingIndex(
            Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

        return image;
    }

    private static void SetObjectReference(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new System.InvalidOperationException("Не найдено поле компонента: " + propertyName);

        property.objectReferenceValue = value;
    }

    private static Sprite LoadOrCreateSprite(string assetPath, bool isSpark)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = Path.GetFileNameWithoutExtension(assetPath);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (size - 1f)) * 2f - 1f;
                float ny = (y / (size - 1f)) * 2f - 1f;
                float alpha = isSpark
                    ? CalculateSparkAlpha(nx, ny)
                    : CalculateGlowAlpha(nx, ny);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static float CalculateGlowAlpha(float x, float y)
    {
        // The target button occupies roughly 89% of the glow width and 66% of its height.
        // A soft rectangular rim keeps the center of the button clear.
        float normalizedEdge = Mathf.Max(
            Mathf.Abs(x) / 0.89f,
            Mathf.Abs(y) / 0.66f);
        float distanceToRim = Mathf.Abs(normalizedEdge - 1f);
        return Mathf.Clamp01(1f - distanceToRim / 0.28f) * 0.72f;
    }

    private static float CalculateSparkAlpha(float x, float y)
    {
        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);
        float diamond = Mathf.Clamp01(1f - (absX + absY) * 1.15f);
        float verticalRay = Mathf.Clamp01(1f - absX * 7f) *
                            Mathf.Clamp01(1f - absY * 1.25f);
        float horizontalRay = Mathf.Clamp01(1f - absY * 7f) *
                              Mathf.Clamp01(1f - absX * 1.25f);
        return Mathf.Max(diamond, Mathf.Max(verticalRay, horizontalRay) * 0.78f);
    }
}
#endif
