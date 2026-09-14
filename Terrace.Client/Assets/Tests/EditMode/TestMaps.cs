using System;
using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>テスト用のマップと敵定義。JSON に依存せずコードで組み立てる。</summary>
    internal static class TestMaps
    {
        /// <summary>Terrace.Map の samples/sample_map.json と同じ内容。</summary>
        public static MapData Sample()
        {
            return new MapData
            {
                Id = 1,
                Name = "Sample Field",
                Bounds = new WorldBounds { Left = -10, Right = 60, Top = 30, Bottom = -5 },
                Footholds = new List<Foothold>
                {
                    new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, PrevId = 0, NextId = 2, Layer = 0 },
                    new Foothold { Id = 2, X1 = 10, Y1 = 0, X2 = 20, Y2 = 5, PrevId = 1, NextId = 3, Layer = 0 },
                    new Foothold { Id = 3, X1 = 20, Y1 = 5, X2 = 30, Y2 = 5, PrevId = 2, NextId = 0, Layer = 0 },
                    new Foothold { Id = 4, X1 = 30, Y1 = 10, X2 = 40, Y2 = 10, PrevId = 0, NextId = 5, Layer = 0 },
                    new Foothold { Id = 5, X1 = 40, Y1 = 10, X2 = 50, Y2 = 10, PrevId = 4, NextId = 0, Layer = 0 },
                    new Foothold { Id = 6, X1 = 15, Y1 = 12, X2 = 25, Y2 = 12, PrevId = 0, NextId = 0, Layer = 1 },
                },
                Ladders = new List<Ladder>
                {
                    new Ladder { Id = 1, X = 30, Y1 = 5, Y2 = 10, IsRope = false },
                },
                Portals = new List<Portal>
                {
                    new Portal { Id = 1, Name = "spawn", X = 2, Y = 0, TargetMapId = 2, TargetPortalName = "west" },
                    new Portal { Id = 2, Name = "east", X = 48, Y = 10, TargetMapId = 3, TargetPortalName = "west" },
                },
                SpawnPoints = new List<SpawnPoint>
                {
                    new SpawnPoint { Id = 1, X = 25, Y = 5, EnemyId = 1, RespawnSeconds = 10 },
                    new SpawnPoint { Id = 2, X = 45, Y = 10, EnemyId = 2, RespawnSeconds = 30 },
                },
            };
        }

        /// <summary>同じマップ内で行き来するポータルを 2 つ持つ平らなマップ。</summary>
        public static MapData FlatWithPortals()
        {
            return new MapData
            {
                Id = 7,
                Name = "Flat",
                Bounds = new WorldBounds { Left = -5, Right = 45, Top = 20, Bottom = -5 },
                Footholds = new List<Foothold>
                {
                    new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 40, Y2 = 0 },
                },
                Portals = new List<Portal>
                {
                    new Portal { Id = 1, Name = "spawn", X = 2, Y = 0, TargetMapId = 7, TargetPortalName = "far" },
                    new Portal { Id = 2, Name = "far", X = 30, Y = 0, TargetMapId = 7, TargetPortalName = "spawn" },
                },
            };
        }

        public static EnemyDefinition? Lookup(int enemyId)
        {
            switch (enemyId)
            {
                case 1: return new EnemyDefinition { EnemyId = 1, Name = "Slime", MaxHp = 10, Attack = 1, DropItemIds = new[] { 1, 4 }, DropRate = 1f };
                case 2: return new EnemyDefinition { EnemyId = 2, Name = "Goblin", MaxHp = 30, Attack = 5, DropItemIds = new[] { 2, 3, 4 }, DropRate = 1f };
                default: return null;
            }
        }

        public static string ItemName(int itemId)
        {
            switch (itemId)
            {
                case 1: return "Potion";
                case 2: return "Sword";
                case 3: return "Shield";
                case 4: return "Herb";
                default: return $"item{itemId}";
            }
        }

        public static GameSimulation NewSimulation(MapData? map = null, int seed = 1)
            => new GameSimulation(map ?? Sample(), Lookup, ItemName, random: new Random(seed));
    }
}
