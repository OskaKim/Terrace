using System;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 拾う係: 近くの落とし物を世界の権威へ拾うよう頼み、権威の「落とし物が消えた」のうち拾った人が自分のものだけを持ち物に入れる。
    /// 規則は docs/spec/enemy-drop.md。
    /// </summary>
    public sealed class LootingSystem
    {
        private readonly GameContext _context;

        public LootingSystem(GameContext context)
        {
            _context = context;
            context.DropRemoved += OnDropRemoved;
        }

        /// <summary>自分が拾って持ち物に入った。</summary>
        public event Action<ItemDrop>? ItemPickedUp;

        /// <summary>拾う(拾うキーを押したとき)。拾えたかどうかは権威の結果で分かる。</summary>
        public void Pickup()
        {
            var motor = _context.Motor;
            var drop = _context.World.FindPickupCandidate(motor.X, motor.Y, _context.PlayerConfig.PickupRange);
            if (drop != null) _context.Authority.RequestPickup(drop);
        }

        private void OnDropRemoved(DropRemoval removal)
        {
            if (!removal.Picker.IsSelf) return;
            _context.Player.Inventory.Add(removal.Drop.ItemId);
            _context.Messages.Add(_context.Time, $"{_context.ItemName(removal.Drop.ItemId)} を拾った");
            ItemPickedUp?.Invoke(removal.Drop);
        }
    }
}
