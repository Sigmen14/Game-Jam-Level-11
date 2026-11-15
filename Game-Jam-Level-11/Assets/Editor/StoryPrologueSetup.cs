#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the prologue presentation inside the existing UI_Menu prefab.
/// It reuses the prefab's Canvas and GraphicRaycaster and never adds an EventSystem.
/// Running the command again rebuilds only the StoryPrologue child.
/// </summary>
public static class StoryPrologueSetup
{
    private const string MenuPrefabPath = "Assets/Prefabs/UI/UI_Menu.prefab";
    private const string SoftCirclePath = "Assets/Art/LastFlame/SoftCircle.asset";
    private const string WindAudioPath = "Assets/Audio/SFX/Prologue_Wind.wav";
    private const string FireAudioPath = "Assets/Audio/SFX/Prologue_Fire.wav";

    [MenuItem("Game Jam/Setup Story Prologue")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Сюжетное вступление", "Остановите Play Mode перед настройкой.", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "Сюжетное вступление",
                "Не найден UI_Menu.prefab. Сначала восстановите существующее главное меню.",
                "OK");
            return;
        }

        AudioClip windClip = EnsureAudioClip(WindAudioPath, true);
        AudioClip fireClip = EnsureAudioClip(FireAudioPath, false);
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoftCirclePath);
        if (glowSprite == null)
            glowSprite = uiSprite;

        GameObject root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException("В UI_Menu.prefab не найден существующий Canvas.");

            Transform oldPrologue = root.transform.Find("StoryPrologue");
            if (oldPrologue != null)
                UnityEngine.Object.DestroyImmediate(oldPrologue.gameObject);

            GameObject overlay = CreateRect("StoryPrologue", root.transform);
            overlay.transform.SetAsLastSibling();

            CanvasGroup overlayGroup = overlay.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            Image background = overlay.AddComponent<Image>();
            background.sprite = uiSprite;
            background.color = new Color(0.006f, 0.009f, 0.018f, 1f);
            background.raycastTarget = true;
            Stretch(overlay.GetComponent<RectTransform>());

            CanvasGroup worldGroup = CreateGroup("RuinedWorld", overlay.transform);
            CanvasGroup flameGroup = CreateGroup("LastSpark", overlay.transform);
            CanvasGroup beaconGroup = CreateGroup("AncientBeacon", overlay.transform);
            CanvasGroup titleGroup = CreateGroup("TitleText", overlay.transform);
            CanvasGroup storyGroup = CreateGroup("StoryText", overlay.transform);
            CanvasGroup finalGroup = CreateGroup("FinalLine", overlay.transform);

            CreateRuinedWorld(worldGroup.transform, uiSprite);
            Image flameGlow;
            Image flameCore;
            Image[] emberParticles = CreateLastSpark(flameGroup.transform, uiSprite, glowSprite, out flameGlow, out flameCore);
            Image beaconGlow = CreateBeacon(beaconGroup.transform, uiSprite, glowSprite);
            Image[] ashParticles = CreateAsh(worldGroup.transform, uiSprite);

            Text titleText = CreateText(
                "Title", titleGroup.transform, "Когда погас последний свет",
                new Vector2(0.5f, 0.79f), new Vector2(1280f, 106f), 62,
                new Color(1f, 0.82f, 0.58f, 1f));
            Text storyText = CreateText(
                "Story", storyGroup.transform, string.Empty,
                new Vector2(0.5f, 0.61f), new Vector2(1420f, 176f), 34,
                new Color(0.88f, 0.89f, 0.9f, 1f));
            Text finalText = CreateText(
                "Hope", finalGroup.transform, "Пока горит хотя бы одна искра — надежда не умерла",
                new Vector2(0.5f, 0.55f), new Vector2(1500f, 150f), 44,
                new Color(1f, 0.78f, 0.43f, 1f));
            Text continueHint = CreateText(
                "ContinueHint", finalGroup.transform, "Нажмите ЛКМ или ПРОБЕЛ, чтобы продолжить",
                new Vector2(0.5f, 0.36f), new Vector2(1300f, 56f), 24,
                new Color(0.88f, 0.86f, 0.81f, 0.92f));

            Button skipButton = CreateSkipButton(overlay.transform, uiSprite);
            AudioSource windSource = ConfigureAudioSource(overlay.AddComponent<AudioSource>(), windClip);
            AudioSource fireSource = ConfigureAudioSource(overlay.AddComponent<AudioSource>(), fireClip);

            PrologueController controller = overlay.AddComponent<PrologueController>();
            SerializedObject serialized = new SerializedObject(controller);
            SetReference(serialized, "overlayGroup", overlayGroup);
            SetReference(serialized, "worldGroup", worldGroup);
            SetReference(serialized, "flameGroup", flameGroup);
            SetReference(serialized, "beaconGroup", beaconGroup);
            SetReference(serialized, "titleGroup", titleGroup);
            SetReference(serialized, "storyGroup", storyGroup);
            SetReference(serialized, "finalGroup", finalGroup);
            SetReference(serialized, "titleText", titleText);
            SetReference(serialized, "storyText", storyText);
            SetReference(serialized, "finalText", finalText);
            SetReference(serialized, "continueHint", continueHint);
            SetReference(serialized, "flameGlow", flameGlow);
            SetReference(serialized, "flameCore", flameCore);
            SetReference(serialized, "beaconGlow", beaconGlow);
            SetReference(serialized, "skipButton", skipButton);
            SetReference(serialized, "windAudio", windSource);
            SetReference(serialized, "fireAudio", fireSource);
            SetArray(serialized, "ashParticles", ashParticles);
            SetArray(serialized, "emberParticles", emberParticles);
            serialized.FindProperty("totalDuration").floatValue = 21.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Сюжетное вступление",
            "Готово. Вступление добавлено поверх существующего UI_Menu. Canvas, EventSystem и игровые сцены не дублировались.",
            "OK");
    }

    [MenuItem("Game Jam/Reset Story Prologue Playback")]
    public static void ResetPlayback()
    {
        PlayerPrefs.DeleteKey(PrologueController.ViewedKey);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog(
            "Сюжетное вступление",
            "Флаг первого просмотра сброшен. Вступление снова появится при следующем запуске Main.",
            "OK");
    }

    private static void CreateRuinedWorld(Transform parent, Sprite sprite)
    {
        CreateImage("AshSky", parent, sprite, new Color(0.055f, 0.067f, 0.09f, 1f),
            new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(2100f, 800f));

        CreateImage("DistantSmoke", parent, sprite, new Color(0.3f, 0.31f, 0.32f, 0.16f),
            new Vector2(0.5f, 0.47f), new Vector2(-90f, 0f), new Vector2(2080f, 150f));
        CreateImage("HorizonAsh", parent, sprite, new Color(0.18f, 0.19f, 0.2f, 0.38f),
            new Vector2(0.5f, 0.34f), new Vector2(0f, -22f), new Vector2(2100f, 190f));
        CreateImage("AshGround", parent, sprite, new Color(0.035f, 0.038f, 0.045f, 1f),
            new Vector2(0.5f, 0.07f), new Vector2(0f, -12f), new Vector2(2200f, 300f));

        float[] houseX = { 0.075f, 0.235f, 0.64f, 0.91f };
        float[] houseScale = { 0.86f, 1.12f, 0.98f, 0.9f };
        for (int i = 0; i < houseX.Length; i++)
        {
            float x = houseX[i];
            float scale = houseScale[i];
            Color wall = i % 2 == 0
                ? new Color(0.09f, 0.095f, 0.105f, 1f)
                : new Color(0.12f, 0.12f, 0.13f, 1f);

            CreateImage("RuinedHouse_" + i, parent, sprite, wall,
                new Vector2(x, 0.255f), Vector2.zero, new Vector2(170f * scale, 148f * scale));

            CreateImage("RoofLeft_" + i, parent, sprite, new Color(0.075f, 0.08f, 0.09f, 1f),
                new Vector2(x, 0.335f), new Vector2(-37f * scale, 0f), new Vector2(112f * scale, 15f * scale), -24f);
            CreateImage("RoofRight_" + i, parent, sprite, new Color(0.075f, 0.08f, 0.09f, 1f),
                new Vector2(x, 0.335f), new Vector2(37f * scale, 0f), new Vector2(112f * scale, 15f * scale), 24f);

            Color window = new Color(0.015f, 0.018f, 0.022f, 0.9f);
            CreateImage("WindowA_" + i, parent, sprite, window,
                new Vector2(x, 0.265f), new Vector2(-33f * scale, 0f), new Vector2(21f, 35f));
            CreateImage("WindowB_" + i, parent, sprite, window,
                new Vector2(x, 0.265f), new Vector2(34f * scale, 0f), new Vector2(21f, 35f));
        }

        CreateImage("BrokenStreet", parent, sprite, new Color(0.09f, 0.092f, 0.095f, 0.9f),
            new Vector2(0.48f, 0.17f), new Vector2(0f, -4f), new Vector2(660f, 34f), -7f);
        CreateImage("StreetRubbleLeft", parent, sprite, new Color(0.13f, 0.13f, 0.14f, 0.55f),
            new Vector2(0.18f, 0.13f), new Vector2(0f, 0f), new Vector2(130f, 13f), 11f);
        CreateImage("StreetRubbleRight", parent, sprite, new Color(0.13f, 0.13f, 0.14f, 0.48f),
            new Vector2(0.75f, 0.12f), new Vector2(0f, 0f), new Vector2(170f, 12f), -9f);

        float[] hearthX = { 0.28f, 0.54f, 0.72f };
        for (int i = 0; i < hearthX.Length; i++)
        {
            CreateImage("DeadHearth_" + i, parent, sprite, new Color(0.018f, 0.02f, 0.025f, 1f),
                new Vector2(hearthX[i], 0.19f), Vector2.zero, new Vector2(50f, 12f));
            CreateImage("ColdAsh_" + i, parent, sprite, new Color(0.23f, 0.22f, 0.2f, 0.28f),
                new Vector2(hearthX[i], 0.2f), new Vector2(0f, 7f), new Vector2(38f, 3f), 16f);
        }
    }

    private static Image[] CreateAsh(Transform parent, Sprite sprite)
    {
        List<Image> particles = new List<Image>();
        System.Random random = new System.Random(1407);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)random.NextDouble() * 1840f - 920f;
            float y = (float)random.NextDouble() * 650f - 280f;
            float width = i % 3 == 0 ? 14f : 6f;
            float height = i % 3 == 0 ? 2.5f : 4f;
            float alpha = 0.13f + (float)random.NextDouble() * 0.2f;
            Color color = new Color(0.63f, 0.65f, 0.67f, alpha);
            Image particle = CreateImage(
                "AshParticle_" + i, parent, sprite, color,
                new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height),
                (float)random.NextDouble() * 45f - 22.5f);
            particles.Add(particle);
        }

        return particles.ToArray();
    }

    private static Image[] CreateLastSpark(
        Transform parent,
        Sprite sprite,
        Sprite glowSprite,
        out Image flameGlow,
        out Image flameCore)
    {
        CreateImage("BurntWoodLeft", parent, sprite, new Color(0.055f, 0.035f, 0.025f, 1f),
            new Vector2(0.41f, 0.2f), new Vector2(-11f, -7f), new Vector2(70f, 8f), -13f);
        CreateImage("BurntWoodRight", parent, sprite, new Color(0.055f, 0.035f, 0.025f, 1f),
            new Vector2(0.41f, 0.2f), new Vector2(13f, -7f), new Vector2(62f, 8f), 10f);

        flameGlow = CreateImage("LastSparkGlow", parent, glowSprite, new Color(1f, 0.31f, 0.06f, 0.45f),
            new Vector2(0.41f, 0.285f), Vector2.zero, new Vector2(215f, 215f));
        Image outerGlow = CreateImage("LastSparkOuterGlow", parent, glowSprite, new Color(1f, 0.24f, 0.035f, 0.16f),
            new Vector2(0.41f, 0.285f), Vector2.zero, new Vector2(330f, 330f));
        flameCore = CreateImage("LastSparkCore", parent, glowSprite, new Color(1f, 0.75f, 0.27f, 1f),
            new Vector2(0.41f, 0.285f), new Vector2(0f, 7f), new Vector2(32f, 58f));
        CreateImage("LastSparkHeart", parent, sprite, new Color(1f, 0.97f, 0.72f, 1f),
            new Vector2(0.41f, 0.285f), new Vector2(0f, 11f), new Vector2(8f, 18f), 9f);
        outerGlow.raycastTarget = false;

        List<Image> embers = new List<Image>();
        System.Random random = new System.Random(909);
        for (int i = 0; i < 16; i++)
        {
            float x = (float)random.NextDouble() * 240f - 120f;
            float y = (float)random.NextDouble() * 250f - 40f;
            Color color = i % 3 == 0
                ? new Color(1f, 0.45f, 0.12f, 0.85f)
                : new Color(1f, 0.72f, 0.3f, 0.75f);
            Image particle = CreateImage(
                "EmberParticle_" + i, parent, sprite, color,
                new Vector2(0.41f, 0.285f), new Vector2(x, y), new Vector2(4f + i % 3, 9f + i % 5 * 2f),
                (float)random.NextDouble() * 38f - 19f);
            embers.Add(particle);
        }

        return embers.ToArray();
    }

    private static Image CreateBeacon(Transform parent, Sprite sprite, Sprite glowSprite)
    {
        CreateImage("BeaconGlow", parent, glowSprite, new Color(1f, 0.56f, 0.18f, 0.25f),
            new Vector2(0.83f, 0.49f), Vector2.zero, new Vector2(245f, 245f));
        CreateImage("BeaconTower", parent, sprite, new Color(0.13f, 0.135f, 0.145f, 1f),
            new Vector2(0.83f, 0.305f), new Vector2(0f, 0f), new Vector2(52f, 225f));
        CreateImage("BeaconFoot", parent, sprite, new Color(0.09f, 0.095f, 0.105f, 1f),
            new Vector2(0.83f, 0.205f), new Vector2(0f, 0f), new Vector2(84f, 24f));
        CreateImage("BeaconRoof", parent, sprite, new Color(0.08f, 0.085f, 0.095f, 1f),
            new Vector2(0.83f, 0.415f), new Vector2(0f, 0f), new Vector2(74f, 20f));
        CreateImage("BeaconWindow", parent, sprite, new Color(0.62f, 0.32f, 0.12f, 0.65f),
            new Vector2(0.83f, 0.36f), new Vector2(0f, 0f), new Vector2(12f, 34f));
        Image lamp = CreateImage("BeaconLamp", parent, glowSprite, new Color(1f, 0.78f, 0.38f, 0.95f),
            new Vector2(0.83f, 0.43f), new Vector2(0f, 0f), new Vector2(25f, 25f));
        CreateImage("BeaconTop", parent, sprite, new Color(0.16f, 0.16f, 0.17f, 1f),
            new Vector2(0.83f, 0.445f), new Vector2(0f, 0f), new Vector2(42f, 14f));
        return lamp;
    }

    private static Button CreateSkipButton(Transform parent, Sprite sprite)
    {
        GameObject buttonObject = CreateRect("SkipButton", parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.92f, 0.93f), Vector2.zero, new Vector2(180f, 54f));

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(0.08f, 0.075f, 0.075f, 0.84f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.8f, 0.55f, 1f);
        colors.pressedColor = new Color(0.85f, 0.55f, 0.27f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.16f;
        button.colors = colors;

        Text label = CreateText(
            "Label", buttonObject.transform, "ПРОПУСТИТЬ",
            new Vector2(0.5f, 0.5f), new Vector2(170f, 48f), 20,
            new Color(0.95f, 0.9f, 0.82f, 1f));
        label.raycastTarget = false;
        return button;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        Vector2 anchor,
        Vector2 size,
        int fontSize,
        Color color)
    {
        GameObject textObject = CreateRect(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, Vector2.zero, size);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(18, fontSize - 10);
        text.resizeTextMaxSize = fontSize;
        text.lineSpacing = 1.12f;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    private static CanvasGroup CreateGroup(string name, Transform parent)
    {
        GameObject groupObject = CreateRect(name, parent);
        Stretch(groupObject.GetComponent<RectTransform>());
        CanvasGroup group = groupObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float rotation = 0f)
    {
        GameObject imageObject = CreateRect(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, position, size, rotation);

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        result.transform.localScale = Vector3.one;
        return result;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float rotation = 0f)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static AudioSource ConfigureAudioSource(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.pitch = 1f;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 64;
        return source;
    }

    private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException("Поле PrologueController не найдено: " + name);

        property.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject serialized, string name, UnityEngine.Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || !property.isArray)
            throw new InvalidOperationException("Массив PrologueController не найден: " + name);

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static AudioClip EnsureAudioClip(string assetPath, bool wind)
    {
        if (!File.Exists(assetPath))
        {
            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(assetPath, GenerateWave(wind));
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
            throw new InvalidOperationException("Не удалось импортировать аудио: " + assetPath);

        return clip;
    }

    private static byte[] GenerateWave(bool wind)
    {
        const int sampleRate = 22050;
        int seconds = wind ? 8 : 6;
        int count = sampleRate * seconds;
        float[] samples = new float[count];
        System.Random random = new System.Random(wind ? 217 : 731);
        float low = 0f;
        float mid = 0f;
        float crackle = 0f;

        for (int i = 0; i < count; i++)
        {
            float time = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * (wind ? 0.0035f : 0.035f);
            mid += (noise - mid) * 0.075f;

            float sample;
            if (wind)
            {
                float gust = 0.54f
                    + 0.27f * Mathf.Sin(time * 0.72f)
                    + 0.13f * Mathf.Sin(time * 0.23f + 1.1f);
                sample = (low * 1.7f + (mid - low) * 0.38f) * gust * 0.66f;
            }
            else
            {
                if (random.NextDouble() < 0.0018)
                    crackle = 0.35f + (float)random.NextDouble() * 0.55f;
                crackle *= 0.992f;
                float warmBed = low * 0.16f + (mid - low) * 0.11f;
                sample = warmBed + (noise - low) * crackle * 0.55f;
            }

            float edge = Mathf.Min(1f, i / 1000f, (count - 1 - i) / 1000f);
            samples[i] = Mathf.Clamp(sample * edge, -0.92f, 0.92f);
        }

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int dataSize = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)Mathf.RoundToInt(samples[i] * 32767f));

            writer.Flush();
            return stream.ToArray();
        }
    }
}
#endif
