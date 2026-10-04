using System.Collections.Generic;

namespace Terrace.Client.Core
{
    /// <summary>プレイヤーの戦闘パラメータ。最大 HP と攻撃力はレベル表(LevelTable)の今のレベルの行から引くので、ここには無い。</summary>
    public sealed class PlayerConfig
    {
        /// <summary>攻撃が届く前方の距離。</summary>
        public float AttackRange { get; set; } = 1.6f;

        /// <summary>攻撃が届く高さの差。</summary>
        public float AttackHeight { get; set; } = 1.2f;

        /// <summary>被弾後の無敵時間。</summary>
        public float InvulnerableSeconds { get; set; } = 1.0f;

        public float RespawnSeconds { get; set; } = 2.0f;
        public float KnockbackVelocityX { get; set; } = 4f;
        public float KnockbackVelocityY { get; set; } = 5f;

        /// <summary>当たり判定の半幅と高さ(足元基準)。</summary>
        public float HalfWidth { get; set; } = 0.35f;
        public float Height { get; set; } = 1.5f;

        public float PickupRange { get; set; } = 1.0f;

        /// <summary>開始時の所持金。</summary>
        public long StartingMeso { get; set; } = 3000;

        public static PlayerConfig Default => new PlayerConfig();
    }

    /// <summary>プレイヤーの状態。</summary>
    public sealed class PlayerState
    {
        public PlayerState(PlayerProgression progression)
        {
            Progression = progression;
            Hp = MaxHp;
        }

        /// <summary>経験値とレベル。</summary>
        public PlayerProgression Progression { get; }

        public int Level => Progression.Level;
        public int Exp => Progression.Exp;

        /// <summary>最大 HP。今のレベルの行の値。</summary>
        public int MaxHp => Progression.MaxHp;

        /// <summary>攻撃力(1 回の攻撃で与えるダメージ)。今のレベルの行の値。</summary>
        public int Attack => Progression.Attack;

        public int Hp { get; internal set; }
        public float InvulnerableTimer { get; internal set; }
        public bool IsDead { get; internal set; }
        public float RespawnTimer { get; internal set; }
        public int Kills { get; internal set; }
        public List<int> Inventory { get; } = new List<int>();

        /// <summary>所持金。</summary>
        public long Meso { get; internal set; }

        public bool IsInvulnerable => InvulnerableTimer > 0f;

        /// <summary>
        /// 経験値を得て、上がったレベルの数を返す。上がったら HP を新しい最大 HP まで全快する
        /// (倒れている間は全快しない。復活で全快する)。
        /// </summary>
        internal int GainExp(int amount)
        {
            var gained = Progression.AddExp(amount);
            if (gained > 0 && !IsDead) Hp = MaxHp;
            return gained;
        }

        public int CountOf(int itemId)
        {
            var count = 0;
            foreach (var id in Inventory) if (id == itemId) count++;
            return count;
        }
        public float HpRatio => MaxHp <= 0 ? 0f : (float)Hp / MaxHp;
    }

    /// <summary>画面に流すメッセージの履歴。</summary>
    public sealed class MessageLog
    {
        private readonly List<(float Time, string Text)> _items = new List<(float, string)>();

        public MessageLog(int capacity = 8)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }
        public IReadOnlyList<(float Time, string Text)> Items => _items;

        public void Add(float time, string text)
        {
            _items.Add((time, text));
            while (_items.Count > Capacity) _items.RemoveAt(0);
        }

        public bool Contains(string fragment)
        {
            foreach (var item in _items) if (item.Text.Contains(fragment)) return true;
            return false;
        }
    }
}
