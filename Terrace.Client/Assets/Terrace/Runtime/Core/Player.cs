using System.Collections.Generic;

namespace Terrace.Client.Core
{
    /// <summary>プレイヤーの戦闘パラメータ。</summary>
    public sealed class PlayerConfig
    {
        public int MaxHp { get; set; } = 100;
        public int AttackDamage { get; set; } = 10;

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

        public static PlayerConfig Default => new PlayerConfig();
    }

    /// <summary>プレイヤーの状態。</summary>
    public sealed class PlayerState
    {
        public PlayerState(int maxHp)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public int MaxHp { get; }
        public int Hp { get; internal set; }
        public float InvulnerableTimer { get; internal set; }
        public bool IsDead { get; internal set; }
        public float RespawnTimer { get; internal set; }
        public int Kills { get; internal set; }
        public List<int> Inventory { get; } = new List<int>();

        public bool IsInvulnerable => InvulnerableTimer > 0f;
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
