using System;
using System.Collections.Generic;

namespace Terrace.Map
{
    public enum MapValidationCode
    {
        /// <summary>ワールド境界が Left &lt; Right, Bottom &lt; Top を満たしていない。</summary>
        InvalidWorldBounds,
        /// <summary>同じ種類のオブジェクトに同じ Id が複数ある。</summary>
        DuplicateId,
        /// <summary>同じ名前のポータルが複数ある。</summary>
        DuplicatePortalName,
        /// <summary>PrevId / NextId が自分自身を指している。</summary>
        FootholdSelfLink,
        /// <summary>PrevId / NextId が存在しない Id を指している。</summary>
        FootholdLinkTargetMissing,
        /// <summary>A の Next が B なのに B の Prev が A でない(またはその逆)。</summary>
        FootholdLinkNotMutual,
        /// <summary>繋がっている相手の端点と離れすぎている。</summary>
        FootholdEndpointTooFar,
        /// <summary>ポータルの接続先(TargetMapId / TargetPortalName)が空。</summary>
        PortalTargetEmpty,
        /// <summary>ポータルの接続先が自分自身。</summary>
        PortalTargetSelf,
        /// <summary>ワールド境界の外にある。</summary>
        OutOfWorldBounds,
    }

    /// <summary>Validate が見つけた問題 1 件。</summary>
    public sealed class MapValidationIssue
    {
        public MapValidationIssue(MapValidationCode code, string kind, int objectId, string message)
        {
            Code = code;
            Kind = kind;
            ObjectId = objectId;
            Message = message;
        }

        public MapValidationCode Code { get; }

        /// <summary>"Map" / "Foothold" / "Ladder" / "Portal" / "SpawnPoint"</summary>
        public string Kind { get; }

        public int ObjectId { get; }
        public string Message { get; }

        public override string ToString() => $"[{Code}] {Kind}#{ObjectId}: {Message}";
    }

    internal static class MapValidator
    {
        public const float DefaultTolerance = 0.01f;

        public static IReadOnlyList<MapValidationIssue> Validate(MapData map, float tolerance)
        {
            var issues = new List<MapValidationIssue>();
            var bounds = map.Bounds;
            var boundsOk = bounds != null && bounds.IsValid;
            if (!boundsOk)
            {
                issues.Add(new MapValidationIssue(MapValidationCode.InvalidWorldBounds, "Map", map.Id,
                    "ワールド境界が不正です (Left < Right かつ Bottom < Top が必要)"));
            }

            ValidateFootholds(map, issues, boundsOk ? bounds : null, tolerance);
            ValidateLadders(map, issues, boundsOk ? bounds : null);
            ValidatePortals(map, issues, boundsOk ? bounds : null);
            ValidateSpawnPoints(map, issues, boundsOk ? bounds : null);

            return issues;
        }

        private static void ValidateFootholds(MapData map, List<MapValidationIssue> issues, WorldBounds? bounds, float tolerance)
        {
            var byId = new Dictionary<int, Foothold>();
            foreach (var foothold in map.Footholds)
            {
                if (byId.ContainsKey(foothold.Id))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.DuplicateId, "Foothold", foothold.Id, "Id が重複しています"));
                    continue;
                }
                byId.Add(foothold.Id, foothold);
            }

            foreach (var foothold in map.Footholds)
            {
                CheckLink(issues, foothold, byId, tolerance, isNext: false);
                CheckLink(issues, foothold, byId, tolerance, isNext: true);

                if (bounds != null && (!bounds.Contains(foothold.Start) || !bounds.Contains(foothold.End)))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.OutOfWorldBounds, "Foothold", foothold.Id,
                        $"ワールド境界の外にあります ({foothold.X1}, {foothold.Y1})-({foothold.X2}, {foothold.Y2})"));
                }
            }
        }

        private static void CheckLink(List<MapValidationIssue> issues, Foothold foothold, Dictionary<int, Foothold> byId, float tolerance, bool isNext)
        {
            var field = isNext ? "NextId" : "PrevId";
            var targetId = isNext ? foothold.NextId : foothold.PrevId;
            if (targetId == 0) return;

            if (targetId == foothold.Id)
            {
                issues.Add(new MapValidationIssue(MapValidationCode.FootholdSelfLink, "Foothold", foothold.Id,
                    $"{field} が自分自身を指しています"));
                return;
            }

            if (!byId.TryGetValue(targetId, out var target))
            {
                issues.Add(new MapValidationIssue(MapValidationCode.FootholdLinkTargetMissing, "Foothold", foothold.Id,
                    $"{field} = {targetId} は存在しない Foothold です"));
                return;
            }

            var backField = isNext ? "PrevId" : "NextId";
            var backId = isNext ? target.PrevId : target.NextId;
            var mutual = backId == foothold.Id;
            if (!mutual)
            {
                issues.Add(new MapValidationIssue(MapValidationCode.FootholdLinkNotMutual, "Foothold", foothold.Id,
                    $"{field} = {targetId} ですが、Foothold#{targetId} の {backField} は {backId} です"));
            }

            // 端点の距離。相互に繋がっている場合は Next 側だけで 1 回報告する
            if (isNext || !mutual)
            {
                var mine = isNext ? foothold.End : foothold.Start;
                var theirs = isNext ? target.Start : target.End;
                var distance = mine.DistanceTo(theirs);
                if (distance > tolerance)
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.FootholdEndpointTooFar, "Foothold", foothold.Id,
                        $"{field} = {targetId} の端点 {theirs} と自身の端点 {mine} が {distance} 離れています (許容 {tolerance})"));
                }
            }
        }

        private static void ValidateLadders(MapData map, List<MapValidationIssue> issues, WorldBounds? bounds)
        {
            var seen = new HashSet<int>();
            foreach (var ladder in map.Ladders)
            {
                if (!seen.Add(ladder.Id))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.DuplicateId, "Ladder", ladder.Id, "Id が重複しています"));
                }
                if (bounds != null && (!bounds.Contains(ladder.X, ladder.Y1) || !bounds.Contains(ladder.X, ladder.Y2)))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.OutOfWorldBounds, "Ladder", ladder.Id,
                        $"ワールド境界の外にあります x={ladder.X} y={ladder.Y1}..{ladder.Y2}"));
                }
            }
        }

        private static void ValidatePortals(MapData map, List<MapValidationIssue> issues, WorldBounds? bounds)
        {
            var seenIds = new HashSet<int>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var portal in map.Portals)
            {
                if (!seenIds.Add(portal.Id))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.DuplicateId, "Portal", portal.Id, "Id が重複しています"));
                }
                if (!string.IsNullOrEmpty(portal.Name) && !seenNames.Add(portal.Name))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.DuplicatePortalName, "Portal", portal.Id,
                        $"名前 '{portal.Name}' が重複しています"));
                }

                if (portal.TargetMapId <= 0 || string.IsNullOrWhiteSpace(portal.TargetPortalName))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.PortalTargetEmpty, "Portal", portal.Id,
                        $"接続先が空です (TargetMapId = {portal.TargetMapId}, TargetPortalName = '{portal.TargetPortalName}')"));
                }
                else if (portal.TargetMapId == map.Id && string.Equals(portal.TargetPortalName, portal.Name, StringComparison.Ordinal))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.PortalTargetSelf, "Portal", portal.Id,
                        $"接続先が自分自身です (map {map.Id} '{portal.Name}')"));
                }

                if (bounds != null && !bounds.Contains(portal.X, portal.Y))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.OutOfWorldBounds, "Portal", portal.Id,
                        $"ワールド境界の外にあります ({portal.X}, {portal.Y})"));
                }
            }
        }

        private static void ValidateSpawnPoints(MapData map, List<MapValidationIssue> issues, WorldBounds? bounds)
        {
            var seen = new HashSet<int>();
            foreach (var spawnPoint in map.SpawnPoints)
            {
                if (!seen.Add(spawnPoint.Id))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.DuplicateId, "SpawnPoint", spawnPoint.Id, "Id が重複しています"));
                }
                if (bounds != null && !bounds.Contains(spawnPoint.X, spawnPoint.Y))
                {
                    issues.Add(new MapValidationIssue(MapValidationCode.OutOfWorldBounds, "SpawnPoint", spawnPoint.Id,
                        $"ワールド境界の外にあります ({spawnPoint.X}, {spawnPoint.Y})"));
                }
            }
        }
    }
}
