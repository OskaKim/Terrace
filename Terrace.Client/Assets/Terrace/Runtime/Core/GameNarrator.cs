using System;
using Terrace.Client.Core.Online;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// メッセージ欄の文言を作る係。メッセージ欄(MessageLog)に書くのはここだけで、規則の係はイベントで知らせるだけにする。
    ///
    /// 係・世界の権威(GameContext が中継する結果)・オンラインの接続のイベントを見て、起きた順に一行ずつ流す。
    /// 文言を変えたいときも、メッセージを足したいときも、ここだけを直す。
    /// </summary>
    public sealed class GameNarrator
    {
        private readonly GameContext _context;

        public GameNarrator(GameContext context, PlayerLifeSystem life, KillRewardSystem rewards, LootingSystem looting, TravelSystem travel, TradingSystem trading)
        {
            _context = context;

            context.EnemyDamaged += OnEnemyDamaged;
            context.EnemyKilled += OnEnemyKilled;

            life.PlayerDamaged += (enemy, damage) => Say($"{enemy.Definition.Name} から {damage} ダメージ");
            life.PlayerDied += cause => Say($"倒れた… ({cause})");
            life.PlayerRespawned += () => Say("復活した");

            rewards.Rewarded += OnRewarded;
            looting.ItemPickedUp += drop => Say($"{context.ItemName(drop.ItemId)} を拾った");

            travel.Teleported += (from, to) => Say($"ポータル {from.Name} から {to.Name} へ移動");
            travel.PortalDeadEnd += portal => Say($"ポータル {portal.Name} の行き先 (map {portal.TargetMapId} '{portal.TargetPortalName}') はまだありません");
            travel.MapChanged += (previous, next) => Say($"{next.Name} へ移動した");

            trading.Talked += npc => Say(string.IsNullOrEmpty(npc.Greeting) ? $"{npc.Name}: ……" : $"{npc.Name}: {npc.Greeting}");
            trading.ShopNotFound += npc => Say($"{npc.Name}: 店 '{npc.ShopId}' はまだありません");
            trading.ShopOpened += shop => Say(string.IsNullOrEmpty(shop.Npc.Greeting) ? $"{shop.Npc.Name}: いらっしゃい" : $"{shop.Npc.Name}: {shop.Npc.Greeting}");
            trading.ShopClosed += shop => Say($"{shop.Npc.Name}: またどうぞ");
            trading.ShopTraded += OnShopTraded;

            var online = context.Online;
            if (online != null)
            {
                online.Synchronized += OnSynchronized;
                online.PlayerJoined += player => Say($"{player.Name} がやって来た");
                online.PlayerLeft += player => Say($"{player.Name} が去った");
            }
        }

        /// <summary>接続が切れてオフラインに切り替わった(GameSimulation.WentOffline に繋ぐ)。</summary>
        public void OnWentOffline(string reason) => Say($"サーバーとの接続が切れた。オフラインで続けます ({reason})");

        private void Say(string text) => _context.Messages.Add(_context.Time, text);

        private void OnEnemyDamaged(EnemyDamage damage)
        {
            if (!damage.Attacker.IsSelf || damage.Enemy.Hp <= 0) return;
            Say($"{damage.Enemy.Definition.Name} に {damage.Damage} ダメージ (残り {damage.Enemy.Hp})");
        }

        private void OnEnemyKilled(EnemyKill kill)
        {
            // 自分が倒したときは報酬と一緒に流す(OnRewarded)
            if (kill.Killer.IsSelf) return;
            var killerId = kill.Killer.PlayerId;
            var killer = _context.RemotePlayers.Find(killerId)?.Name ?? $"player{killerId}";
            Say($"{killer} が {kill.Enemy.Definition.Name} を倒した");
        }

        private void OnRewarded(KillReward reward)
        {
            var expText = reward.Exp > 0 ? $", +{reward.Exp} EXP" : "";
            var drops = reward.DroppedItemIds.Length == 0
                ? ""
                : $" ドロップ: {string.Join(", ", Array.ConvertAll(reward.DroppedItemIds, _context.ItemName))}";
            Say($"{reward.Enemy.Definition.Name} を倒した (+{reward.Meso} メソ{expText}){drops}");

            if (reward.LevelsGained == 0) return;
            var player = _context.Player;
            Say($"レベルが上がった! Lv. {player.Level} (最大 HP {player.MaxHp}、攻撃力 {player.Attack})");
        }

        private void OnShopTraded(ShopTrade trade)
        {
            var player = _context.Player;
            if (trade.Kind == ShopTradeKind.Buy)
            {
                switch (trade.Result)
                {
                    case ShopResult.Ok:
                        Say($"{_context.ItemName(trade.ItemId)} を {trade.Count} 個買った (残り {player.Meso:N0} メソ)");
                        break;
                    case ShopResult.NotEnoughMeso:
                        Say("メソが足りない");
                        break;
                    default:
                        Say("それは買えない");
                        break;
                }
                return;
            }

            Say(trade.Result == ShopResult.Ok
                ? $"{_context.ItemName(trade.ItemId)} を {trade.Count} 個売った (所持 {player.Meso:N0} メソ)"
                : "それは売れない");
        }

        private void OnSynchronized(int others)
        {
            var text = others == 0 ? "ほかに誰もいない" : $"ほかに {others} 人";
            Say($"{_context.Map.Name} に入った ({text})");
        }
    }
}
