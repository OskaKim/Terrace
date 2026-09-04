using System.Threading.Tasks;
using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>StreamingHub: クライアント → サーバー。1 マップ = 1 ルーム。</summary>
    public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
    {
        /// <summary>マップに参加する。成功すると <see cref="IGameHubReceiver.OnSnapshot"/> で現在の全状態が届く。</summary>
        ValueTask JoinAsync(int mapId, PlayerInfo self);

        ValueTask LeaveAsync();

        /// <summary>クライアント権威の移動状態を送る。サーバーは他のクライアントへ中継する。</summary>
        ValueTask MoveAsync(MoveState state);

        /// <summary>敵にダメージを与える。HP・死亡・リスポーン・ドロップはサーバー権威。</summary>
        ValueTask AttackAsync(int enemyInstanceId, int damage);
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
    }
}
