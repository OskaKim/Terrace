using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Terrace.Shared;
using Terrace.TestClient;

Console.OutputEncoding = Encoding.UTF8;

var options = ClientOptions.Parse(args);
if (options.Error is not null)
{
    if (options.Error.Length > 0) Console.WriteLine($"引数エラー: {options.Error}");
    Console.WriteLine(ClientOptions.Usage);
    return options.Error.Length == 0 ? 0 : 2;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // 即終了せず、LeaveAsync してから終了する
    Log.Write("[ctrl+c] 退室します…");
    cts.Cancel();
};
if (options.DurationSeconds > 0)
{
    Log.Write($"[auto] {options.DurationSeconds} 秒後に自動で退室します");
    cts.CancelAfter(TimeSpan.FromSeconds(options.DurationSeconds));
}

// 1. 接続して、サーバー(ワールド)の情報を聞き、ログインする(Unary)
Log.Write($"[connect] server={options.Server}");
using var channel = GrpcChannel.ForAddress(options.Server);
LoginResult login;
try
{
    var account = MagicOnionClient.Create<IAccountService>(channel);
    var info = await account.GetServerInfoAsync();
    Log.Write($"[server] world={info.WorldName} ({info.WorldId}) protocol={info.ProtocolVersion} master={(info.MasterSha256.Length > 8 ? info.MasterSha256[..8] : info.MasterSha256)} password={(info.PasswordRequired ? "要る" : "要らない")} players={info.Players}");
    // マスタは持たないので sha256 は送らない(サーバーは比べない)
    login = await account.LoginAsync(new LoginRequest
    {
        Name = options.Name,
        Token = options.Token,
        Password = options.Password,
        ProtocolVersion = ProtocolVersion.Current,
    });
}
catch (RpcException ex) when (ex.StatusCode is StatusCode.FailedPrecondition or StatusCode.PermissionDenied)
{
    Log.Write($"[error] ログインを断られました: {ex.Status.Detail}");
    return 1;
}
catch (Exception ex)
{
    Log.Write($"[error] ログインできませんでした。サーバーは起動していますか? {ex.GetType().Name}: {ex.Message}");
    return 1;
}
Log.Write($"[login] name={options.Name} => playerId={login.PlayerId} character={login.Character.Name} lv={login.Character.Level} map={login.Character.MapId} ({login.Character.X:F1}, {login.Character.Y:F1}) token={login.Token}");

// 2. StreamingHub に接続して Join
var world = new WorldView();
var receiver = new LoggingReceiver(world);
var hub = await StreamingHubClient.ConnectAsync<IGameHub, IGameHubReceiver>(channel, receiver, cancellationToken: cts.Token);

var self = new PlayerInfo { PlayerId = login.PlayerId, Name = login.Character.Name };
world.Remember(self);
var position = new MoveState
{
    X = options.X ?? login.Character.X,
    Y = options.Y ?? login.Character.Y,
    Facing = Facing.Right,
    Motion = MotionState.Stand,
};
try
{
    await hub.JoinAsync(new JoinRequest { MapId = options.MapId, Token = login.Token, State = position });
}
catch (RpcException ex)
{
    Log.Write($"[error] 参加を断られました: {ex.Status.Detail}");
    await hub.DisposeAsync();
    return 1;
}
Log.Write($"[join] map={options.MapId} as {self} at ({position.X:F1}, {position.Y:F1})");

if (options.Chat.Length > 0)
{
    hub.SendChat(new SendChatRequest { Text = options.Chat });
    Log.Write($"[send] Chat {options.Chat}");
}

var leaving = false;
_ = hub.WaitForDisconnect().ContinueWith(_ =>
{
    if (leaving) return;
    Log.Write("[disconnect] サーバーとの接続が切れました");
    cts.Cancel();
}, TaskScheduler.Default);

// 3. 送信ループ

try
{
    if (options.Mode == ClientMode.Manual)
    {
        await ManualMode.RunAsync(hub, world, position, cts.Token);
    }
    else
    {
        await PatrolMode.RunAsync(hub, world, position, options.IntervalMs, options.Attack, cts.Token);
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C、--duration 経過、または切断
}

// 4. 退室して正常終了
leaving = true;
try
{
    await hub.LeaveAsync();
    Log.Write("[leave] ok");
}
catch (Exception ex)
{
    Log.Write($"[leave] 送れませんでした: {ex.GetType().Name}: {ex.Message}");
}

await hub.DisposeAsync();
Log.Write("[exit] bye");
return 0;
