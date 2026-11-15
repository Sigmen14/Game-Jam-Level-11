using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MenuCameraSetup
{
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";

    [MenuItem("Game Jam/Fix Menu Camera")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        Camera camera = FindCamera(scene);

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Menu Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.enabled = true;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.014f, 0.028f, 1f);
        camera.cullingMask = ~0;

        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        cameraData.SetRenderer(0);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Menu Camera",
            "Камера Menu создана и настроена. Надпись No cameras rendering должна исчезнуть.",
            "OK");
    }

    private static Camera FindCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null)
                return camera;
        }

        return null;
    }
}
