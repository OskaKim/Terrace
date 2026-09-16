using System.Threading.Tasks;
using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>StreamingHub: クライアント → サーバー。1 マップ = 1 ルーム。</summary>
    public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
    {
        /// <summary>
        /// マップに参加する。既に別のマップにいれば先に退出する(マップ移動もこれで行う)。
        /// 成功すると <see cref="IGameHubReceiver.OnSnapshot"/> で現在の全状態が届く。
        /// state は参加した瞬間の位置(他の人にはここに現れる)。
        /// </summary>
        ValueTask JoinAsync(int mapId, PlayerInfo self, MoveState state);

        ValueTask LeaveAsync();

        /// <summary>クライアント権威の移動状態を送る。サーバーは他のクライアントへ中継する。</summary>
        ValueTask MoveAsync(MoveState state);

        /// <summary>敵にダメージを与える。HP・死亡・リスポーン・ドロップはサーバー権威。</summary>
        ValueTask AttackAsync(int enemyInstanceId, int damage);

        /// <summary>落ちているアイテムを拾う。早い者勝ちで、結果は <see cref="IGameHubReceiver.OnDropRemoved"/> で全員に届く。</summary>
        ValueTask PickupAsync(int dropId);
    }

    /// <summary>StreamingHub: サーバー → クライアント。</summary>
    public interface IGameHubReceiver
    {
        void OnJoin(PlayerInfo player, MoveState state);
        void OnLeave(int playerId);
        void OnMove(int playerId, MoveState state);
        void OnEnemySpawn(EnemyState enemy);
        void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage);
        void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds);
        void OnSnapshot(RoomSnapshot snapshot);

        /// <summary>敵の位置(定期配信、生きている敵だけ)。</summary>
        void OnEnemyMove(EnemyMoveState[] enemies);

        /// <summary>アイテムが落ちた。</summary>
        void OnDropSpawn(DropState[] drops);

        /// <summary>落ちていたアイテムが消えた。playerId が拾った人(0 なら時間切れ)。</summary>
        void OnDropRemoved(int dropId, int playerId);
    }
}
