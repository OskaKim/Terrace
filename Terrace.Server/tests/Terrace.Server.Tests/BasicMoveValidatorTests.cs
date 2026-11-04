using Terrace.Map;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class BasicMoveValidatorTests
{
    private static readonly BasicMoveValidator Validator = new();

    /// <summary>x -5..45、y -5..20 のマップ。</summary>
    private static MapData Map() => new()
    {
        Id = 1,
        Bounds = new WorldBounds { Left = -5, Right = 45, Top = 20, Bottom = -5 },
    };

    private static MoveState At(float x, float y) => new() { X = x, Y = y, Facing = Facing.Right, Motion = MotionState.Walk };

    [Fact]
    public void ふつうの移動は通す()
    {
        Assert.True(Validator.IsAcceptable(Map(), null, At(10f, 0f)));
        Assert.True(Validator.IsAcceptable(Map(), null, new MoveState { X = 1f, Y = 2f, VelocityX = -3f, VelocityY = -12f, Facing = Facing.Left, Motion = MotionState.Jump }));
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(0f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(0f, float.NegativeInfinity)]
    public void NaNや無限大の座標は断る(float x, float y)
    {
        Assert.False(Validator.IsAcceptable(Map(), null, At(x, y)));
        Assert.False(Validator.IsAcceptable(null, null, At(x, y)), "マップの無いルームでも断る");
    }

    [Fact]
    public void NaNや無限大の速度は断る()
    {
        var nanVelocity = At(0f, 0f);
        nanVelocity.VelocityX = float.NaN;
        var infiniteVelocity = At(0f, 0f);
        infiniteVelocity.VelocityY = float.PositiveInfinity;

        Assert.False(Validator.IsAcceptable(Map(), null, nanVelocity));
        Assert.False(Validator.IsAcceptable(Map(), null, infiniteVelocity));
    }

    [Fact]
    public void 知らない向きや動作は断る()
    {
        var badFacing = At(0f, 0f);
        badFacing.Facing = (Facing)0;
        var badMotion = At(0f, 0f);
        badMotion.Motion = (MotionState)99;

        Assert.False(Validator.IsAcceptable(Map(), null, badFacing));
        Assert.False(Validator.IsAcceptable(Map(), null, badMotion));
    }

    [Fact]
    public void 境界の外でも余白の内なら通し大きく外れたら断る()
    {
        var map = Map();
        var margin = BasicMoveValidator.BoundsMargin;

        // 落ちている途中(下端より下)や、境界のすぐ外
        Assert.True(Validator.IsAcceptable(map, null, At(10f, -5f - margin + 0.1f)));
        Assert.True(Validator.IsAcceptable(map, null, At(45f + margin - 0.1f, 0f)));

        Assert.False(Validator.IsAcceptable(map, null, At(10f, -5f - margin - 0.1f)));
        Assert.False(Validator.IsAcceptable(map, null, At(-5f - margin - 0.1f, 0f)));
        Assert.False(Validator.IsAcceptable(map, null, At(10f, 20f + margin + 0.1f)));
        Assert.False(Validator.IsAcceptable(map, null, At(1e9f, 0f)));
    }

    [Fact]
    public void マップが無いか境界が正しくなければ境界は見ない()
    {
        Assert.True(Validator.IsAcceptable(null, null, At(1e9f, -1e9f)));
        var broken = new MapData { Id = 2, Bounds = new WorldBounds { Left = 10, Right = 0, Top = 0, Bottom = 10 } };
        Assert.True(Validator.IsAcceptable(broken, null, At(1e9f, 0f)));
    }

    [Fact]
    public void 断った移動はルームに保持せず他の人へも届けない()
    {
        var sink = new RecordingEventSink();
        var room = new Room(1, Map(), Array.Empty<EnemySpawnConfig>(), Validator, sink);
        room.Join(new PlayerInfo { PlayerId = 1, Name = "alice" }, At(0f, 0f));

        Assert.Equal(MoveResult.Rejected, room.Move(1, At(float.NaN, 0f)));
        Assert.Equal(MoveResult.Rejected, room.Move(1, At(1e9f, 0f)));
        Assert.Empty(sink.Moves);
        Assert.Equal(0f, room.FindPlayer(1)!.State.X);

        Assert.Equal(MoveResult.Accepted, room.Move(1, At(3f, 0f)));
        Assert.Single(sink.Moves);
    }

    [Fact]
    public void 参加してよい位置かをマップごとに確かめる()
    {
        var map = Map();
        var manager = new RoomManager(_ => Array.Empty<EnemySpawnConfig>(), id => id == 1 ? map : null, Validator, _ => new RecordingEventSink());

        Assert.True(manager.CanJoinAt(1, At(0f, 0f)));
        Assert.False(manager.CanJoinAt(1, At(float.NaN, 0f)));
        Assert.False(manager.CanJoinAt(1, At(1e9f, 0f)));
        Assert.True(manager.CanJoinAt(2, At(1e9f, 0f)), "マップの無いルームでは境界を見ない");
    }
}
