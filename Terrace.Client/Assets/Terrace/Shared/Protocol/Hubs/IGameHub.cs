using System.Threading.Tasks;
using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>
    /// StreamingHub: クライアント → サーバー。1 マップ = 1 ルーム。
    /// 要求の引数は DTO 1 つにする(項目を足すときは DTO の <c>[Key]</c> を末尾に足す)。
    /// 返事の要らない要求は <c>void</c>(送りっぱなし)にし、結果は通知(<see cref="IGameHubReceiver.OnNotification"/>)で届く。
    /// </summary>
    public interface IGameHub : IStreamingHub<IGameHub, IGameHubReceiver>
    {
        /// <summary>
        /// マップに参加する。既に別のマップにいれば先に退出する(マップ移動もこれで行う)。
        /// 成功すると <see cref="RoomSnapshot"/> の通知で現在の全状態が届く。参加を断られたら失敗を返す。
        /// </summary>
        ValueTask JoinAsync(JoinRequest request);

        ValueTask LeaveAsync();

        /// <summary>クライアント権威の移動状態を送る。サーバーは他のクライアントへ中継する。</summary>
        void Move(MoveState state);

        /// <summary>敵にダメージを与える。HP・死亡・リスポーン・ドロップはサーバー権威。</summary>
        void Attack(AttackRequest request);

        /// <summary>落ちているアイテムを拾う。早い者勝ちで、結果は <see cref="DropRemovedNotification"/> で全員に届く。</summary>
        void Pickup(PickupRequest request);
    }

    /// <summary>StreamingHub: サーバー → クライアント。通知はすべてこの 1 本で届く(<see cref="INotification"/>)。</summary>
    public interface IGameHubReceiver
    {
        void OnNotification(INotification notification);
    }
}
