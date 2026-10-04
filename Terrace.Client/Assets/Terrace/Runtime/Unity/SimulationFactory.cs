using System;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 読み込んだ物(GameContent)と接続から GameSimulation を作る。
    /// 敵とアイテムの引き方(マスタが無ければ Fallback)、レベル表、プレイヤーの設定をここで決める。
    /// </summary>
    public static class SimulationFactory
    {
        /// <summary>online を渡すとオンラインで始める(inbox も要る)。</summary>
        public static GameSimulation Create(GameContent content, IOnlineChannel? online, OnlineInbox? inbox)
        {
            var masterData = content.MasterData;
            var art = content.Art;
            Func<int, EnemyDefinition?> enemyLookup = id =>
            {
                var definition = masterData?.GetEnemy(id) ?? FallbackEnemies.Get(id);
                var enemyArt = definition != null ? art?.GetEnemy(definition.EnemyId) : null;
                if (definition != null && enemyArt != null)
                {
                    // 当たり判定の大きさを絵に合わせる
                    definition.Width = enemyArt.Width;
                    definition.Height = enemyArt.Height;
                }
                return definition;
            };
            IItemCatalog items = masterData ?? (IItemCatalog)new FallbackItems();
            var levels = masterData?.GetLevelTable() ?? LevelTable.Fallback;
            Func<int, string> itemName = id => items.Get(id)?.Name ?? $"item{id}";

            var playerConfig = PlayerConfig.Default;
            if (art?.Player != null)
            {
                playerConfig.Height = art.Player.Height;
                playerConfig.HalfWidth = art.Player.Width * 0.4f;
            }

            return new GameSimulation(
                content.StartMap, enemyLookup, itemName,
                playerConfig: playerConfig,
                mapLookup: content.Maps.Get,
                items: items,
                shops: new PlaceholderShopCatalog(items),
                online: online,
                inbox: inbox,
                levels: levels);
        }
    }
}
