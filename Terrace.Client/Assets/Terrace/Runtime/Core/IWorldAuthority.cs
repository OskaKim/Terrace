using System;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 敵と落とし物の状態を誰が決めるか。オフラインは <see cref="OfflineRoom"/>(自分で決める)、
    /// オンラインは <see cref="Online.RoomMirror"/>(サーバーの結果を映す)。
    ///
    /// 攻撃と拾うは「頼む」だけで、結果はどちらの権威でも同じイベントで届く。
    /// OfflineRoom は頼まれたその場で決めてすぐイベントを出し、RoomMirror は送るだけで、サーバーの通知が届いたときにイベントを出す。
    /// 報酬や持ち物はイベントだけを見ればよく、オンラインかどうかを知らなくてよい。
    /// </summary>
    public interface IWorldAuthority
    {
        /// <summary>敵と落とし物の入れ物。見た目と問い合わせはここを読む。</summary>
        WorldState State { get; }

        void Tick(float dt);

        /// <summary>target に damage を与えるよう頼む。</summary>
        void RequestAttack(EnemyEntity target, int damage);

        /// <summary>drop を拾うよう頼む。</summary>
        void RequestPickup(ItemDrop drop);

        /// <summary>敵が傷ついた。</summary>
        event Action<EnemyDamage>? EnemyDamaged;

        /// <summary>敵が倒れた。1 体の 1 回の死につき 1 度だけ。</summary>
        event Action<EnemyKill>? EnemyKilled;

        /// <summary>敵が湧いた(復活を含む)。</summary>
        event Action<EnemyEntity>? EnemySpawned;

        /// <summary>落とし物が出た。</summary>
        event Action<ItemDrop>? DropSpawned;

        /// <summary>落とし物が消えた(拾われた、または時間切れ)。</summary>
        event Action<DropRemoval>? DropRemoved;
    }
}
