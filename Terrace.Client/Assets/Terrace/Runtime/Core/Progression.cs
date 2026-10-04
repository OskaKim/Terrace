using System;
using System.Collections.Generic;

namespace Terrace.Client.Core
{
    /// <summary>レベル表の 1 行(マスタの player_level の 1 行)。</summary>
    public sealed class LevelDefinition
    {
        public int Level { get; set; }

        /// <summary>次のレベルに上がるのに要る経験値。0 以下なら最高レベル。</summary>
        public int ExpToNext { get; set; }

        /// <summary>そのレベルの最大 HP。</summary>
        public int MaxHp { get; set; }

        /// <summary>そのレベルの攻撃力(1 回の攻撃で与えるダメージ)。</summary>
        public int Attack { get; set; }
    }

    /// <summary>レベル表。行は Level の小さい順に並べ直して持つ。一番小さい Level から始まり、表の最後の行が最高レベル。</summary>
    public sealed class LevelTable
    {
        private readonly List<LevelDefinition> _rows;
        private readonly Dictionary<int, LevelDefinition> _byLevel = new Dictionary<int, LevelDefinition>();

        public LevelTable(IEnumerable<LevelDefinition> rows)
        {
            _rows = new List<LevelDefinition>(rows ?? throw new ArgumentNullException(nameof(rows)));
            if (_rows.Count == 0) throw new ArgumentException("レベル表に行がありません", nameof(rows));
            _rows.Sort((a, b) => a.Level.CompareTo(b.Level));
            foreach (var row in _rows)
            {
                if (!_byLevel.ContainsKey(row.Level)) _byLevel.Add(row.Level, row);
            }
        }

        public IReadOnlyList<LevelDefinition> Rows => _rows;
        public int MinLevel => _rows[0].Level;
        public int MaxLevel => _rows[_rows.Count - 1].Level;

        public LevelDefinition? Find(int level) => _byLevel.TryGetValue(level, out var row) ? row : null;

        /// <summary>
        /// master.bytes が無いときと、レベル表を渡さずに GameSimulation を作ったとき(試験など)の逃げ道。
        /// samples/csv/player_level.csv の先頭 5 行と同じ値で、5 行目を最高レベルにしてある。
        /// </summary>
        public static LevelTable Fallback => new LevelTable(new[]
        {
            new LevelDefinition { Level = 1, ExpToNext = 15, MaxHp = 100, Attack = 10 },
            new LevelDefinition { Level = 2, ExpToNext = 30, MaxHp = 110, Attack = 11 },
            new LevelDefinition { Level = 3, ExpToNext = 50, MaxHp = 120, Attack = 12 },
            new LevelDefinition { Level = 4, ExpToNext = 75, MaxHp = 130, Attack = 13 },
            new LevelDefinition { Level = 5, ExpToNext = 0, MaxHp = 140, Attack = 15 },
        });
    }

    /// <summary>
    /// 経験値とレベルの規則: 経験値を足す → 今のレベルの ExpToNext に届けば差し引いてレベルを 1 上げる(1 度に複数上がることもある)
    /// → 最大 HP と攻撃力が新しいレベルの行の値になる。最高レベルでは経験値は増えない。
    ///
    /// Unity・通信・GameSimulation を知らない。後で権威を Server へ移すときは、このクラスをそのまま持っていく。
    /// </summary>
    public sealed class PlayerProgression
    {
        private readonly LevelTable _table;

        public PlayerProgression(LevelTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            Level = table.MinLevel;
        }

        public int Level { get; private set; }

        /// <summary>今のレベルで溜まっている経験値(レベルが上がると、届いた分を差し引いた残り)。</summary>
        public int Exp { get; private set; }

        public LevelDefinition Current => _table.Find(Level)!;

        /// <summary>今のレベルの行が最高レベル(ExpToNext が 0 以下、または表の次の行が無い)。</summary>
        public bool IsMaxLevel => Current.ExpToNext <= 0 || _table.Find(Level + 1) == null;

        /// <summary>次のレベルに要る経験値。最高レベルでは 0。</summary>
        public int ExpToNext => IsMaxLevel ? 0 : Current.ExpToNext;

        public int MaxHp => Current.MaxHp;
        public int Attack => Current.Attack;

        /// <summary>経験値の溜まり具合(0〜1)。最高レベルでは 1。</summary>
        public float ExpRatio => IsMaxLevel ? 1f : Math.Min(1f, (float)Exp / ExpToNext);

        /// <summary>経験値を足し、上がったレベルの数を返す。0 以下の量と、最高レベルでは何も変わらない。</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            Exp += amount;
            var gained = 0;
            while (!IsMaxLevel && Exp >= Current.ExpToNext)
            {
                Exp -= Current.ExpToNext;
                Level++;
                gained++;
            }
            if (IsMaxLevel) Exp = 0;
            return gained;
        }
    }
}
