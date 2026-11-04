using MagicOnion.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Terrace.Server.Accounts;
using Terrace.Server.Chat;
using Terrace.Server.Configuration;
using Terrace.Server.Content;
using Terrace.Server.Hosting;
using Terrace.Server.Hubs;
using Terrace.Server.Rooms;
using Terrace.Server.Saves;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection(TerraceServerOptions.SectionName).Get<TerraceServerOptions>() ?? new TerraceServerOptions();

// Kestrel: TLS なしの HTTP/2 (h2c) で gRPC を受ける。Unity(YetAnotherHttpHandler)も TestClient も http:// で繋ぐ。
// 平文で HTTP/1.1 と HTTP/2 を同じポートに同居させることはできないため、確認用の HTTP/1.1 は別ポートに開く。
// 状態確認の口はプレイヤーの名前と位置が見えるので、gRPC を外へ開いても、StatusListenAnyIP でなければ手元だけで待つ
builder.WebHost.ConfigureKestrel(kestrel =>
{
    if (settings.ListenAnyIP) kestrel.ListenAnyIP(settings.GrpcPort, listen => listen.Protocols = HttpProtocols.Http2);
    else kestrel.ListenLocalhost(settings.GrpcPort, listen => listen.Protocols = HttpProtocols.Http2);

    if (settings.StatusListenAnyIP) kestrel.ListenAnyIP(settings.HttpPort, listen => listen.Protocols = HttpProtocols.Http1);
    else kestrel.ListenLocalhost(settings.HttpPort, listen => listen.Protocols = HttpProtocols.Http1);
});

// 内容物(マップとマスタ)を読む。マップが 1 枚も無ければダミーのスポーン設定で動かす。
var contentRoot = settings.ContentRoot ?? Path.Combine(AppContext.BaseDirectory, "content");
var content = ServerContent.Load(contentRoot);

// StreamingHub の heartbeat。クラッシュ・回線断・スリープで黙って切れた接続も、応答が無ければ切ってルームから外す
// (切れた後は普段の切断と同じく OnDisconnected で退出する)。Client(MagicOnion.Client)は自動で応える
builder.Services.AddMagicOnion(options =>
{
    if (settings.HeartbeatIntervalSeconds <= 0) return;
    options.EnableStreamingHubHeartbeat = true;
    options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(settings.HeartbeatIntervalSeconds);
    options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(settings.HeartbeatTimeoutSeconds);
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(content);
// ワールド: フォルダが指定されていれば、そこから読む(無ければ作って書く)。指定が無ければ、起動のたびに新しいワールド
var accounts = new AccountRegistry();
var worldSave = string.IsNullOrWhiteSpace(settings.WorldDirectory) ? null : new WorldSave(settings.WorldDirectory, accounts);
var world = worldSave?.LoadOrCreate(settings.WorldName) ?? new WorldIdentity(settings.WorldName);
builder.Services.AddSingleton(accounts);
builder.Services.AddSingleton(world);
builder.Services.AddSingleton(new LoginGate(content.MasterSha256, settings.JoinPassword));
builder.Services.AddSingleton(new ChatGate(settings.ChatBurst, settings.ChatRefillSeconds));
builder.Services.AddSingleton<PlayerConnections>();
builder.Services.AddSingleton<ISpawnConfigProvider>(_ => content.Maps.Count > 0
    ? new MapSpawnConfigProvider(content.GetMap, content)
    : new DummySpawnConfigProvider());
builder.Services.AddSingleton<IMoveValidator, BasicMoveValidator>();
builder.Services.AddSingleton<RoomGroupRegistry>();
builder.Services.AddSingleton(provider =>
{
    var spawns = provider.GetRequiredService<ISpawnConfigProvider>();
    var groups = provider.GetRequiredService<RoomGroupRegistry>();
    var validator = provider.GetRequiredService<IMoveValidator>();
    return new RoomManager(spawns.GetSpawns, content.GetMap, validator, groups.CreateSink);
});
builder.Services.AddHostedService<RoomTickService>();
builder.Services.AddHostedService<ParentProcessWatcher>();

var app = builder.Build();

app.MapMagicOnionService();

// 動作確認用: ブラウザや curl で http://localhost:5001/ を開くと接続先とルーム状況が見える
app.MapGet("/", (RoomManager rooms, TerraceServerOptions options, ServerContent loaded) => Results.Json(new
{
    status = "ok",
    grpc = options.GrpcUrl,
    master = loaded.MasterSource,
    maps = loaded.Maps.Values.OrderBy(m => m.Id).Select(m => new { m.Id, m.Name, spawnPoints = m.SpawnPoints.Count }),
    warnings = loaded.Warnings,
    rooms = rooms.Rooms.Select(room => new
    {
        mapId = room.MapId,
        players = room.Players.Select(p => new { p.Info.PlayerId, p.Info.Name, p.State.X, p.State.Y, motion = p.State.Motion.ToString() }),
        enemies = room.Enemies.Select(e => new { e.InstanceId, e.EnemyId, e.Name, e.Hp, e.MaxHp, e.IsDead, e.X, e.Y }),
        drops = room.Drops.Select(d => new { d.DropId, d.ItemId, d.X, d.Y, d.RemainingSeconds }),
    }),
}));

// 閉じる口: ゲームから開いたワールドを、持ち主の Client が閉じる(鍵が合うときだけ。鍵の設定が空なら口は無い)。
// 状態確認のポートでだけ受ける(gRPC のポートでは受けない)。止めるのは、ふつうに止めるときと同じ経路なので、保存してから止まる
if (!string.IsNullOrEmpty(settings.AdminKey))
{
    app.MapPost("/admin/shutdown", (HttpRequest request, IHostApplicationLifetime lifetime) =>
    {
        if (!AdminKey.Matches(request.Headers[AdminKey.HeaderName].ToString(), settings.AdminKey)) return Results.StatusCode(StatusCodes.Status403Forbidden);
        app.Logger.LogInformation("閉じる口から止めます");
        lifetime.StopApplication();
        return Results.Json(new { status = "stopping" });
    }).RequireHost($"*:{settings.HttpPort}");
}

app.Logger.LogInformation("content: {Root}", contentRoot);
app.Logger.LogInformation("master: {Source}", content.MasterSource);
foreach (var map in content.Maps.Values.OrderBy(m => m.Id))
{
    app.Logger.LogInformation("map {Id} {Name}: 湧き点 {Spawns}", map.Id, map.Name, map.SpawnPoints.Count);
}
foreach (var warning in content.Warnings)
{
    app.Logger.LogWarning("content: {Warning}", warning);
}

// 起動時にルームを 1 つ作っておく(敵が湧いている状態で最初の人を迎える)
var roomManager = app.Services.GetRequiredService<RoomManager>();
var initialRoom = roomManager.GetOrCreate(settings.InitialMapId);
app.Logger.LogInformation("Room map={MapId} を作成しました (敵 {Enemies} 体)", initialRoom.MapId, initialRoom.Enemies.Count);
app.Logger.LogInformation("gRPC (h2c): {GrpcUrl}   状態確認: {HttpUrl}", settings.GrpcUrl, settings.HttpUrl);

// 保存: 新しいキャラクターを作ったときと、止まるとき(親のプロセスがいなくなったときを含む)
if (worldSave is not null)
{
    accounts.AccountCreated += _ => worldSave.Write(world);
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        worldSave.Write(world);
        app.Logger.LogInformation("world: {Path} に保存しました(キャラクター {Count} 人)", worldSave.FilePath, accounts.Count);
    });
    app.Logger.LogInformation("world: {Name} ({Id}) を {Path} から使います(キャラクター {Count} 人)", world.Name, world.Id, worldSave.FilePath, accounts.Count);
}
else
{
    app.Logger.LogInformation("world: {Name} ({Id})。保存しません(WorldDirectory が空)", world.Name, world.Id);
}

app.Run();
