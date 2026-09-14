using System;
using Terrace.Client.Core;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// シーンに 1 つ置くだけでゲームが動く起動役。
    /// マップ(StreamingAssets/maps)とマスタ(StreamingAssets/master.bytes)を読み、シミュレーションと見た目を組み立て、
    /// 毎フレーム入力を渡してシミュレーションを進め、見た目を同期する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string mapFileName = "field01.json";
        [SerializeField] private bool showSpawnMarkers = true;
        [SerializeField] private float timeScale = 1f;

        private PlayerView? _playerView;
        private WorldViewSync? _worldView;
        private CameraRig? _cameraRig;
        private HudView? _hud;

        public string MapFileName
        {
            get => mapFileName;
            set => mapFileName = value;
        }

        public float TimeScale
        {
            get => timeScale;
            set => timeScale = value;
        }

        /// <summary>入力の供給元。テストでは ScriptedInputSource に差し替える。</summary>
        public IInputSource InputSource { get; set; } = new KeyboardInputSource();

        public GameSimulation? Simulation { get; private set; }
        public MasterDataRepository? MasterData { get; private set; }
        public Camera? Camera { get; private set; }
        public string? LastError { get; private set; }
        public bool IsReady => Simulation != null;

        private void Start()
        {
            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                Debug.LogException(ex, this);
            }
        }

        public void Initialize()
        {
            if (IsReady) return;

            var map = MapLoader.LoadFromStreamingAssets(mapFileName);
            foreach (var issue in map.Validate())
            {
                Debug.LogWarning($"[map] {issue}", this);
            }

            try
            {
                MasterData = MasterDataRepository.LoadFromStreamingAssets();
                Debug.Log($"[masterdata] loaded: items={MasterData.ItemCount} enemies={MasterData.EnemyCount} quests={MasterData.QuestCount} sha={MasterData.ShortVersion}", this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[masterdata] master.bytes を読めませんでした。仮の敵定義で続行します: {ex.Message}", this);
            }

            var masterData = MasterData;
            Func<int, EnemyDefinition?> enemyLookup = id => masterData?.GetEnemy(id) ?? FallbackEnemies.Get(id);
            Func<int, string> itemName = id => masterData?.ItemName(id) ?? $"item{id}";

            Simulation = new GameSimulation(map, enemyLookup, itemName);

            var mapRoot = new GameObject("Map");
            mapRoot.AddComponent<MapView>().Build(map, showSpawnMarkers);

            var playerGo = new GameObject("Player");
            _playerView = playerGo.AddComponent<PlayerView>();
            _playerView.Bind(Simulation);

            var worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(worldRoot.transform, Simulation.World);

            Camera = UnityEngine.Camera.main;
            if (Camera == null)
            {
                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                Camera = cameraGo.AddComponent<Camera>();
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
                cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            }
            _cameraRig = Camera.gameObject.GetComponent<CameraRig>();
            if (_cameraRig == null) _cameraRig = Camera.gameObject.AddComponent<CameraRig>();
            _cameraRig.Bind(Simulation);

            _hud = gameObject.AddComponent<HudView>();
            var version = masterData == null ? "master: fallback" : $"master: {masterData.ShortVersion}";
            _hud.Bind(Simulation, Camera, $"{map.Name}   {version}");

            Debug.Log($"[game] ready: map={map.Name} footholds={map.Footholds.Count} enemies={Simulation.World.Enemies.Count} spawn=({Simulation.SpawnPosition.X}, {Simulation.SpawnPosition.Y})", this);
        }

        private void Update()
        {
            if (Simulation == null) return;
            var input = InputSource.Read();
            var dt = Mathf.Min(Time.deltaTime, 0.05f) * timeScale;
            Step(input, dt);
        }

        /// <summary>1 フレーム分進めて見た目を同期する(テストからも呼べる)。</summary>
        public void Step(InputFrame input, float dt)
        {
            if (Simulation == null) return;
            Simulation.Step(input, dt);
            _playerView?.Sync();
            _worldView?.Sync(Simulation.World);
        }
    }
}
