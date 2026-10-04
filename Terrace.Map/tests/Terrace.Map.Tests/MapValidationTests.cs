using Terrace.Map;

namespace Terrace.Map.Tests;

public class MapValidationTests
{
    [Theory]
    [InlineData("missing_link_target.json", MapValidationCode.FootholdLinkTargetMissing)]
    [InlineData("not_mutual.json", MapValidationCode.FootholdLinkNotMutual)]
    [InlineData("endpoint_gap.json", MapValidationCode.FootholdEndpointTooFar)]
    [InlineData("portal_target_empty.json", MapValidationCode.PortalTargetEmpty)]
    [InlineData("portal_target_self.json", MapValidationCode.PortalTargetSelf)]
    [InlineData("out_of_bounds.json", MapValidationCode.OutOfWorldBounds)]
    public void 壊れたマップJSONから不整合を検出する(string file, MapValidationCode expected)
    {
        var map = MapData.Load(Fixtures.Broken(file));

        var issues = map.Validate();

        Assert.Contains(issues, i => i.Code == expected);
    }

    [Fact]
    public void 存在しないIdへのリンクはその足場のIdで報告する()
    {
        var issues = MapData.Load(Fixtures.Broken("missing_link_target.json")).Validate();

        var issue = Assert.Single(issues);
        Assert.Equal(MapValidationCode.FootholdLinkTargetMissing, issue.Code);
        Assert.Equal("Foothold", issue.Kind);
        Assert.Equal(1, issue.ObjectId);
        Assert.Contains("NextId = 99", issue.Message);
    }

    [Fact]
    public void 相互に一致しないリンクは片側から報告する()
    {
        var issues = MapData.Load(Fixtures.Broken("not_mutual.json")).Validate();

        var issue = Assert.Single(issues);
        Assert.Equal(MapValidationCode.FootholdLinkNotMutual, issue.Code);
        Assert.Equal(1, issue.ObjectId);
        Assert.Contains("PrevId は 0", issue.Message);
    }

    [Fact]
    public void 端点の距離は許容誤差で判定する()
    {
        var issues = MapData.Load(Fixtures.Broken("endpoint_gap.json")).Validate();
        var gap = Assert.Single(issues, i => i.Code == MapValidationCode.FootholdEndpointTooFar);
        Assert.Equal(1, gap.ObjectId);

        // 許容誤差を広げれば通る
        var relaxed = MapData.Load(Fixtures.Broken("endpoint_gap.json")).Validate(tolerance: 10f);
        Assert.DoesNotContain(relaxed, i => i.Code == MapValidationCode.FootholdEndpointTooFar);

        // わずかなずれは既定の許容誤差内
        var a = new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, NextId = 2 };
        var b = new Foothold { Id = 2, X1 = 10.005f, Y1 = 0, X2 = 20, Y2 = 0, PrevId = 1 };
        var map = new MapData { Bounds = Wide(), Footholds = new List<Foothold> { a, b } };
        Assert.Empty(map.Validate());
        Assert.Contains(map.Validate(tolerance: 0.001f), i => i.Code == MapValidationCode.FootholdEndpointTooFar);
    }

    [Fact]
    public void 相互に繋がった端点のずれは1回だけ報告する()
    {
        var a = new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, NextId = 2 };
        var b = new Foothold { Id = 2, X1 = 13, Y1 = 0, X2 = 20, Y2 = 0, PrevId = 1 };
        var map = new MapData { Bounds = Wide(), Footholds = new List<Foothold> { a, b } };

        var issues = map.Validate();

        Assert.Single(issues, i => i.Code == MapValidationCode.FootholdEndpointTooFar);
    }

    [Fact]
    public void ポータルの接続先が空のものをすべて報告する()
    {
        var issues = MapData.Load(Fixtures.Broken("portal_target_empty.json")).Validate();

        Assert.Equal(2, issues.Count(i => i.Code == MapValidationCode.PortalTargetEmpty));
    }

    [Fact]
    public void 境界の外にあるオブジェクトを種類ごとに報告する()
    {
        var issues = MapData.Load(Fixtures.Broken("out_of_bounds.json")).Validate();

        var kinds = issues.Where(i => i.Code == MapValidationCode.OutOfWorldBounds).Select(i => i.Kind).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "Foothold", "Ladder", "Portal", "SpawnPoint" }, kinds);
    }

    [Fact]
    public void 自分自身へのリンクと重複Idを検出する()
    {
        var map = new MapData
        {
            Bounds = Wide(),
            Footholds = new List<Foothold>
            {
                new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, NextId = 1 },
                new Foothold { Id = 1, X1 = 10, Y1 = 0, X2 = 20, Y2 = 0 },
            },
            Portals = new List<Portal>
            {
                new Portal { Id = 1, Name = "a", TargetMapId = 2, TargetPortalName = "x" },
                new Portal { Id = 1, Name = "a", TargetMapId = 2, TargetPortalName = "x" },
            },
        };

        var issues = map.Validate();

        Assert.Contains(issues, i => i.Code == MapValidationCode.FootholdSelfLink && i.ObjectId == 1);
        Assert.Contains(issues, i => i.Code == MapValidationCode.DuplicateId && i.Kind == "Foothold");
        Assert.Contains(issues, i => i.Code == MapValidationCode.DuplicateId && i.Kind == "Portal");
        Assert.Contains(issues, i => i.Code == MapValidationCode.DuplicatePortalName);
    }

    [Fact]
    public void 不正なワールド境界を検出し境界チェックは行わない()
    {
        var map = new MapData
        {
            Bounds = new WorldBounds { Left = 10, Right = 0, Top = 0, Bottom = 10 },
            SpawnPoints = new List<SpawnPoint> { new SpawnPoint { Id = 1, X = 999, Y = 999 } },
        };

        var issues = map.Validate();

        Assert.Contains(issues, i => i.Code == MapValidationCode.InvalidWorldBounds);
        Assert.DoesNotContain(issues, i => i.Code == MapValidationCode.OutOfWorldBounds);
    }

    [Fact]
    public void 出現地点のポータルは接続先が無くても通り入れるポータルには数えない()
    {
        var spawn = new Portal { Id = 1, Name = "spawn", X = 5, Y = 0, Kind = PortalKind.Spawn };
        var gate = new Portal { Id = 2, Name = "east", X = 20, Y = 0, TargetMapId = 2, TargetPortalName = "west" };
        var map = new MapData
        {
            Id = 1,
            Bounds = Wide(),
            Footholds = new List<Foothold> { new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 30, Y2 = 0 } },
            Portals = new List<Portal> { spawn, gate },
        };

        Assert.Empty(map.Validate());
        Assert.Same(spawn, map.FindSpawnPortal());
        Assert.Null(map.FindPortalNear(5, 0, 1f)); // 出現地点には入れない
        Assert.Same(gate, map.FindPortalNear(20, 0, 1f));
    }

    [Fact]
    public void NPCの重複Idと境界外を検出する()
    {
        var map = new MapData
        {
            Id = 1,
            Bounds = new WorldBounds { Left = 0, Right = 10, Top = 10, Bottom = 0 },
            Npcs = new List<Npc>
            {
                new Npc { Id = 1, Name = "メリー", X = 2, Y = 0, Kind = Npc.KindShop, ShopId = "general" },
                new Npc { Id = 1, Name = "ラク", X = 4, Y = 0 },
                new Npc { Id = 2, Name = "遠い人", X = 50, Y = 0 },
            },
            Decorations = new List<Decoration>
            {
                new Decoration { Id = 1, Sprite = "Tiles/fence", X = 1, Y = 0 },
                new Decoration { Id = 1, Sprite = "Tiles/fence", X = 2, Y = 0 },
            },
        };

        var issues = map.Validate();

        Assert.Contains(issues, i => i.Code == MapValidationCode.DuplicateId && i.Kind == "Npc" && i.ObjectId == 1);
        Assert.Contains(issues, i => i.Code == MapValidationCode.OutOfWorldBounds && i.Kind == "Npc" && i.ObjectId == 2);
        Assert.Contains(issues, i => i.Code == MapValidationCode.DuplicateId && i.Kind == "Decoration");
        Assert.True(map.FindNpc(1)!.IsShop);
        Assert.Equal("メリー", map.FindNpcNear(2.5f, 0, 1f)!.Name);
    }

    [Theory]
    [InlineData("town\\home")]
    [InlineData("town.ogg")]
    [InlineData("bgm/town.ogg")]
    [InlineData("../town")]
    [InlineData("bgm/../town")]
    [InlineData("/town")]
    [InlineData("bgm//town")]
    [InlineData("town/")]
    public void 形の悪いBGMの指定を報告する(string bgm)
    {
        var map = new MapData { Id = 3, Bounds = Wide(), Bgm = bgm };

        var issue = Assert.Single(map.Validate());

        Assert.Equal(MapValidationCode.InvalidBgmPath, issue.Code);
        Assert.Equal("Map", issue.Kind);
        Assert.Equal(3, issue.ObjectId);
        Assert.Contains(bgm, issue.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("town1_home_town")]
    [InlineData("Town/home-town 2")]
    public void BGMの指定が空か区切りが正しい相対パスなら通る(string bgm)
    {
        var map = new MapData { Bounds = Wide(), Bgm = bgm };

        Assert.Empty(map.Validate());
    }

    [Fact]
    public void 遊び用のマップはすべて検証を通る()
    {
        var paths = Fixtures.PlayMaps();
        Assert.NotEmpty(paths);

        foreach (var path in paths)
        {
            var issues = MapData.Load(path).Validate();
            Assert.True(issues.Count == 0, $"{System.IO.Path.GetFileName(path)}: {string.Join("\n", issues)}");
        }
    }

    [Fact]
    public void 問題の文字列表現にコードと対象が含まれる()
    {
        var issue = Assert.Single(MapData.Load(Fixtures.Broken("portal_target_self.json")).Validate());
        Assert.Equal("[PortalTargetSelf] Portal#1: 接続先が自分自身です (map 14 'loop')", issue.ToString());
    }

    private static WorldBounds Wide() => new WorldBounds { Left = -100, Right = 100, Top = 100, Bottom = -100 };
}
