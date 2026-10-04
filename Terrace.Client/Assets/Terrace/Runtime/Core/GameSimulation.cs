using System;
using System.Collections.Generic;
using Terrace.Client.Core.Online;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// ゲーム 1 セッション分のシミュレーション。UnityEngine には依存しない。
    /// ここには係(~System)の組み立てと、1 フレームの中で係を呼ぶ順番だけを書く。規則は係にある。
    ///
    ///   PlayerLifeSystem  被弾・ノックバック・死亡・復活・無敵
    ///   CombatSystem      攻撃の相手探し → 世界の権威へ頼む
    ///   KillRewardSystem  自分が倒したときの報酬(メソ・キル数・経験値とレベル)
    ///   LootingSystem     拾う → 世界の権威へ頼む。自分が拾えたら持ち物へ
    ///   TravelSystem      ポータル・マップ移動・マップごとの世界の権威
    ///   TradingSystem     NPC に話す・店を開く・買う・売る・閉じる
    ///
    /// 係が共有して読む状態は GameContext。敵と落とし物の世界は「入れ物」(WorldState)と「誰が決めるか」(IWorldAuthority)に分かれ、
    /// 報酬と持ち物は権威の結果のイベントだけを見て動く(オンラインかどうかで分けない)。
    /// オンラインでは接続を OnlineSession が持ち、切れたらオフラインに切り替えて今いるマップの敵を自分で湧かせ直す。
    /// </summary>
    public sealed class GameSimulation
    {
        private readonly GameContext _context;

        public GameSimulation(
            MapData map,
            Func<int, EnemyDefinition?> enemyLookup,
            Func<int, string> itemNameLookup,
            MotorConfig? motorConfig = null,
            PlayerConfig? playerConfig = null,
            Random? random = null,
            Func<int, MapData?>? mapLookup = null,
            IItemCatalog? items = null,
            IShopCatalog? shops = null,
            IOnlineChannel? online = null,
            OnlineInbox? inbox = null,
            LevelTable? levels = null)
        {
            var config = playerConfig ?? PlayerConfig.Default;
            var motors = motorConfig ?? MotorConfig.Default;
            var player = new PlayerState(new PlayerProgression(levels ?? LevelTable.Fallback)) { Meso = config.StartingMeso };
            var spawn = TravelSystem.ResolveSpawnPosition(map);
            _context = new GameContext(map, new CharacterMotor(map, motors, spawn.X, spawn.Y), player, config, new MessageLog(), itemNameLookup)
            {
                SpawnPosition = spawn,
            };

            if (online != null)
            {
                var onlineInbox = inbox ?? throw new ArgumentNullException(nameof(inbox), "オンラインでは受け箱(OnlineInbox)も渡してください");
                _context.Online = new OnlineSession(online, onlineInbox, enemyLookup, _context.RemotePlayers);
                _context.Online.Synchronized += OnSynchronized;
                _context.Online.PlayerJoined += OnPlayerJoined;
                _context.Online.PlayerLeft += OnPlayerLeft;
            }

            Travel = new TravelSystem(_context, motors, enemyLookup, random ?? new Random(), mapLookup);
            Life = new PlayerLifeSystem(_context);
            Combat = new CombatSystem(_context);
            Rewards = new KillRewardSystem(_context);
            Looting = new LootingSystem(_context);
            Trading = new TradingSystem(_context, items, shops);

            // 倒れるときとマップを移るときは、先に店を閉じる
            Life.Dying += Trading.CloseShop;
            Travel.MapChanging += (_, __) => Trading.CloseShop();

            Travel.EnterWorld();
        }

        // ---- 係 ----

        public PlayerLifeSystem Life { get; }
        public CombatSystem Combat { get; }
        public KillRewardSystem Rewards { get; }
        public LootingSystem Looting { get; }
        public TravelSystem Travel { get; }
        public TradingSystem Trading { get; }

        // ---- 状態の入口 ----

        public GameContext Context => _context;
        public MapData Map => _context.Map;
        public CharacterMotor Motor => _context.Motor;

        /// <summary>今いるマップの敵と落とし物。</summary>
        public WorldState World => _context.World;

        /// <summary>今いるマップの敵と落とし物を決める権威(オフラインは OfflineRoom、オンラインは RoomMirror)。</summary>
        public IWorldAuthority WorldAuthority => _context.Authority;

        /// <summary>オンラインの接続。オフラインでは null。</summary>
        public OnlineSession? Online => _context.Online;

        public bool IsOnline => _context.Online != null;

        /// <summary>同じマップにいる他のプレイヤー。オフラインでは空のまま、セッションの間ずっと同じもの(見た目が購読し続ける)。</summary>
        public RemotePlayerRegistry RemotePlayers => _context.RemotePlayers;

        public PlayerState Player => _context.Player;
        public PlayerConfig PlayerConfig => _context.PlayerConfig;
        public MessageLog Messages => _context.Messages;
        public Position SpawnPosition => _context.SpawnPosition;
        public float Time => _context.Time;
        public IReadOnlyList<Npc> Npcs => Map.Npcs;

        public string ItemName(int itemId) => _context.ItemName(itemId);

        /// <summary>自分が跳んだ(地上・はしごからの跳び上がり、↓+ジャンプの飛び降りを含む)。</summary>
        public event Action? Jumped;

        /// <summary>世界(敵と落とし物)が別物に差し替わった(オフラインへの切り替えなど、マップはそのまま)。</summary>
        public event Action<WorldState>? WorldReplaced;

        /// <summary>接続が切れてオフラインに切り替わった。引数は理由。</summary>
        public event Action<string>? WentOffline;

        // ---- 1 フレーム ----

        public void Step(in InputFrame input, float dt)
        {
            _context.Time += dt;
            var online = _context.Online;
            if (online != null)
            {
                online.Pump();
                if (online.IsDisconnected) GoOffline(online.DisconnectReason);
            }
            _context.Authority.Tick(dt);
            RemotePlayers.Tick(dt);

            if (Life.Tick(dt)) StepPlayer(input, dt);

            _context.Online?.SendMoveIfNeeded(OnlineSession.MoveStateOf(Motor, Player), dt);
        }

        private void StepPlayer(in InputFrame input, float dt)
        {
            // 店を開いている間はその場に立ち止まる
            var effectiveInput = Trading.IsOpen ? InputFrame.None : input;
            var events = Motor.Step(effectiveInput, dt);

            if (events.Jumped) Jumped?.Invoke();
            if (events.AttackStarted) Combat.Attack();

            if (events.EnteredPortal != null)
            {
                Travel.EnterPortal(events.EnteredPortal);
                return;
            }

            if (events.FellOutOfWorld)
            {
                Life.Kill("落下");
                return;
            }

            if (effectiveInput.PickupPressed) Looting.Pickup();
            Life.CheckContact();
        }

        // ---- オンライン ----

        /// <summary>接続を手放してオフラインで続ける(切断の印を受けたときも呼ばれる)。今いるマップの敵を自分で湧かせ直す。</summary>
        public void GoOffline(string reason)
        {
            var online = _context.Online;
            if (online == null) return;

            online.Synchronized -= OnSynchronized;
            online.PlayerJoined -= OnPlayerJoined;
            online.PlayerLeft -= OnPlayerLeft;
            _context.Online = null;
            RemotePlayers.Clear();

            Travel.EnterWorld();
            Messages.Add(Time, $"サーバーとの接続が切れた。オフラインで続けます ({reason})");
            WorldReplaced?.Invoke(World);
            WentOffline?.Invoke(reason);
        }

        private void OnSynchronized(int others)
        {
            var text = others == 0 ? "ほかに誰もいない" : $"ほかに {others} 人";
            Messages.Add(Time, $"{Map.Name} に入った ({text})");
        }

        private void OnPlayerJoined(RemotePlayer player) => Messages.Add(Time, $"{player.Name} がやって来た");

        private void OnPlayerLeft(RemotePlayer player) => Messages.Add(Time, $"{player.Name} が去った");
    }
}
