using MessagePack;

namespace Terrace.Shared
{
    /// <summary>ルーム内の敵 1 体の現在状態(サーバー権威)。</summary>
    [MessagePackObject]
    public sealed class EnemyState
    {
        /// <summary>ルーム内で一意なインスタンス ID。攻撃対象の指定に使う。</summary>
        [Key(0)]
        public int InstanceId { get; set; }

        /// <summary>マスタ上の敵 ID。</summary>
        [Key(1)]
        public int EnemyId { get; set; }

        [Key(2)]
        public float X { get; set; }

        [Key(3)]
        public float Y { get; set; }

        [Key(4)]
        public int Hp { get; set; }

        [Key(5)]
        public int MaxHp { get; set; }

        [Key(6)]
        public bool IsDead { get; set; }

        public override string ToString() => $"enemy#{InstanceId} (id={EnemyId}) hp={Hp}/{MaxHp}{(IsDead ? " dead" : "")} at ({X:F1}, {Y:F1})";
    }
}
