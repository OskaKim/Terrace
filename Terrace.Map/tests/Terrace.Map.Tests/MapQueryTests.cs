using Terrace.Map;

namespace Terrace.Map.Tests;

public class MapQueryTests
{
    private readonly MapData _map = Fixtures.LoadSample();

    private Foothold Foothold(int id) => _map.FindFoothold(id) ?? throw new Xunit.Sdk.XunitException($"Foothold#{id} がありません");

    [Fact]
    public void 平坦な床の上でFindFootholdBelowが真下の足場を返す()
    {
        Assert.Equal(1, _map.FindFootholdBelow(5f, 3f)!.Id);
        // 足場の上にちょうど立っている点も拾う
        Assert.Equal(1, _map.FindFootholdBelow(5f, 0f)!.Id);
        // 両端を含む
        Assert.Equal(1, _map.FindFootholdBelow(0f, 1f)!.Id);
    }

    [Fact]
    public void 足場より下やX範囲外ではFindFootholdBelowはnull()
    {
        Assert.Null(_map.FindFootholdBelow(5f, -1f));
        Assert.Null(_map.FindFootholdBelow(-5f, 3f));
        Assert.Null(_map.FindFootholdBelow(55f, 3f));
    }

    [Fact]
    public void 斜面上のGetYAtが線形補間で正しい値を返す()
    {
        var slope = Foothold(2); // (10,0)-(20,5)

        Assert.Equal(0f, _map.GetYAt(slope, 10f));
        Assert.Equal(2.5f, _map.GetYAt(slope, 15f));
        Assert.Equal(1.25f, _map.GetYAt(slope, 12.5f));
        Assert.Equal(5f, _map.GetYAt(slope, 20f));
        // 範囲外は近い方の端
        Assert.Equal(0f, slope.GetYAt(5f));
        Assert.Equal(5f, slope.GetYAt(25f));
        Assert.Equal(0.5f, slope.Slope);
    }

    [Fact]
    public void 斜面の上に立つ点はその斜面を真下として返す()
    {
        var below = _map.FindFootholdBelow(15f, 2.5f);
        Assert.Equal(2, below!.Id);
        Assert.Equal(2, _map.FindFootholdBelow(15f, 8f)!.Id);
    }

    [Fact]
    public void IsWithinXは両端を含む()
    {
        var slope = Foothold(2);
        Assert.True(_map.IsWithinX(slope, 10f));
        Assert.True(_map.IsWithinX(slope, 20f));
        Assert.True(_map.IsWithinX(slope, 15f));
        Assert.False(_map.IsWithinX(slope, 9.99f));
        Assert.False(_map.IsWithinX(slope, 20.01f));
    }

    [Fact]
    public void チェーンの端でGetNextがnullを返しIsEdgeがtrueになる()
    {
        var first = Foothold(1);
        var last = Foothold(3);

        Assert.Null(_map.GetNext(first, Direction.Left));
        Assert.True(_map.IsEdge(first, Direction.Left));
        Assert.Null(_map.GetNext(last, Direction.Right));
        Assert.True(_map.IsEdge(last, Direction.Right));
    }

    [Fact]
    public void チェーンの途中ではGetNextが繋がる足場を返す()
    {
        var first = Foothold(1);
        var slope = Foothold(2);
        var last = Foothold(3);

        Assert.Same(slope, _map.GetNext(first, Direction.Right));
        Assert.Same(first, _map.GetNext(slope, Direction.Left));
        Assert.Same(last, _map.GetNext(slope, Direction.Right));
        Assert.Same(slope, _map.GetNext(last, Direction.Left));
        Assert.False(_map.IsEdge(slope, Direction.Left));
        Assert.False(_map.IsEdge(slope, Direction.Right));
    }

    [Fact]
    public void 逆向きに定義された線分でも方向はジオメトリで解決する()
    {
        // 右から左へ定義された 2 本のチェーン: A(20,0)->(10,0), B(10,0)->(0,0)
        var a = new Foothold { Id = 1, X1 = 20, Y1 = 0, X2 = 10, Y2 = 0, PrevId = 0, NextId = 2 };
        var b = new Foothold { Id = 2, X1 = 10, Y1 = 0, X2 = 0, Y2 = 0, PrevId = 1, NextId = 0 };
        var map = new MapData
        {
            Bounds = new WorldBounds { Left = -10, Right = 30, Top = 10, Bottom = -10 },
            Footholds = new List<Foothold> { a, b },
        };

        Assert.Empty(map.Validate());
        Assert.Same(b, map.GetNext(a, Direction.Left));
        Assert.Same(a, map.GetNext(b, Direction.Right));
        Assert.True(map.IsEdge(a, Direction.Right));
        Assert.True(map.IsEdge(b, Direction.Left));
        Assert.Equal(new Position(20, 0), a.GetEnd(Direction.Right));
        Assert.Equal(new Position(10, 0), a.GetEnd(Direction.Left));
    }

    [Fact]
    public void 重なった複数レイヤーがあるとき真下にある方を返す()
    {
        // x=18 では斜面(#2, y=4)と上の足場(#6, y=12, layer 1)が重なっている
        Assert.Equal(6, _map.FindFootholdBelow(18f, 13f)!.Id);
        Assert.Equal(6, _map.FindFootholdBelow(18f, 20f)!.Id);
        Assert.Equal(2, _map.FindFootholdBelow(18f, 8f)!.Id);
        Assert.Equal(2, _map.FindFootholdBelow(18f, 11.9f)!.Id);
    }

    [Fact]
    public void レイヤーを指定するとそのレイヤーだけを対象にする()
    {
        Assert.Equal(2, _map.FindFootholdBelow(18f, 20f, layer: 0)!.Id);
        Assert.Equal(6, _map.FindFootholdBelow(18f, 20f, layer: 1)!.Id);
        Assert.Null(_map.FindFootholdBelow(18f, 8f, layer: 1));
    }

    [Fact]
    public void 同じ高さに複数あればLayerが小さい方を返す()
    {
        var lower = new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, Layer = 0 };
        var upper = new Foothold { Id = 2, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, Layer = 3 };
        var map = new MapData { Footholds = new List<Foothold> { upper, lower } };

        Assert.Same(lower, map.FindFootholdBelow(5f, 1f));
    }

    [Fact]
    public void 壁は真下検索の対象にしない()
    {
        var wall = new Foothold { Id = 1, X1 = 5, Y1 = 0, X2 = 5, Y2 = 10 };
        var map = new MapData { Footholds = new List<Foothold> { wall } };

        Assert.True(wall.IsVertical);
        Assert.Null(map.FindFootholdBelow(5f, 20f));
    }

    [Fact]
    public void FindLadderNearはつかまれるはしごを返す()
    {
        var ladder = _map.FindLadderNear(30.2f, 7f, 0.5f);
        Assert.NotNull(ladder);
        Assert.Equal(1, ladder!.Id);
        Assert.False(ladder.IsRope);

        // 区間の端は range 分の余裕を見る
        Assert.NotNull(_map.FindLadderNear(30f, 10.3f, 0.5f));
        Assert.NotNull(_map.FindLadderNear(30f, 4.7f, 0.5f));
    }

    [Fact]
    public void FindLadderNearは遠いはしごや区間外ではnull()
    {
        Assert.Null(_map.FindLadderNear(31f, 7f, 0.5f));
        Assert.Null(_map.FindLadderNear(30f, 12f, 0.5f));
        Assert.Null(_map.FindLadderNear(30f, 3f, 0.5f));
    }

    [Fact]
    public void FindLadderNearは複数あればXが最も近いものを返す()
    {
        var near = new Ladder { Id = 1, X = 10.1f, Y1 = 0, Y2 = 10 };
        var far = new Ladder { Id = 2, X = 9.5f, Y1 = 0, Y2 = 10 };
        var map = new MapData { Ladders = new List<Ladder> { far, near } };

        Assert.Same(near, map.FindLadderNear(10f, 5f, 1f));
    }

    [Fact]
    public void FindPortalNearは範囲内で最も近いポータルを返す()
    {
        Assert.Equal("spawn", _map.FindPortalNear(2.3f, 0.2f, 1f)!.Name);
        Assert.Equal("east", _map.FindPortalNear(47f, 10f, 1.5f)!.Name);
        Assert.Null(_map.FindPortalNear(5f, 0f, 1f));
        Assert.Equal("east", _map.FindPortalByName("east")!.Name);
        Assert.Null(_map.FindPortalByName("nope"));
    }

    [Fact]
    public void ClampToWorldは境界の内側へ丸める()
    {
        Assert.Equal(new Position(60, -5), _map.ClampToWorld(new Position(100, -50)));
        Assert.Equal(new Position(-10, 30), _map.ClampToWorld(new Position(-99, 99)));
        Assert.Equal(new Position(5, 5), _map.ClampToWorld(new Position(5, 5)));
    }

    [Fact]
    public void 足場を追加すると索引が追従する()
    {
        var map = new MapData();
        Assert.Null(map.FindFoothold(1));

        map.Footholds.Add(new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0 });
        Assert.NotNull(map.FindFoothold(1));

        map.Footholds.Clear();
        map.RebuildIndex();
        Assert.Null(map.FindFoothold(1));
    }
}
