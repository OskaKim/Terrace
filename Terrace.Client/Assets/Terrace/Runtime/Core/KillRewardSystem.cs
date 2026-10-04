using System;

namespace Terrace.Client.Core
{
    /// <summary>自分が倒した敵 1 体分の報酬。</summary>
    public readonly struct KillReward
    {
        public KillReward(EnemyEntity enemy, int meso, int exp, int[] droppedItemIds, int levelsGained)
        {
            Enemy = enemy;
            Meso = meso;
            Exp = exp;
            DroppedItemIds = droppedItemIds;
            LevelsGained = levelsGained;
        }

        public EnemyEntity Enemy { get; }

        /// <summary>得たメソ。</summary>
        public int Meso { get; }

        /// <summary>得た経験値(最高レベルでは 0)。</summary>
        public int Exp { get; }

        /// <summary>敵が落とした品の ItemId。</summary>
        public int[] DroppedItemIds { get; }

        /// <summary>この撃破で上がったレベルの数。</summary>
        public int LevelsGained { get; }
    }

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

        /// <summary>自分が倒した敵の報酬を足した(何をいくら得たか)。</summary>
        public event Action<KillReward>? Rewarded;

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
            Rewarded?.Invoke(new KillReward(enemy, reward, exp, droppedItemIds, gained));
            EnemyKilled?.Invoke(enemy);

            for (var level = levelBefore + 1; level <= player.Level; level++) LeveledUp?.Invoke(level);
        }
    }
}
