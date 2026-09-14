using System.IO;
using Terrace.Client.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Terrace.Client.Editor
{
    /// <summary>
    /// Main シーンをコードで組み立てる(手作業でのシーン編集を不要にするため)。
    /// メニュー Terrace/Create Main Scene、またはバッチ: -executeMethod Terrace.Client.Editor.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Terrace/Create Main Scene")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CameraRig>();

            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[setup] {ScenePath} を作成し、Build Settings に登録しました");
        }

        [MenuItem("Terrace/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath)) CreateMainScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>バッチ実行用のエントリ。</summary>
        public static void Run()
        {
            CreateMainScene();
        }
    }
}
