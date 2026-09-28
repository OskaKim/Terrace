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
    /// Windows 版の exe を作る: メニュー Terrace/Build Windows Player、またはバッチ: -executeMethod Terrace.Client.Editor.ProjectSetup.BuildWindows
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string WindowsBuildPath = "Build/Windows/Terrace.exe";

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
            var cameraRig = cameraGo.AddComponent<CameraRig>();

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            // シーンから起動したときはログイン窓で「オンライン / ひとり」を選ばせる
            bootstrap.Startup = StartupMode.Login;

            // スクリプトがアセットとして読み込まれていないと、シーンにスクリプトの複製が埋め込まれて
            // 別の環境では「スクリプトが見つからない」シーンになる。その状態では保存しない
            EnsureScriptAsset(cameraRig);
            EnsureScriptAsset(bootstrap);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[setup] {ScenePath} を作成し、Build Settings に登録しました");
        }

        private static void EnsureScriptAsset(MonoBehaviour component)
        {
            var script = MonoScript.FromMonoBehaviour(component);
            var path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                throw new System.InvalidOperationException(
                    $"[setup] {component.GetType().Name} のスクリプトがアセットとして見つかりません。シーンを保存しません(もう一度 -Setup を実行してください)");
            }
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

        /// <summary>
        /// Windows 版を Build/Windows/Terrace.exe に書き出す。複数起動して多人数で試すとき用。
        /// バックグラウンドでも動き続けるようにし、ウィンドウは 1280x720 の窓にする。
        /// </summary>
        [MenuItem("Terrace/Build Windows Player")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateMainScene();
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[build] {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (Application.isBatchMode && summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
