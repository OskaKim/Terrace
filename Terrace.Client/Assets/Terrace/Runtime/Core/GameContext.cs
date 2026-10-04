using System;
using Terrace.Client.Core.Online;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 1 セッションの中で係(~System)が共有して読む状態: 今のマップ・Motor・世界の権威・プレイヤー・時刻・設定・メッセージ欄。
    ///
    /// マップが変わると Map・Motor・世界の権威は別物になるが、係は作り直さない。係は古いものを握らず、毎回ここから読む。
    /// 世界の権威の結果のイベントはここで中継するので、係は権威が差し替わっても購読し直さなくてよい。
    /// </summary>
    public sealed class GameContext
    {
        private readonly Func<int, string> _itemName;

        public GameContext(MapData map, CharacterMotor motor, PlayerState player, PlayerConfig playerConfig, MessageLog messages, Func<int, string> itemName)
        {
            Map = map;
            Motor = motor;
            Player = player;
            PlayerConfig = playerConfig;
            Messages = messages;
            _itemName = itemName;
        }

        public MapData Map { get; internal set; }

        /// <summary>今のマップの出現地点(復活する所)。</summary>
        public Position SpawnPosition { get; internal set; }

        public CharacterMotor Motor { get; internal set; }
        public PlayerState Player { get; }
        public PlayerConfig PlayerConfig { get; }
        public MessageLog Messages { get; }
        public float Time { get; internal set; }

        /// <summary>同じマップにいる他のプレイヤー。オフラインでは空のまま、セッションの間ずっと同じもの(見た目が購読し続ける)。</summary>
        public RemotePlayerRegistry RemotePlayers { get; } = new RemotePlayerRegistry();

        /// <summary>オンラインの接続。オフラインでは null。</summary>
        public OnlineSession? Online { get; internal set; }

        /// <summary>今いるマップの敵と落とし物を決める権威(オフラインは OfflineRoom、オンラインは RoomMirror)。</summary>
        public IWorldAuthority Authority { get; private set; } = null!;

        /// <summary>今いるマップの敵と落とし物。</summary>
        public WorldState World => Authority.State;

        /// <summary>敵が傷ついた(今の世界の権威から中継する)。</summary>
        public event Action<EnemyDamage>? EnemyDamaged;

        /// <summary>敵が倒れた(今の世界の権威から中継する)。</summary>
        public event Action<EnemyKill>? EnemyKilled;

        /// <summary>落とし物が消えた(今の世界の権威から中継する)。</summary>
        public event Action<DropRemoval>? DropRemoved;

        public string ItemName(int itemId) => _itemName(itemId);

        /// <summary>世界の権威を差し替え、結果のイベントの中継を付け替える。</summary>
        internal void SetAuthority(IWorldAuthority next)
        {
            if (Authority != null)
            {
                Authority.EnemyDamaged -= RelayEnemyDamaged;
                Authority.EnemyKilled -= RelayEnemyKilled;
                Authority.DropRemoved -= RelayDropRemoved;
            }
            Authority = next;
            next.EnemyDamaged += RelayEnemyDamaged;
            next.EnemyKilled += RelayEnemyKilled;
            next.DropRemoved += RelayDropRemoved;
        }

        private void RelayEnemyDamaged(EnemyDamage damage) => EnemyDamaged?.Invoke(damage);

        private void RelayEnemyKilled(EnemyKill kill) => EnemyKilled?.Invoke(kill);

        private void RelayDropRemoved(DropRemoval removal) => DropRemoved?.Invoke(removal);
    }
}
