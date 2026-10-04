using System;
using System.Collections.Generic;
using Terrace.Client.Core.Online;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Core
{
    /// <summary>
    /// ゲーム 1 セッション分のシミュレーション。
    /// 入力を受けて、移動・攻撃・接触ダメージ・拾う・経験値とレベル・ポータル・マップ移動・死亡と復活・店を進める。UnityEngine には依存しない。
    ///
    /// 敵と落とし物の世界は「入れ物」(WorldState)と「誰が決めるか」(IWorldAuthority)に分かれている。
    /// 攻撃と拾うは世界の権威へ頼み、報酬と持ち物は権威の結果のイベントだけを見て動かす(オンラインかどうかで分けない)。
    ///
    /// オフライン: 世界の権威は OfflineRoom。マップ ID ごとに覚えておき、戻ってきたときはそのまま続きになる。
    /// オンライン: 接続(送り口・受け箱・スナップショット待ち・移動の送信)は OnlineSession が持つ。世界の権威は OnlineSession がマップごとに作る RoomMirror。
    ///            自分の移動・HP・所持品・経験値とレベル・店はこれまで通りここで計算する(クライアント権威)。他のプレイヤーは RemotePlayers に映る。
    ///            接続が切れたらオフラインに切り替え、今いるマップの敵を自分で湧かせて遊び続けられるようにする。
    /// </summary>
    public sealed class GameSimulation
    {
        private readonly Func<int, MapData?>? _mapLookup;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly Func<int, string> _itemNameLookup;
        private readonly IItemCatalog? _items;
        private readonly IShopCatalog? _shops;
        private readonly MotorConfig _motorConfig;
        private readonly Random _random;
        private readonly Dictionary<int, OfflineRoom> _offlineRooms = new Dictionary<int, OfflineRoom>();
        private IWorldAuthority _authority = null!;
        private OnlineSession? _online;

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
            _enemyLookup = enemyLookup;
            _itemNameLookup = itemNameLookup;
            _mapLookup = mapLookup;
            _items = items;
            _shops = shops;
            _motorConfig = motorConfig ?? MotorConfig.Default;
            _random = random ?? new Random();
            PlayerConfig = playerConfig ?? PlayerConfig.Default;
            Player = new PlayerState(new PlayerProgression(levels ?? LevelTable.Fallback)) { Meso = PlayerConfig.StartingMeso };
            Messages = new MessageLog();

            Map = map;
            SpawnPosition = ResolveSpawnPosition(map);
            Motor = new CharacterMotor(map, _motorConfig, SpawnPosition.X, SpawnPosition.Y);

            if (online != null)
            {
                var onlineInbox = inbox ?? throw new ArgumentNullException(nameof(inbox), "オンラインでは受け箱(OnlineInbox)も渡してください");
                _online = new OnlineSession(online, onlineInbox, _enemyLookup, RemotePlayers);
                _online.Synchronized += OnSynchronized;
                _online.PlayerJoined += OnPlayerJoined;
                _online.PlayerLeft += OnPlayerLeft;
            }
            SetAuthority(CreateAuthority(map));
        }

        public MapData Map { get; private set; }
        public CharacterMotor Motor { get; private set; }

        /// <summary>今いるマップの敵と落とし物。</summary>
        public WorldState World => _authority.State;

        /// <summary>今いるマップの敵と落とし物を決める権威(オフラインは OfflineRoom、オンラインは RoomMirror)。</summary>
        public IWorldAuthority WorldAuthority => _authority;

        /// <summary>オンラインの接続。オフラインでは null。</summary>
        public OnlineSession? Online => _online;

        public bool IsOnline => _online != null;

        /// <summary>同じマップにいる他のプレイヤー。オフラインでは空のまま、セッションの間ずっと同じもの(見た目が購読し続ける)。</summary>
        public RemotePlayerRegistry RemotePlayers { get; } = new RemotePlayerRegistry();

        public PlayerState Player { get; }
        public PlayerConfig PlayerConfig { get; }
        public MessageLog Messages { get; }
        public Position SpawnPosition { get; private set; }
        public float Time { get; private set; }

        /// <summary>開いている店。null なら開いていない。開いている間は移動の入力を受け付けない。</summary>
        public ShopSession? ActiveShop { get; private set; }

        public IReadOnlyList<Npc> Npcs => Map.Npcs;

        /// <summary>自分が跳んだ(地上・はしごからの跳び上がり、↓+ジャンプの飛び降りを含む)。</summary>
        public event Action? Jumped;

        public event Action<AttackOutcome>? Attacked;

        /// <summary>自分が敵を倒した。オフラインでもオンラインでも 1 体につき 1 度だけ(報酬を得るのと同じ所)。</summary>
        public event Action<EnemyEntity>? EnemyKilled;

        /// <summary>自分のレベルが上がった。引数は上がった先のレベル。1 度に複数上がれば、上がった数だけ 1 つずつ起きる。</summary>
        public event Action<int>? LeveledUp;

        public event Action<int>? PlayerDamaged;
        public event Action? PlayerDied;
        public event Action? PlayerRespawned;
        public event Action<ItemDrop>? ItemPickedUp;
        public event Action<Portal, Portal?>? PortalUsed;
        public event Action<MapData, MapData>? MapChanged;
        public event Action<ShopSession>? ShopOpened;
        public event Action? ShopClosed;

        /// <summary>店で買う・売るを試した。引数はその結果(成功は ShopResult.Ok)。</summary>
        public event Action<ShopResult>? ShopTraded;

        /// <summary>世界(敵と落とし物)が別物に差し替わった(オフラインへの切り替えなど、マップはそのまま)。</summary>
        public event Action<WorldState>? WorldReplaced;

        /// <summary>接続が切れてオフラインに切り替わった。引数は理由。</summary>
        public event Action<string>? WentOffline;

        public void Step(in InputFrame input, float dt)
        {
            Time += dt;
            if (_online != null)
            {
                _online.Pump();
                if (_online.IsDisconnected) GoOffline(_online.DisconnectReason);
            }
            _authority.Tick(dt);
            RemotePlayers.Tick(dt);

            StepPlayer(input, dt);

            _online?.SendMoveIfNeeded(OnlineSession.MoveStateOf(Motor, Player), dt);
        }

        private void StepPlayer(in InputFrame input, float dt)
        {
            if (Player.IsDead)
            {
                Player.RespawnTimer -= dt;
                if (Player.RespawnTimer <= 0f) RespawnPlayer();
                return;
            }

            if (Player.InvulnerableTimer > 0f) Player.InvulnerableTimer -= dt;

            // 店を開いている間はその場に立ち止まる
            var effectiveInput = ActiveShop != null ? InputFrame.None : input;
            var events = Motor.Step(effectiveInput, dt);

            if (events.Jumped) Jumped?.Invoke();

            if (events.AttackStarted)
            {
                ResolveAttack();
            }

            if (events.EnteredPortal != null)
            {
                EnterPortal(events.EnteredPortal);
                return;
            }

            if (events.FellOutOfWorld)
            {
                KillPlayer("落下");
                return;
            }

            if (effectiveInput.PickupPressed)
            {
                // 拾えたかどうかは世界の権威の結果(OnDropRemoved)で分かる
                var drop = World.FindPickupCandidate(Motor.X, Motor.Y, PlayerConfig.PickupRange);
                if (drop != null) _authority.RequestPickup(drop);
            }

            if (!Player.IsInvulnerable && Motor.Mode != MotorMode.Ladder)
            {
                var touching = World.FindTouchingEnemy(Motor.X, Motor.Y, PlayerConfig.HalfWidth, PlayerConfig.Height);
                if (touching != null) TakeContactDamage(touching);
            }
        }

        public string ItemName(int itemId) => _itemNameLookup(itemId);

        // ---- NPC と店 ----

        /// <summary>NPC に話しかける。店なら開き、そうでなければ一言を表示する。</summary>
        public bool Interact(Npc npc)
        {
            if (npc.IsShop) return TryOpenShop(npc);

            Messages.Add(Time, string.IsNullOrEmpty(npc.Greeting) ? $"{npc.Name}: ……" : $"{npc.Name}: {npc.Greeting}");
            return false;
        }

        public bool TryOpenShop(Npc npc)
        {
            if (Player.IsDead) return false;
            if (!npc.IsShop || _items == null || _shops == null) return false;

            var shop = _shops.Get(npc.ShopId);
            if (shop == null)
            {
                Messages.Add(Time, $"{npc.Name}: 店 '{npc.ShopId}' はまだありません");
                return false;
            }

            ActiveShop = new ShopSession(npc, shop, _items, Player);
            Messages.Add(Time, string.IsNullOrEmpty(npc.Greeting) ? $"{npc.Name}: いらっしゃい" : $"{npc.Name}: {npc.Greeting}");
            ShopOpened?.Invoke(ActiveShop);
            return true;
        }

        public void CloseShop()
        {
            if (ActiveShop == null) return;
            Messages.Add(Time, $"{ActiveShop.Npc.Name}: またどうぞ");
            ActiveShop = null;
            ShopClosed?.Invoke();
        }

        /// <summary>店で買う。結果をメッセージにも流す。</summary>
        public ShopResult Buy(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotSoldHere;
            var result = ActiveShop.Buy(itemId, count);
            switch (result)
            {
                case ShopResult.Ok:
                    Messages.Add(Time, $"{ItemName(itemId)} を {count} 個買った (残り {Player.Meso:N0} メソ)");
                    break;
                case ShopResult.NotEnoughMeso:
                    Messages.Add(Time, "メソが足りない");
                    break;
                default:
                    Messages.Add(Time, "それは買えない");
                    break;
            }
            ShopTraded?.Invoke(result);
            return result;
        }

        /// <summary>店で売る。</summary>
        public ShopResult Sell(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotInInventory;
            var result = ActiveShop.Sell(itemId, count);
            Messages.Add(Time, result == ShopResult.Ok
                ? $"{ItemName(itemId)} を {count} 個売った (所持 {Player.Meso:N0} メソ)"
                : "それは売れない");
            ShopTraded?.Invoke(result);
            return result;
        }

        // ---- マップ移動 ----

        /// <summary>別のマップへ移る。portalName のポータル(無ければ出現地点)に立つ。</summary>
        public void ChangeMap(MapData map, string? portalName = null)
        {
            var previous = Map;
            if (ActiveShop != null) CloseShop();

            Map = map;
            SpawnPosition = ResolveSpawnPosition(map);

            var target = portalName != null ? map.FindPortalByName(portalName) : null;
            var position = target?.Position ?? SpawnPosition;
            Motor = new CharacterMotor(map, _motorConfig, position.X, position.Y);
            // 着いた先のポータルに立つので、↑を押しっぱなしでもすぐ引き返さないよう待ち時間を入れる
            Motor.Teleport(position.X, position.Y);
            Player.InvulnerableTimer = PlayerConfig.InvulnerableSeconds;

            // オンラインでは着いた位置で参加し直す(前のマップの他のプレイヤーは消え、スナップショット待ちに戻る)
            SetAuthority(CreateAuthority(map));
            Messages.Add(Time, $"{map.Name} へ移動した");
            MapChanged?.Invoke(previous, map);
        }

        private void EnterPortal(Portal portal)
        {
            if (portal.IsSpawn) return;

            if (portal.TargetMapId == Map.Id)
            {
                var target = Map.FindPortalByName(portal.TargetPortalName);
                if (target != null)
                {
                    Motor.Teleport(target.X, target.Y);
                    Messages.Add(Time, $"ポータル {portal.Name} から {target.Name} へ移動");
                    PortalUsed?.Invoke(portal, target);
                    return;
                }
            }
            else
            {
                var next = _mapLookup?.Invoke(portal.TargetMapId);
                if (next != null)
                {
                    var target = next.FindPortalByName(portal.TargetPortalName);
                    PortalUsed?.Invoke(portal, target);
                    ChangeMap(next, portal.TargetPortalName);
                    return;
                }
            }

            Messages.Add(Time, $"ポータル {portal.Name} の行き先 (map {portal.TargetMapId} '{portal.TargetPortalName}') はまだありません");
            PortalUsed?.Invoke(portal, null);
        }

        // ---- 世界の権威 ----

        /// <summary>
        /// map の世界の権威を用意する。オンラインでは敵の状態はサーバーにあるので、今の位置で参加を送り、空の RoomMirror でスナップショットを待つ。
        /// オフラインではマップごとの OfflineRoom を覚えておき、戻ってきたら続きにする。
        /// </summary>
        private IWorldAuthority CreateAuthority(MapData map)
        {
            if (_online != null) return _online.JoinMap(map, OnlineSession.MoveStateOf(Motor, Player));

            if (_offlineRooms.TryGetValue(map.Id, out var existing)) return existing;
            var room = new OfflineRoom(map, _enemyLookup, _random);
            room.SpawnFromMap();
            _offlineRooms[map.Id] = room;
            return room;
        }

        /// <summary>世界の権威を差し替え、結果のイベントを付け替える。</summary>
        private void SetAuthority(IWorldAuthority next)
        {
            if (_authority != null)
            {
                _authority.EnemyDamaged -= OnEnemyDamaged;
                _authority.EnemyKilled -= OnEnemyKilled;
                _authority.DropRemoved -= OnDropRemoved;
            }
            _authority = next;
            next.EnemyDamaged += OnEnemyDamaged;
            next.EnemyKilled += OnEnemyKilled;
            next.DropRemoved += OnDropRemoved;
        }

        private void OnEnemyDamaged(EnemyDamage damage)
        {
            if (!damage.Attacker.IsSelf || damage.Enemy.Hp <= 0) return;
            Messages.Add(Time, $"{damage.Enemy.Definition.Name} に {damage.Damage} ダメージ (残り {damage.Enemy.Hp})");
        }

        private void OnEnemyKilled(EnemyKill kill)
        {
            if (kill.Killer.IsSelf)
            {
                GrantKillReward(kill.Enemy, kill.DroppedItemIds);
                return;
            }

            var killerId = kill.Killer.PlayerId;
            var killer = RemotePlayers.Find(killerId)?.Name ?? $"player{killerId}";
            Messages.Add(Time, $"{killer} が {kill.Enemy.Definition.Name} を倒した");
        }

        private void OnDropRemoved(DropRemoval removal)
        {
            if (!removal.Picker.IsSelf) return;
            Player.Inventory.Add(removal.Drop.ItemId);
            Messages.Add(Time, $"{ItemName(removal.Drop.ItemId)} を拾った");
            ItemPickedUp?.Invoke(removal.Drop);
        }

        // ---- オンライン ----

        /// <summary>接続を手放してオフラインで続ける(切断の印を受けたときも呼ばれる)。今いるマップの敵を自分で湧かせ直す。</summary>
        public void GoOffline(string reason)
        {
            if (_online == null) return;

            _online.Synchronized -= OnSynchronized;
            _online.PlayerJoined -= OnPlayerJoined;
            _online.PlayerLeft -= OnPlayerLeft;
            _online = null;
            RemotePlayers.Clear();

            SetAuthority(CreateAuthority(Map));
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

        // ---- 戦闘と生死 ----

        /// <summary>向いている方向の一番近い敵に当て、世界の権威へダメージを頼む。HP・撃破・報酬は権威の結果のイベントで反映する。</summary>
        private void ResolveAttack()
        {
            var target = World.FindAttackTarget(Motor.X, Motor.Y, Motor.Facing, PlayerConfig.AttackRange, PlayerConfig.AttackHeight);
            if (target == null)
            {
                Attacked?.Invoke(AttackOutcome.Miss);
                return;
            }

            var damage = Math.Max(0, Player.Attack);
            Attacked?.Invoke(new AttackOutcome(target, damage));
            _authority.RequestAttack(target, damage);
        }

        /// <summary>自分が倒した敵の報酬(キル数・メソ・経験値)。世界の権威の撃破の結果で、倒した人が自分のとき。</summary>
        private void GrantKillReward(EnemyEntity enemy, int[] droppedItemIds)
        {
            Player.Kills++;
            var reward = enemy.Definition.EffectiveMesoReward;
            Player.Meso += reward;
            var exp = Player.Progression.IsMaxLevel ? 0 : Math.Max(0, enemy.Definition.Exp);
            var levelBefore = Player.Level;
            var gained = Player.GainExp(exp);
            var expText = exp > 0 ? $", +{exp} EXP" : "";
            var drops = droppedItemIds.Length == 0
                ? ""
                : $" ドロップ: {string.Join(", ", Array.ConvertAll(droppedItemIds, ItemName))}";
            Messages.Add(Time, $"{enemy.Definition.Name} を倒した (+{reward} メソ{expText}){drops}");
            EnemyKilled?.Invoke(enemy);

            if (gained == 0) return;
            Messages.Add(Time, $"レベルが上がった! Lv. {Player.Level} (最大 HP {Player.MaxHp}、攻撃力 {Player.Attack})");
            for (var level = levelBefore + 1; level <= Player.Level; level++) LeveledUp?.Invoke(level);
        }

        private void TakeContactDamage(EnemyEntity enemy)
        {
            var damage = Math.Max(1, enemy.Definition.Attack);
            Player.Hp = Math.Max(0, Player.Hp - damage);
            Player.InvulnerableTimer = PlayerConfig.InvulnerableSeconds;
            Messages.Add(Time, $"{enemy.Definition.Name} から {damage} ダメージ");
            PlayerDamaged?.Invoke(damage);

            var away = Motor.X >= enemy.X ? 1f : -1f;
            Motor.Launch(away * PlayerConfig.KnockbackVelocityX, PlayerConfig.KnockbackVelocityY);

            if (Player.Hp <= 0) KillPlayer(enemy.Definition.Name);
        }

        private void KillPlayer(string cause)
        {
            if (ActiveShop != null) CloseShop();
            Player.Hp = 0;
            Player.IsDead = true;
            Player.RespawnTimer = PlayerConfig.RespawnSeconds;
            Messages.Add(Time, $"倒れた… ({cause})");
            PlayerDied?.Invoke();
        }

        private void RespawnPlayer()
        {
            Player.Hp = Player.MaxHp;
            Player.IsDead = false;
            Player.RespawnTimer = 0f;
            Player.InvulnerableTimer = PlayerConfig.InvulnerableSeconds;
            Motor.Teleport(SpawnPosition.X, SpawnPosition.Y);
            Messages.Add(Time, "復活した");
            PlayerRespawned?.Invoke();
        }

        private static Position ResolveSpawnPosition(MapData map)
        {
            var spawn = map.FindSpawnPortal();
            if (spawn != null) return spawn.Position;
            if (map.Portals.Count > 0) return map.Portals[0].Position;
            if (map.Footholds.Count > 0)
            {
                var first = map.Footholds[0];
                var x = (first.Left + first.Right) * 0.5f;
                return new Position(x, first.GetYAt(x));
            }
            return Position.Zero;
        }
    }
}
