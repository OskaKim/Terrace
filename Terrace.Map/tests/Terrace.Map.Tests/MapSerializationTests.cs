using Terrace.Map;

namespace Terrace.Map.Tests;

public class MapSerializationTests
{
    [Fact]
    public void サンプルマップを読み込める()
    {
        var map = Fixtures.LoadSample();

        Assert.Equal(1, map.Id);
        Assert.Equal("Sample Field", map.Name);
        Assert.Equal(6, map.Footholds.Count);
        Assert.Single(map.Ladders);
        Assert.Equal(2, map.Portals.Count);
        Assert.Equal(2, map.SpawnPoints.Count);
        Assert.Equal(-10f, map.Bounds.Left);
        Assert.Equal(60f, map.Bounds.Right);
        Assert.Equal(30f, map.Bounds.Top);
        Assert.Equal(-5f, map.Bounds.Bottom);

        var slope = map.FindFoothold(2)!;
        Assert.Equal((10f, 0f, 20f, 5f), (slope.X1, slope.Y1, slope.X2, slope.Y2));
        Assert.Equal(1, slope.PrevId);
        Assert.Equal(3, slope.NextId);
        Assert.Equal(1, map.FindFoothold(6)!.Layer);
        Assert.Equal(10f, map.SpawnPoints[0].RespawnSeconds);
        Assert.Equal("west", map.Portals[0].TargetPortalName);
    }

    [Fact]
    public void サンプルマップは検証を通る()
    {
        var issues = Fixtures.LoadSample().Validate();
        Assert.Empty(issues);
    }

    [Fact]
    public void SaveしてLoadすると同じ内容になる()
    {
        var original = Fixtures.LoadSample();
        var path = Fixtures.TempFile("roundtrip.json");

        original.Save(path);
        var loaded = MapData.Load(path);

        Assert.Equal(MapSerializer.ToJson(original), MapSerializer.ToJson(loaded));
        Assert.Equal(original.Footholds.Count, loaded.Footholds.Count);
        Assert.Equal(original.FindFoothold(3)!.NextId, loaded.FindFoothold(3)!.NextId);
        Assert.Equal(original.Ladders[0].IsRope, loaded.Ladders[0].IsRope);
    }

    [Fact]
    public void 計算プロパティはJSONに書き出さない()
    {
        var json = MapSerializer.ToJson(Fixtures.LoadSample());

        Assert.Contains("\"prevId\"", json);
        Assert.Contains("\"nextId\"", json);
        Assert.DoesNotContain("\"isVertical\"", json);
        Assert.DoesNotContain("\"slope\"", json);
        Assert.DoesNotContain("\"position\"", json);
        Assert.DoesNotContain("\"isValid\"", json);
    }

    [Fact]
    public void 編集して保存した内容が反映される()
    {
        var map = Fixtures.LoadSample();
        map.Name = "Edited";
        map.Footholds.Add(new Foothold { Id = 7, X1 = 50, Y1 = 10, X2 = 58, Y2 = 10, PrevId = 0, NextId = 0 });
        var path = Fixtures.TempFile("edited.json");

        map.Save(path);
        var loaded = MapData.Load(path);

        Assert.Equal("Edited", loaded.Name);
        Assert.Equal(7, loaded.Footholds.Count);
        Assert.NotNull(loaded.FindFoothold(7));
    }

    [Fact]
    public void プロパティ名の大文字小文字とコメントを許容する()
    {
        const string json = """
            {
              // コメント
              "Id": 5, "NAME": "loose",
              "bounds": { "Left": 0, "Right": 10, "Top": 10, "Bottom": 0 },
              "footholds": [ { "id": 1, "X1": 0, "Y1": 0, "X2": 10, "Y2": 0, }, ],
            }
            """;

        var map = MapSerializer.FromJson(json);

        Assert.Equal(5, map.Id);
        Assert.Equal("loose", map.Name);
        Assert.Single(map.Footholds);
        Assert.Equal(10f, map.Footholds[0].X2);
    }
}
