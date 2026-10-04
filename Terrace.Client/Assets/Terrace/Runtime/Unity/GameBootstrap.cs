using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Client.Online;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// シーンに 1 つ置くだけでゲームが動く起動役。下を組み立て、毎フレーム入力を渡してシミュレーションを進め、見た目を同期するだけ。
    ///
    ///   StartupFlow        ひとり / 接続 / ログイン窓の選び方(起動の流れ)。始めるときに Initialize を呼ぶ
    ///   GameContentLoader  マップ一式・マスタ・絵を読む
    ///   SimulationFactory  読んだ物と接続から GameSimulation を作る
    ///   MapViewSet         今いるマップの見た目(マップが変わったら作り直す)
    ///   PointerInteraction マウスのクリック → NPC に話しかける
    ///
    /// プレイヤー・他のプレイヤー・HUD・店の窓・音(AudioDirector)はマップが変わっても作り直さないので、ここで作る。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private StartupMode startupMode = StartupMode.Offline;
        [SerializeField] private int startMapId = 100;
        [SerializeField] private string mapFileName = string.Empty;
        [SerializeField] private bool showSpawnMarkers = true;
        [SerializeField] private float timeScale = 1f;

        [Tooltip("空ならログイン窓の前回値(PlayerPrefs)か、-terraceServer 引数")]
        [SerializeField] private string serverAddress = string.Empty;

        [Tooltip("空ならログイン窓の前回値(PlayerPrefs)か、-terraceName 引数")]
        [SerializeField] private string playerName = string.Empty;

        private StartupFlow? _startupFlow;
        private GameObject? _playerRoot;
        private PlayerView? _playerView;
        private RemotePlayersViewSync? _remoteViews;
        private GameObject? _remoteRoot;
        private HudView? _hud;
        private MapViewSet? _mapViews;
        private PointerInteraction? _pointer;

        public StartupMode Startup
        {
            get => startupMode;
            set => startupMode = value;
        }

        /// <summary>開始するマップ ID(maps/*.json の id)。</summary>
        public int StartMapId
        {
            get => startMapId;
            set => startMapId = value;
        }

        /// <summary>指定するとマップ一覧の代わりにこのファイルだけを読んで開始する(テスト用)。</summary>
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

        public string ServerAddress
        {
            get => serverAddress;
            set => serverAddress = value;
        }

        public string PlayerName
        {
            get => playerName;
            set => playerName = value;
        }

        /// <summary>入力の供給元。テストでは ScriptedInputSource に差し替える。</summary>
        public IInputSource InputSource { get; set; } = new KeyboardInputSource();

        public GameSimulation? Simulation { get; private set; }
        public MasterDataRepository? MasterData { get; private set; }
        public MapRegistry? Maps { get; private set; }

        /// <summary>Kenney の素材。Resources に無ければ null(生成スプライトで動く)。</summary>
        public ArtLibrary? Art { get; private set; }

        /// <summary>音の台帳。null なら Initialize で Resources/Terrace/Audio から読む。テストでは音の無い台帳に差し替える。</summary>
        public AudioLibrary? Audio { get; set; }

        /// <summary>効果音を鳴らす係(Initialize で作る)。</summary>
        public AudioDirector? AudioDirector { get; private set; }

        public ShopWindow? ShopWindow { get; private set; }
        public LoginWindow? LoginWindow => _startupFlow?.LoginWindow;
        public Camera? Camera { get; private set; }
        public string? LastError { get; private set; }

        /// <summary>最後に接続できなかった理由。</summary>
        public string? LastConnectError => _startupFlow?.LastConnectError;

        public MagicOnionConnection? Connection { get; private set; }
        public bool IsConnecting => _startupFlow?.IsConnecting ?? false;
        public bool IsReady => Simulation != null;
        public IReadOnlyList<NpcView> NpcViews => _mapViews?.NpcViews ?? Array.Empty<NpcView>();
        public int RemotePlayerViewCount => _remoteViews?.Count ?? 0;

        private void Start() => EnsureStartupFlow().Begin(startupMode);

        private void OnDestroy()
        {
            if (Simulation != null) Simulation.WentOffline -= OnWentOffline;
            _remoteViews?.Dispose();
            CloseConnection();

            // 自分で作った物(このオブジェクトの子ではない物)を片付ける
            _mapViews?.Dispose();
            DestroyIfAlive(_playerRoot);
            DestroyIfAlive(_remoteRoot);
            if (ShopWindow != null) DestroyIfAlive(ShopWindow.gameObject);
            if (LoginWindow != null) DestroyIfAlive(LoginWindow.gameObject);
        }

        private static void DestroyIfAlive(GameObject? go)
        {
            if (go != null) Destroy(go);
        }

        private void OnApplicationQuit() => CloseConnection();

        // ---- 起動 ----

        /// <summary>ログイン窓を出す。</summary>
        public void ShowLogin(string? error = null) => EnsureStartupFlow().ShowLogin(error);

        public void StartOffline() => EnsureStartupFlow().StartOffline();

        /// <summary>
        /// 接続してから始める。失敗したらログイン窓に理由を出す。
        /// remember が true なら、繋がった名前と接続先を次回の初期値として覚える(ログイン窓で押したときだけ。テストでは覚えない)。
        /// </summary>
        public Task<bool> ConnectAndStartAsync(string name, string address, bool remember = false)
            => EnsureStartupFlow().ConnectAndStartAsync(name, address, remember);

        /// <summary>起動の流れを用意する。名前と接続先は、インスペクタやテストで指定されていればそれを優先する。</summary>
        private StartupFlow EnsureStartupFlow()
        {
            if (_startupFlow != null) return _startupFlow;
            var settings = OnlineSettings.Load();
            if (!string.IsNullOrWhiteSpace(serverAddress)) settings.ServerAddress = serverAddress;
            if (!string.IsNullOrWhiteSpace(playerName)) settings.PlayerName = playerName;
            _startupFlow = new StartupFlow(settings, LoadArt, () => IsReady, () => this != null, StartWith, this);
            return _startupFlow;
        }

        private void StartWith(MagicOnionConnection? connection)
        {
            Connection = connection;
            try
            {
                if (connection != null) Initialize(connection, connection.Inbox);
                else Initialize();
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                Debug.LogException(ex, this);
            }
        }

        public void Initialize() => Initialize(null, null);

        /// <summary>シミュレーションと見た目を組み立てる。online を渡すとオンラインで始める。</summary>
        public void Initialize(IOnlineChannel? online, OnlineInbox? inbox)
        {
            if (IsReady) return;

            var content = GameContentLoader.Load(startMapId, mapFileName, LoadArt(), this);
            Maps = content.Maps;
            MasterData = content.MasterData;
            Art = content.Art;

            Simulation = SimulationFactory.Create(content, online, inbox);
            Simulation.WentOffline += OnWentOffline;

            // カメラ
            Camera = UnityEngine.Camera.main;
            if (Camera == null)
            {
                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                Camera = cameraGo.AddComponent<Camera>();
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
                cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            }
            var cameraRig = Camera.gameObject.GetComponent<CameraRig>();
            if (cameraRig == null) cameraRig = Camera.gameObject.AddComponent<CameraRig>();
            // 音を聴く耳。シーンのカメラ(ProjectSetup が作る)には付いているが、ここで作ったカメラには無い
            if (Camera.gameObject.GetComponent<AudioListener>() == null) Camera.gameObject.AddComponent<AudioListener>();

            // プレイヤー・他のプレイヤー・HUD・店(マップが変わっても作り直さない)
            _playerRoot = new GameObject("Player");
            _playerView = _playerRoot.AddComponent<PlayerView>();
            _playerView.Bind(Simulation, Art);

            _remoteRoot = new GameObject("RemotePlayers");
            _remoteViews = new RemotePlayersViewSync(_remoteRoot.transform, Simulation.RemotePlayers, Art);

            // 音(Resources に無くても黙って動く)
            var audio = Audio ??= AudioLibrary.Load();
            AudioDirector = gameObject.AddComponent<AudioDirector>();
            AudioDirector.Bind(Simulation, audio);
            Debug.Log($"[audio] 効果音 {AudioDirector.AvailableCount} / {AudioLibrary.AllSoundEffects.Count} 個を読めました{(AudioDirector.IsMuted ? "(消音中)" : string.Empty)}", this);

            _hud = gameObject.AddComponent<HudView>();
            var version = MasterData == null ? "master: fallback" : $"master: {MasterData.ShortVersion}";
            _hud.Bind(Simulation, Camera, version, Art, AudioDirector);

            ShopWindow = ShopWindow.Create(Camera, Art);
            ShopWindow.Bind(Simulation);

            // 今いるマップの見た目とクリック
            var map = content.StartMap;
            _mapViews = new MapViewSet(Simulation, Camera, cameraRig, Art, showSpawnMarkers, _playerView, this);
            _mapViews.Build(map);
            _pointer = new PointerInteraction(Simulation, Camera, _mapViews);

            var session = Simulation.Online;
            var mode = session != null ? $"online {session.ServerAddress} as {session.Self}" : "offline";
            Debug.Log($"[game] ready ({mode}): map={map.Name} (id {map.Id}) footholds={map.Footholds.Count} enemies={Simulation.World.Enemies.Count} npcs={map.Npcs.Count} maps={Maps.Count} spawn=({Simulation.SpawnPosition.X}, {Simulation.SpawnPosition.Y})", this);
        }

        private ArtLibrary LoadArt() => Art ?? ArtLibrary.Load();

        private void CloseConnection()
        {
            if (Connection == null) return;
            Connection.Dispose();
            Connection = null;
        }

        private void OnWentOffline(string reason)
        {
            Debug.LogWarning($"[online] オフラインに切り替えました: {reason}", this);
            CloseConnection();
        }

        // ---- 毎フレーム ----

        private void Update()
        {
            if (Simulation == null) return;

            _pointer?.Update();
            if (InputSource.ReadMuteToggle()) AudioDirector?.ToggleMute();

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
            _mapViews?.Sync();
            _remoteViews?.Sync();
        }

        /// <summary>ワールド座標をクリックしたとして扱う。NPC の上なら話しかける(店なら開く)。</summary>
        public bool TryClickWorld(Vector2 world) => _pointer?.TryClickWorld(world) ?? false;
    }
}
