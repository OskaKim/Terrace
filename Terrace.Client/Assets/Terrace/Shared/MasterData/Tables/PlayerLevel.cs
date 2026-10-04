using System.Linq;
using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Tables
{
    /// <summary>プレイヤーのレベル表。対応する CSV は player_level.csv。1 行が 1 レベルで、Level は 1 から欠けなく続く。</summary>
    [MemoryTable("player_level"), MessagePackObject(true)]
    public sealed class PlayerLevel : IValidatable<PlayerLevel>
    {
        public const int MinLevel = 1;

        [PrimaryKey]
        public int Level { get; set; }

        /// <summary>次のレベルに上がるのに要る経験値。最高レベルの行は 0。</summary>
        public int ExpToNext { get; set; }

        /// <summary>そのレベルの最大 HP。</summary>
        public int MaxHp { get; set; }

        /// <summary>そのレベルの攻撃力(1 回の攻撃で与えるダメージ)。</summary>
        public int Attack { get; set; }

        void IValidatable<PlayerLevel>.Validate(IValidator<PlayerLevel> validator)
        {
            var levels = validator.GetTableSet().TableData;
            var level = Level;
            var maxLevel = levels.Max(x => x.Level);

            // 連続性: 欠けは、すぐ下のレベルの行が無い行で知らせる(行ごとに出せば行番号まで逆引きできる)
            validator.Validate(x => x.Level >= MinLevel, $"[Level] {MinLevel} 以上で指定してください (value = {Level})");
            if (level > MinLevel)
            {
                validator.Validate(
                    x => levels.Any(other => other.Level == level - 1),
                    $"[Level] Level {level - 1} の行がありません。{MinLevel} から欠けなく続けてください (value = {Level})");
            }

            // 最高レベルの行だけが 0、それ以外は 1 以上
            if (level == maxLevel)
            {
                validator.Validate(x => x.ExpToNext == 0, $"[ExpToNext] 最高レベルの行は 0 にしてください (value = {ExpToNext})");
            }
            else
            {
                validator.Validate(x => x.ExpToNext >= 1, $"[ExpToNext] 最高レベル以外の行は 1 以上で指定してください (value = {ExpToNext})");
            }

            validator.Validate(x => x.MaxHp >= 1, $"[MaxHp] 1 以上で指定してください (value = {MaxHp})");
            validator.Validate(x => x.Attack >= 1, $"[Attack] 1 以上で指定してください (value = {Attack})");
        }
    }
}
