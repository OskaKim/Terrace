using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Tables
{
    /// <summary>クエストマスタ。対応する CSV は quest.csv。</summary>
    [MemoryTable("quest"), MessagePackObject(true)]
    public sealed class Quest : IValidatable<Quest>
    {
        public const int MinRequiredLevel = 1;
        public const int MaxRequiredLevel = 99;

        [PrimaryKey]
        public int QuestId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>報酬アイテム。item.csv の ItemId を参照する。</summary>
        public int RewardItemId { get; set; }

        public int RequiredLevel { get; set; }

        void IValidatable<Quest>.Validate(IValidator<Quest> validator)
        {
            // 参照チェック: RewardItemId は item テーブルに存在しなければならない
            validator.GetReferenceSet<Item>().Exists(x => x.RewardItemId, y => y.ItemId);

            // 範囲チェック
            validator.Validate(
                x => x.RequiredLevel >= MinRequiredLevel && x.RequiredLevel <= MaxRequiredLevel,
                $"[RequiredLevel] {MinRequiredLevel}〜{MaxRequiredLevel} の範囲で指定してください (value = {RequiredLevel})");
        }
    }
}
