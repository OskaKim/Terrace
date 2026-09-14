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

        /// <summary>Kenney の素材。Resources に無ければ null(生成スプライトで動く)。</summary>
        public ArtLibrary? Art { get; private set; }

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

            var art = ArtLibrary.Load();
            if (art.IsAvailable)
            {
                Art = art;
                Debug.Log("[art] Kenney Platformer Art Deluxe (CC0) の素材を使います", this);
            }
            else
            {
                Debug.LogWarning("[art] Resources/Terrace/Art/Kenney に素材が無いため、生成スプライトで続行します", this);
            }

            var masterData = MasterData;
            var artLibrary = Art;
            Func<int, EnemyDefinition?> enemyLookup = id =>
            {
                var definition = masterData?.GetEnemy(id) ?? FallbackEnemies.Get(id);
                var enemyArt = definition != null ? artLibrary?.GetEnemy(definition.EnemyId) : null;
                if (definition != null && enemyArt != null)
                {
                    // 当たり判定の大きさを絵に合わせる
                    definition.Width = enemyArt.Width;
                    definition.Height = enemyArt.Height;
                }
                return definition;
            };
            Func<int, string> itemName = id => masterData?.ItemName(id) ?? $"item{id}";

            var playerConfig = PlayerConfig.Default;
            if (Art?.Player != null)
            {
                playerConfig.Height = Art.Player.Height;
                playerConfig.HalfWidth = Art.Player.Width * 0.4f;
            }

            Simulation = new GameSimulation(map, enemyLookup, itemName, playerConfig: playerConfig);

            var mapRoot = new GameObject("Map");
            mapRoot.AddComponent<MapView>().Build(map, showSpawnMarkers, Art);

            var playerGo = new GameObject("Player");
            _playerView = playerGo.AddComponent<PlayerView>();
            _playerView.Bind(Simulation, Art);

            var worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(worldRoot.transform, Simulation.World, Art);

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

            if (Art?.Background != null)
            {
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
                var backdropGo = new GameObject("Backdrop");
                backdropGo.AddComponent<ParallaxBackdrop>().Bind(Camera, Art.Background, Camera.orthographicSize);
            }

            _hud = gameObject.AddComponent<HudView>();
            var version = masterData == null ? "master: fallback" : $"master: {masterData.ShortVersion}";
            _hud.Bind(Simulation, Camera, $"{map.Name}   {version}", Art);

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
