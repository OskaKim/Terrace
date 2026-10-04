using System;
using System.Collections.Generic;
using Terrace.Client.Core.Online;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// マップ移動の係: ポータル(同じマップの中の移動と別のマップへの移動)、マップの出現地点、マップごとの世界の権威を受け持つ。
    ///
    /// オフラインではマップごとの OfflineRoom を覚えておき、戻ってきたときはそのまま続きになる。
    /// オンラインではマップに入るたびに OnlineSession が参加を送り、新しい RoomMirror でスナップショットを待つ。
    /// 規則は docs/spec/map-travel-npc.md。
    /// </summary>
    public sealed class TravelSystem
    {
        private readonly GameContext _context;
        private readonly MotorConfig _motorConfig;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly Random _random;
        private readonly Func<int, MapData?>? _mapLookup;
        private readonly Dictionary<int, OfflineRoom> _offlineRooms = new Dictionary<int, OfflineRoom>();

        public TravelSystem(GameContext context, MotorConfig motorConfig, Func<int, EnemyDefinition?> enemyLookup, Random random, Func<int, MapData?>? mapLookup)
        {
            _context = context;
            _motorConfig = motorConfig;
            _enemyLookup = enemyLookup;
            _random = random;
            _mapLookup = mapLookup;
        }

        /// <summary>ポータルを使った(行き先が無ければ to は null)。</summary>
        public event Action<Portal, Portal?>? PortalUsed;

        /// <summary>別のマップへ移る直前(店を閉じるなど、移る前に片付けたい係のため)。引数は今のマップと移る先。</summary>
        public event Action<MapData, MapData>? MapChanging;

        /// <summary>別のマップへ移った。引数は前のマップと今のマップ。</summary>
        public event Action<MapData, MapData>? MapChanged;

        /// <summary>今のマップの世界の権威を用意して差し替える(始めたとき・オフラインに切り替えたとき)。</summary>
        public void EnterWorld() => _context.SetAuthority(CreateAuthority(_context.Map));

        /// <summary>ポータルに入った。同じマップの中ならテレポート、別のマップなら移る。行き先が無ければ知らせるだけ。</summary>
        public void EnterPortal(Portal portal)
        {
            if (portal.IsSpawn) return;

            if (portal.TargetMapId == _context.Map.Id)
            {
                var target = _context.Map.FindPortalByName(portal.TargetPortalName);
                if (target != null)
                {
                    _context.Motor.Teleport(target.X, target.Y);
                    _context.Messages.Add(_context.Time, $"ポータル {portal.Name} から {target.Name} へ移動");
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

            _context.Messages.Add(_context.Time, $"ポータル {portal.Name} の行き先 (map {portal.TargetMapId} '{portal.TargetPortalName}') はまだありません");
            PortalUsed?.Invoke(portal, null);
        }

        /// <summary>別のマップへ移る。portalName のポータル(無ければ出現地点)に立つ。</summary>
        public void ChangeMap(MapData map, string? portalName = null)
        {
            var previous = _context.Map;
            MapChanging?.Invoke(previous, map);

            _context.Map = map;
            _context.SpawnPosition = ResolveSpawnPosition(map);

            var target = portalName != null ? map.FindPortalByName(portalName) : null;
            var position = target?.Position ?? _context.SpawnPosition;
            _context.Motor = new CharacterMotor(map, _motorConfig, position.X, position.Y);
            // 着いた先のポータルに立つので、↑を押しっぱなしでもすぐ引き返さないよう待ち時間を入れる
            _context.Motor.Teleport(position.X, position.Y);
            _context.Player.InvulnerableTimer = _context.PlayerConfig.InvulnerableSeconds;

            // オンラインでは着いた位置で参加し直す(前のマップの他のプレイヤーは消え、スナップショット待ちに戻る)
            EnterWorld();
            _context.Messages.Add(_context.Time, $"{map.Name} へ移動した");
            MapChanged?.Invoke(previous, map);
        }

        /// <summary>マップの出現地点。出現用のポータル、無ければ最初のポータル、無ければ最初の足場の真ん中。</summary>
        public static Position ResolveSpawnPosition(MapData map)
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

        private IWorldAuthority CreateAuthority(MapData map)
        {
            var online = _context.Online;
            if (online != null) return online.JoinMap(map, OnlineSession.MoveStateOf(_context.Motor, _context.Player));

            if (_offlineRooms.TryGetValue(map.Id, out var existing)) return existing;
            var room = new OfflineRoom(map, _enemyLookup, _random);
            room.SpawnFromMap();
            _offlineRooms[map.Id] = room;
            return room;
        }
    }
}
