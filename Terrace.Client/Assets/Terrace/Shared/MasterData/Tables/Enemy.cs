using System.Linq;
using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Tables
{
    /// <summary>敵マスタ。対応する CSV は enemy.csv。</summary>
    [MemoryTable("enemy"), MessagePackObject(true)]
    public sealed class Enemy : IValidatable<Enemy>
    {
        [PrimaryKey]
        public int EnemyId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Hp { get; set; }

        public int Attack { get; set; }

        /// <summary>ドロップ候補のアイテムID一覧。CSV では | 区切り(例: 1|2|3)。</summary>
        public int[] DropItemIds { get; set; } = System.Array.Empty<int>();

        void IValidatable<Enemy>.Validate(IValidator<Enemy> validator)
        {
            // 範囲チェック
            validator.Validate(x => x.Hp >= 1, $"[Hp] 1 以上で指定してください (value = {Hp})");
            validator.Validate(x => x.Attack >= 0, $"[Attack] 0 以上で指定してください (value = {Attack})");

            // 参照チェック: DropItemIds の各要素は item テーブルに存在しなければならない
            var items = validator.GetReferenceSet<Item>();
            foreach (var dropItemId in DropItemIds ?? System.Array.Empty<int>())
            {
                var id = dropItemId;
                validator.Validate(
                    x => items.TableData.Any(item => item.ItemId == id),
                    $"[DropItemIds] item.csv に存在しないIDを参照しています (value = {id})");
            }
        }
    }
}
