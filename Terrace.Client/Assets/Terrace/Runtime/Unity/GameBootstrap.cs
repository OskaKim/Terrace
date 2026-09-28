using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Client.Online;
using Terrace.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Terrace.Client.Unity
{
    /// <summary>起動のしかた。</summary>
    public enum StartupMode
    {
        /// <summary>すぐにひとりで始める(テストの既定)。</summary>
        Offline,

        /// <summary>すぐにサーバーへ接続して始める。失敗したらログイン窓を出す。</summary>
        Online,

        /// <summary>ログイン窓を出して選ばせる(Main シーンの既定)。</summary>
        Login,
    }

    /// <summary>
    /// シーンに 1 つ置くだけでゲームが動く起動役。
    ///
    ///   Start ─┬─ Offline ───────────────────────────────┐
    ///          ├─ Online ── MagicOnionConnection.ConnectAsync ─┤
    ///          └─ Login ─── LoginWindow ─(選ぶ)─────────────┘
    ///                                                      ▼
    ///                                          Initialize(接続 or null)
    ///
    /// Initialize は StreamingAssets のマップ一式(maps/*.json)とマスタ(master.bytes)と素材を読み、
    /// シミュレーションと見た目を組み立てる。毎フレーム入力を渡してシミュレーションを進め、見た目を同期する。
    /// マップが変わったら見た目を組み直す。オンラインなら他のプレイヤーの見た目も出す。
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

        private GameObject? _playerRoot;
        private PlayerView? _playerView;
        private WorldViewSync? _worldView;
        private RemotePlayersViewSync? _remoteViews;
        private CameraRig? _cameraRig;
        private HudView? _hud;
        private GameObject? _mapRoot;
        private GameObject? _worldRoot;
        private GameObject? _npcRoot;
        private GameObject? _backdrop;
        private GameObject? _remoteRoot;
        private readonly List<NpcView> _npcViews = new List<NpcView>();
        private OnlineSettings? _settings;

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

        public ShopWindow? ShopWindow { get; private set; }
        public LoginWindow? LoginWindow { get; private set; }
        public Camera? Camera { get; private set; }
        public string? LastError { get; private set; }

        /// <summary>最後に接続できなかった理由。</summary>
        public string? LastConnectError { get; private set; }

        public MagicOnionConnection? Connection { get; private set; }
        public bool IsConnecting { get; private set; }
        public bool IsReady => Simulation != null;
        public IReadOnlyList<NpcView> NpcViews => _npcViews;
        public int RemotePlayerViewCount => _remoteViews?.Count ?? 0;

        private void Start()
        {
            _settings = OnlineSettings.Load();
            if (!string.IsNullOrWhiteSpace(serverAddress)) _settings.ServerAddress = serverAddress;
            if (!string.IsNullOrWhiteSpace(playerName)) _settings.PlayerName = playerName;

            var mode = startupMode;
            if (_settings.AutoOffline) mode = StartupMode.Offline;
            else if (_settings.AutoOnline) mode = StartupMode.Online;

            switch (mode)
            {
                case StartupMode.Online:
                    _ = ConnectAndStartAsync(_settings.PlayerName, _settings.ServerAddress);
                    break;
                case StartupMode.Login:
                    ShowLogin();
                    break;
                default:
                    StartOffline();
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Simulation != null)
            {
                Simulation.MapChanged -= OnMapChanged;
                Simulation.WorldReplaced -= OnWorldReplaced;
                Simulation.WentOffline -= OnWentOffline;
            }
            _remoteViews?.Dispose();
            CloseConnection();

            // 自分で作った物(このオブジェクトの子ではない物)を片付ける
            TearDownMapViews();
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
        public void ShowLogin(string? error = null)
        {
            if (IsReady) return;
            if (LoginWindow == null)
            {
                var art = LoadArt();
                LoginWindow = LoginWindow.Create(art, _settings?.PlayerName ?? string.Empty, _settings?.ServerAddress ?? OnlineSettings.DefaultServer);
                LoginWindow.OnlineRequested += (name, address) => _ = ConnectAndStartAsync(name, address, remember: true);
                LoginWindow.OfflineRequested += StartOffline;
            }
            if (error != null) LoginWindow.SetStatus(error, true);
        }

        public void StartOffline()
        {
            if (IsReady || IsConnecting) return;
            CloseLogin();
            InitializeSafe(null);
        }

        /// <summary>
        /// 接続してから始める。失敗したらログイン窓に理由を出す。
        /// remember が true なら、繋がった名前と接続先を次回の初期値として覚える(ログイン窓で押したときだけ。テストでは覚えない)。
        /// </summary>
        public async Task<bool> ConnectAndStartAsync(string name, string address, bool remember = false)
        {
            if (IsReady || IsConnecting) return false;
            IsConnecting = true;
            LastConnectError = null;
            LoginWindow?.SetBusy(true, $"{address} に接続中…");

            MagicOnionConnection connection;
            try
            {
                connection = await MagicOnionConnection.ConnectAsync(address, name);
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                LastConnectError = $"{ex.GetType().Name}: {ex.Message}";
                Debug.LogWarning($"[online] 接続できませんでした: {LastConnectError}", this);
                if (this == null) return false;
                LoginWindow?.SetBusy(false, string.Empty);
                ShowLogin($"接続できませんでした。サーバーは起動していますか?\n{ex.Message}");
                return false;
            }

            IsConnecting = false;
            if (this == null)
            {
                // 待っている間にシーンごと消えた
                connection.Dispose();
                return false;
            }

            if (remember && _settings != null)
            {
                _settings.PlayerName = name;
                _settings.ServerAddress = address;
                _settings.Save();
            }

            Connection = connection;
            CloseLogin();
            InitializeSafe(connection);
            return IsReady;
        }

        private void InitializeSafe(MagicOnionConnection? connection)
        {
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

            // マップ
            Maps = MapRegistry.LoadFromStreamingAssets();
            Maps.LogWarnings(this);
            MapData map;
            if (!string.IsNullOrEmpty(mapFileName))
            {
                map = MapLoader.LoadFromStreamingAssets(mapFileName);
                if (Maps.Get(map.Id) == null) Maps.Add(map, mapFileName);
            }
            else
            {
                map = Maps.Get(startMapId) ?? throw new InvalidOperationException($"マップ ID {startMapId} が maps/ にありません (読めたのは {Maps.Count} 枚)");
            }
            foreach (var issue in map.Validate())
            {
                Debug.LogWarning($"[map] {issue}", this);
            }

            // マスタ
            try
            {
                MasterData = MasterDataRepository.LoadFromStreamingAssets();
                Debug.Log($"[masterdata] loaded: items={MasterData.ItemCount} enemies={MasterData.EnemyCount} quests={MasterData.QuestCount} sha={MasterData.ShortVersion}", this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[masterdata] master.bytes を読めませんでした。仮の定義で続行します: {ex.Message}", this);
            }

            // 素材
            var art = LoadArt();
            if (art.IsAvailable)
            {
                Art = art;
                Debug.Log("[art] Kenney Platformer Art Deluxe / UI Pack (CC0) の素材を使います", this);
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
            IItemCatalog items = masterData ?? (IItemCatalog)new FallbackItems();
            Func<int, string> itemName = id => items.Get(id)?.Name ?? $"item{id}";

            var playerConfig = PlayerConfig.Default;
            if (Art?.Player != null)
            {
                playerConfig.Height = Art.Player.Height;
                playerConfig.HalfWidth = Art.Player.Width * 0.4f;
            }

            var registry = Maps;
            Simulation = new GameSimulation(
                map, enemyLookup, itemName,
                playerConfig: playerConfig,
                mapLookup: registry.Get,
                items: items,
                shops: new PlaceholderShopCatalog(items),
                online: online,
                inbox: inbox);
            Simulation.MapChanged += OnMapChanged;
            Simulation.WorldReplaced += OnWorldReplaced;
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
            _cameraRig = Camera.gameObject.GetComponent<CameraRig>();
            if (_cameraRig == null) _cameraRig = Camera.gameObject.AddComponent<CameraRig>();

            // プレイヤー・他のプレイヤー・HUD・店(マップが変わっても作り直さない)
            _playerRoot = new GameObject("Player");
            _playerView = _playerRoot.AddComponent<PlayerView>();
            _playerView.Bind(Simulation, Art);

            _remoteRoot = new GameObject("RemotePlayers");
            _remoteViews = new RemotePlayersViewSync(_remoteRoot.transform, Simulation.RemotePlayers, Art);

            _hud = gameObject.AddComponent<HudView>();
            var version = masterData == null ? "master: fallback" : $"master: {masterData.ShortVersion}";
            _hud.Bind(Simulation, Camera, version, Art);

            ShopWindow = ShopWindow.Create(Camera, Art);
            ShopWindow.Bind(Simulation);

            BuildMapViews(map);

            var mode = Simulation.IsOnline ? $"online {Simulation.ServerAddress} as {Simulation.Self}" : "offline";
            Debug.Log($"[game] ready ({mode}): map={map.Name} (id {map.Id}) footholds={map.Footholds.Count} enemies={Simulation.World.Enemies.Count} npcs={map.Npcs.Count} maps={Maps.Count} spawn=({Simulation.SpawnPosition.X}, {Simulation.SpawnPosition.Y})", this);
        }

        private ArtLibrary LoadArt() => Art ?? ArtLibrary.Load();

        private void CloseLogin()
        {
            if (LoginWindow == null) return;
            LoginWindow.Close();
            LoginWindow = null;
        }

        private void CloseConnection()
        {
            if (Connection == null) return;
            Connection.Dispose();
            Connection = null;
        }

        // ---- マップと世界の見た目 ----

        private void OnMapChanged(MapData previous, MapData next)
        {
            TearDownMapViews();
            BuildMapViews(next);
            Debug.Log($"[game] map changed: {previous.Name} -> {next.Name} (enemies={Simulation!.World.Enemies.Count}, npcs={next.Npcs.Count})", this);
        }

        private void OnWorldReplaced(LocalWorld world)
        {
            if (_worldRoot != null) Destroy(_worldRoot);
            _worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(_worldRoot.transform, world, Art);
        }

        private void OnWentOffline(string reason)
        {
            Debug.LogWarning($"[online] オフラインに切り替えました: {reason}", this);
            CloseConnection();
        }

        private void BuildMapViews(MapData map)
        {
            if (Simulation == null || Camera == null || _cameraRig == null) return;

            _mapRoot = new GameObject("Map");
            _mapRoot.AddComponent<MapView>().Build(map, showSpawnMarkers, Art);

            _worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(_worldRoot.transform, Simulation.World, Art);

            _npcRoot = new GameObject("Npcs");
            _npcViews.Clear();
            foreach (var npc in map.Npcs)
            {
                var go = new GameObject($"Npc {npc.Name}#{npc.Id}");
                go.transform.SetParent(_npcRoot.transform, false);
                var view = go.AddComponent<NpcView>();
                view.Bind(npc, Art);
                _npcViews.Add(view);
            }

            _cameraRig.Bind(Simulation);
            var theme = Art?.Theme(map.Theme);
            if (theme != null)
            {
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = theme.SkyColor;
                if (theme.Background != null)
                {
                    _backdrop = new GameObject("Backdrop");
                    _backdrop.AddComponent<ParallaxBackdrop>().Bind(Camera, theme.Background, Camera.orthographicSize);
                }
            }

            _playerView?.Sync();
        }

        private void TearDownMapViews()
        {
            if (_mapRoot != null) Destroy(_mapRoot);
            if (_worldRoot != null) Destroy(_worldRoot);
            if (_npcRoot != null) Destroy(_npcRoot);
            if (_backdrop != null) Destroy(_backdrop);
            _mapRoot = null;
            _worldRoot = null;
            _npcRoot = null;
            _backdrop = null;
            _worldView = null;
            _npcViews.Clear();
        }

        // ---- 毎フレーム ----

        private void Update()
        {
            if (Simulation == null) return;

            HandlePointer();

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
            _remoteViews?.Sync();
        }

        private void HandlePointer()
        {
            if (Simulation == null || Camera == null || Simulation.ActiveShop != null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var screen = mouse.position.ReadValue();
            var world = Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.transform.position.z));
            TryClickWorld(new Vector2(world.x, world.y));
        }

        /// <summary>ワールド座標をクリックしたとして扱う。NPC の上なら話しかける(店なら開く)。</summary>
        public bool TryClickWorld(Vector2 world)
        {
            if (Simulation == null) return false;
            foreach (var view in _npcViews)
            {
                if (view.Npc != null && view.Contains(world))
                {
                    Simulation.Interact(view.Npc);
                    return true;
                }
            }
            return false;
        }
    }
}
