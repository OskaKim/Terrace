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
    public void テーマとポータル種類とNPCと飾りを保存して読める()
    {
        var map = new MapData
        {
            Id = 100,
            Name = "Town",
            Theme = "stone",
            Bounds = new WorldBounds { Left = -5, Right = 60, Top = 30, Bottom = -5 },
            Footholds = new List<Foothold> { new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 50, Y2 = 0 } },
            Portals = new List<Portal>
            {
                new Portal { Id = 1, Name = "spawn", X = 25, Y = 0, Kind = PortalKind.Spawn },
                new Portal { Id = 2, Name = "west", X = 2, Y = 0, TargetMapId = 1, TargetPortalName = "east" },
            },
            Npcs = new List<Npc> { new Npc { Id = 1, Name = "メリー", X = 10, Y = 0, Kind = Npc.KindShop, ShopId = "general", Greeting = "いらっしゃい", Sprite = "alienPink" } },
            Decorations = new List<Decoration> { new Decoration { Id = 1, Sprite = "Tiles/houseBeige", X = 12, Y = 0, Layer = -20, Scale = 2f, FlipX = true } },
        };
        var path = Fixtures.TempFile("town.json");

        map.Save(path);
        var json = File.ReadAllText(path);
        var loaded = MapData.Load(path);

        Assert.Contains("\"kind\": \"Spawn\"", json);
        Assert.Contains("\"theme\": \"stone\"", json);
        Assert.Equal("stone", loaded.Theme);
        Assert.Equal(PortalKind.Spawn, loaded.FindPortalByName("spawn")!.Kind);
        Assert.Equal(PortalKind.Portal, loaded.FindPortalByName("west")!.Kind);
        var npc = Assert.Single(loaded.Npcs);
        Assert.Equal(("メリー", "shop", "general", "いらっしゃい", "alienPink"), (npc.Name, npc.Kind, npc.ShopId, npc.Greeting, npc.Sprite));
        var decoration = Assert.Single(loaded.Decorations);
        Assert.Equal(("Tiles/houseBeige", -20, 2f, true), (decoration.Sprite, decoration.Layer, decoration.Scale, decoration.FlipX));
        Assert.Empty(loaded.Validate());
    }

    [Fact]
    public void 種類を省いたポータルは通常のポータルになる()
    {
        var map = MapSerializer.FromJson("""{ "id": 1, "bounds": { "left": 0, "right": 10, "top": 10, "bottom": 0 }, "portals": [ { "id": 1, "name": "a", "x": 1, "y": 0, "targetMapId": 2, "targetPortalName": "b" } ] }""");

        Assert.Equal(PortalKind.Portal, map.Portals[0].Kind);
        Assert.Equal("grass", map.Theme);
        Assert.Empty(map.Npcs);
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
