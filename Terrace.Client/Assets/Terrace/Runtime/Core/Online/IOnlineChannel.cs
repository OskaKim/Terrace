using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// サーバーへの送信口。GameSimulation はこれだけを知っていて、通信の実装(MagicOnion)は知らない。
    /// 実装は送るだけで結果を待たない(結果は <see cref="OnlineInbox"/> に通知として届く)。
    /// テストでは送った内容を記録する偽物に差し替える。
    /// </summary>
    public interface IOnlineChannel
    {
        /// <summary>ログインで発行された自分。</summary>
        PlayerInfo Self { get; }

        /// <summary>接続先(表示用)。</summary>
        string ServerAddress { get; }

        /// <summary>マップ(ルーム)に入る。別のマップにいれば先に退出する。</summary>
        void Join(int mapId, MoveState state);

        void Move(MoveState state);

        void Attack(int enemyInstanceId, int damage);

        void Pickup(int dropId);
    }
}
