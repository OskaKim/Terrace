using Terrace.Server.Accounts;
using Terrace.Server.Hosting;
using Terrace.Server.Saves;

namespace Terrace.Server.Tests;

public sealed class WorldSaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "terrace-world-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void 無ければ新しいワールドを作ってすぐ書く()
    {
        var save = new WorldSave(_directory, new AccountRegistry());

        var world = save.LoadOrCreate("テラスの村");

        Assert.Equal("テラスの村", world.Name);
        Assert.True(File.Exists(save.FilePath));
        var file = save.Read()!;
        Assert.Equal(world.Id, file.WorldId);
        Assert.Equal(WorldSaveFile.CurrentVersion, file.Version);
        Assert.Empty(file.Accounts);
    }

    [Fact]
    public void 書いて読み直すと同じワールドの同じキャラクターにトークンで戻れる()
    {
        var accounts = new AccountRegistry();
        var save = new WorldSave(_directory, accounts);
        var world = save.LoadOrCreate("テラスの村");
        var alice = accounts.Login("alice", null);
        accounts.Login("bob", null);
        save.Write(world);

        // 起動し直したつもり
        var restartedAccounts = new AccountRegistry();
        var restarted = new WorldSave(_directory, restartedAccounts).LoadOrCreate("別の名前");

        Assert.Equal(world.Id, restarted.Id);
        Assert.Equal("テラスの村", restarted.Name, StringComparer.Ordinal);
        Assert.Equal(2, restartedAccounts.Count);
        var back = restartedAccounts.Login("x", alice.Token);
        Assert.False(back.IsNew);
        Assert.Equal(alice.Account.PlayerId, back.Account.PlayerId);
        Assert.Equal("alice", back.Account.Name);
        Assert.Equal(3, restartedAccounts.Login("carol", null).Account.PlayerId);
    }

    [Fact]
    public void ファイルにトークンそのものは書かない()
    {
        var accounts = new AccountRegistry();
        var save = new WorldSave(_directory, accounts);
        var world = save.LoadOrCreate("w");
        var alice = accounts.Login("alice", null);
        save.Write(world);

        var text = File.ReadAllText(save.FilePath);
        Assert.DoesNotContain(alice.Token, text);
        Assert.Contains(alice.Account.TokenSha256, text);
    }

    [Fact]
    public void 知らない版のファイルなら読まずに止める()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, WorldSave.FileName), $$"""{ "version": {{WorldSaveFile.CurrentVersion + 1}}, "worldId": "abc", "worldName": "w", "lastPlayerId": 0, "accounts": [] }""");
        var save = new WorldSave(_directory, new AccountRegistry());

        var error = Assert.Throws<InvalidDataException>(() => save.LoadOrCreate("w"));
        Assert.Contains("版", error.Message);
        Assert.Contains("\"worldId\": \"abc\"", File.ReadAllText(save.FilePath));   // 上書きして消していない
    }

    [Fact]
    public void 壊れたファイルなら読まずに止める()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, WorldSave.FileName), "{ 壊れている");

        Assert.Throws<InvalidDataException>(() => new WorldSave(_directory, new AccountRegistry()).LoadOrCreate("w"));
    }

    [Fact]
    public void 書きかけのファイルが残っていても前のファイルを読む()
    {
        var accounts = new AccountRegistry();
        var save = new WorldSave(_directory, accounts);
        var world = save.LoadOrCreate("w");
        accounts.Login("alice", null);
        save.Write(world);
        File.WriteAllText(save.FilePath + ".tmp", "{ 書いている途中で落ちた");

        var restartedAccounts = new AccountRegistry();
        var restarted = new WorldSave(_directory, restartedAccounts).LoadOrCreate("w");

        Assert.Equal(world.Id, restarted.Id);
        Assert.Equal(1, restartedAccounts.Count);
    }

    [Fact]
    public void 親のプロセスが動いているかを見分ける()
    {
        Assert.True(ParentProcessWatcher.IsAlive(Environment.ProcessId));
        Assert.False(ParentProcessWatcher.IsAlive(0));
        Assert.False(ParentProcessWatcher.IsAlive(-1));
        Assert.False(ParentProcessWatcher.IsAlive(int.MaxValue));
    }
}
