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
    public void 問題の文字列表現にコードと対象が含まれる()
    {
        var issue = Assert.Single(MapData.Load(Fixtures.Broken("portal_target_self.json")).Validate());
        Assert.Equal("[PortalTargetSelf] Portal#1: 接続先が自分自身です (map 14 'loop')", issue.ToString());
    }

    private static WorldBounds Wide() => new WorldBounds { Left = -100, Right = 100, Top = 100, Bottom = -100 };
}
