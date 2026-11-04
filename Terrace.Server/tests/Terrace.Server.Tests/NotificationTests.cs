using System.Reflection;
using MessagePack;
using Terrace.Shared;

namespace Terrace.Server.Tests;

/// <summary>通知(<see cref="INotification"/>)の Union の登録と、MessagePack での往復。</summary>
public class NotificationTests
{
    private static readonly UnionAttribute[] Unions = typeof(INotification).GetCustomAttributes<UnionAttribute>().ToArray();

    [Fact]
    public void INotificationを実装する型はすべてUnionに登録してある()
    {
        var implementations = typeof(INotification).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(INotification).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToArray();
        var registered = Unions.Select(u => u.SubType).OrderBy(t => t.Name).ToArray();

        Assert.Equal(implementations, registered);
    }

    [Fact]
    public void Unionの番号は重ならず0から詰めて並ぶ()
    {
        var keys = Unions.Select(u => u.Key).OrderBy(k => k).ToArray();

        Assert.Equal(Enumerable.Range(0, keys.Length).ToArray(), keys);
    }

    [Fact]
    public void INotificationとして書いて読むと同じ型と値に戻る()
    {
        var killed = RoundTrip(new EnemyKilledNotification { EnemyInstanceId = 7, KillerPlayerId = 2, DroppedItemIds = new[] { 1, 4 } });
        var k = Assert.IsType<EnemyKilledNotification>(killed);
        Assert.Equal(7, k.EnemyInstanceId);
        Assert.Equal(2, k.KillerPlayerId);
        Assert.Equal(new[] { 1, 4 }, k.DroppedItemIds);

        var joined = Assert.IsType<PlayerJoinedNotification>(RoundTrip(new PlayerJoinedNotification
        {
            Player = new PlayerInfo { PlayerId = 3, Name = "bob" },
            State = new MoveState { X = 1.5f, Motion = MotionState.Walk },
        }));
        Assert.Equal("bob", joined.Player.Name);
        Assert.Equal(1.5f, joined.State.X);
        Assert.Equal(MotionState.Walk, joined.State.Motion);

        var snapshot = Assert.IsType<RoomSnapshot>(RoundTrip(new RoomSnapshot
        {
            MapId = 2,
            Enemies = new[] { new EnemyState { InstanceId = 1, Hp = 30 } },
        }));
        Assert.Equal(2, snapshot.MapId);
        Assert.Equal(30, Assert.Single(snapshot.Enemies).Hp);
    }

    [Fact]
    public void どの通知も既定の値のままINotificationとして書いて読める()
    {
        foreach (var union in Unions)
        {
            var notification = (INotification)Activator.CreateInstance(union.SubType)!;
            Assert.IsType(union.SubType, RoundTrip(notification));
        }
    }

    private static INotification RoundTrip(INotification notification)
        => MessagePackSerializer.Deserialize<INotification>(MessagePackSerializer.Serialize(notification));
}
