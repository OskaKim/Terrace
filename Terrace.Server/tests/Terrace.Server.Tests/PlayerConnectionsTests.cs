using Terrace.Server.Accounts;

namespace Terrace.Server.Tests;

public class PlayerConnectionsTests
{
    private const int PlayerId = 7;

    [Fact]
    public void 最初の参加では置き換えが無く持ち主になる()
    {
        var connections = new PlayerConnections();
        var first = new object();
        int? replaced = -1;

        connections.Join(PlayerId, first, 1, previous => replaced = previous);

        Assert.Null(replaced);
        Assert.True(connections.IsOwner(PlayerId, first));
    }

    [Fact]
    public void 同じIDを別の接続が使うと先の接続のマップを渡して置き換え先の接続は持ち主でなくなる()
    {
        var connections = new PlayerConnections();
        var first = new object();
        var second = new object();
        connections.Join(PlayerId, first, 1, _ => { });

        int? replaced = null;
        connections.Join(PlayerId, second, 2, previous => replaced = previous);

        Assert.Equal(1, replaced);
        Assert.False(connections.IsOwner(PlayerId, first));
        Assert.True(connections.IsOwner(PlayerId, second));
    }

    [Fact]
    public void 置き換えられた接続の退出は後の接続の持ち主の印を消さず退出の処理も呼ばない()
    {
        var connections = new PlayerConnections();
        var first = new object();
        var second = new object();
        connections.Join(PlayerId, first, 1, _ => { });
        connections.Join(PlayerId, second, 1, _ => { });

        var called = false;
        var left = connections.Leave(PlayerId, first, () => called = true);

        Assert.False(left);
        Assert.False(called);
        Assert.True(connections.IsOwner(PlayerId, second));
    }

    [Fact]
    public void 持ち主の退出で退出の処理を呼んでIDが空き次の参加は置き換えにならない()
    {
        var connections = new PlayerConnections();
        var first = new object();
        connections.Join(PlayerId, first, 1, _ => { });

        var called = false;
        Assert.True(connections.Leave(PlayerId, first, () => called = true));

        Assert.True(called);
        Assert.False(connections.IsOwner(PlayerId, first));
        int? replaced = -1;
        connections.Join(PlayerId, new object(), 1, previous => replaced = previous);
        Assert.Null(replaced);
    }

    [Fact]
    public void 同じ接続が別のマップへ参加し直しても置き換えにはならない()
    {
        var connections = new PlayerConnections();
        var first = new object();
        connections.Join(PlayerId, first, 1, _ => { });

        int? replaced = -1;
        connections.Join(PlayerId, first, 2, previous => replaced = previous);

        Assert.Null(replaced);
        Assert.True(connections.IsOwner(PlayerId, first));
    }
}
