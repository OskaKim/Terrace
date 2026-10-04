using System;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 撃破の報酬の係: 世界の権威の「敵が倒れた」のうち、倒した人が自分のものだけで、キル数・メソ・経験値を足す。
    /// オフラインでもオンラインでも 1 体につき 1 度だけ(権威が 1 回の死につき 1 度しか知らせないため)。
    /// 規則は docs/spec/economy-shop.md(メソ)と docs/spec/progression.md(経験値とレベル)。
    /// </summary>
    public sealed class KillRewardSystem
    {
        private readonly GameContext _context;

        public KillRewardSystem(GameContext context)
        {
            _context = context;
            context.EnemyKilled += OnEnemyKilled;
        }

        /// <summary>自分が敵を倒した(報酬を足した後)。</summary>
        public event Action<EnemyEntity>? EnemyKilled;

        /// <summary>自分のレベルが上がった。引数は上がった先のレベル。1 度に複数上がれば、上がった数だけ 1 つずつ起きる。</summary>
        public event Action<int>? LeveledUp;

        private void OnEnemyKilled(EnemyKill kill)
        {
            if (kill.Killer.IsSelf) Grant(kill.Enemy, kill.DroppedItemIds);
        }

        private void Grant(EnemyEntity enemy, int[] droppedItemIds)
        {
            var player = _context.Player;
            player.Kills++;
            var reward = enemy.Definition.EffectiveMesoReward;
            player.Meso += reward;
            var exp = player.Progression.IsMaxLevel ? 0 : Math.Max(0, enemy.Definition.Exp);
            var levelBefore = player.Level;
            var gained = player.GainExp(exp);
            var expText = exp > 0 ? $", +{exp} EXP" : "";
            var drops = droppedItemIds.Length == 0
                ? ""
                : $" ドロップ: {string.Join(", ", Array.ConvertAll(droppedItemIds, _context.ItemName))}";
            _context.Messages.Add(_context.Time, $"{enemy.Definition.Name} を倒した (+{reward} メソ{expText}){drops}");
            EnemyKilled?.Invoke(enemy);

            if (gained == 0) return;
            _context.Messages.Add(_context.Time, $"レベルが上がった! Lv. {player.Level} (最大 HP {player.MaxHp}、攻撃力 {player.Attack})");
            for (var level = levelBefore + 1; level <= player.Level; level++) LeveledUp?.Invoke(level);
        }
    }
}
