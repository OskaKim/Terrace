using MagicOnion.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Terrace.Server.Accounts;
using Terrace.Server.Configuration;
using Terrace.Server.Content;
using Terrace.Server.Hubs;
using Terrace.Server.Rooms;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection(TerraceServerOptions.SectionName).Get<TerraceServerOptions>() ?? new TerraceServerOptions();

// Kestrel: TLS なしの HTTP/2 (h2c) で gRPC を受ける。Unity(YetAnotherHttpHandler)も TestClient も http:// で繋ぐ。
// 平文で HTTP/1.1 と HTTP/2 を同じポートに同居させることはできないため、確認用の HTTP/1.1 は別ポートに開く。
builder.WebHost.ConfigureKestrel(kestrel =>
{
    if (settings.ListenAnyIP)
    {
        kestrel.ListenAnyIP(settings.GrpcPort, listen => listen.Protocols = HttpProtocols.Http2);
        kestrel.ListenAnyIP(settings.HttpPort, listen => listen.Protocols = HttpProtocols.Http1);
    }
    else
    {
        kestrel.ListenLocalhost(settings.GrpcPort, listen => listen.Protocols = HttpProtocols.Http2);
        kestrel.ListenLocalhost(settings.HttpPort, listen => listen.Protocols = HttpProtocols.Http1);
    }
});

// 内容物(マップとマスタ)を読む。マップが 1 枚も無ければダミーのスポーン設定で動かす。
var contentRoot = settings.ContentRoot ?? Path.Combine(AppContext.BaseDirectory, "content");
var content = ServerContent.Load(contentRoot);

builder.Services.AddMagicOnion();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(content);
builder.Services.AddSingleton<AccountRegistry>();
builder.Services.AddSingleton<ISpawnConfigProvider>(_ => content.Maps.Count > 0
    ? new MapSpawnConfigProvider(content.GetMap, content)
    : new DummySpawnConfigProvider());
builder.Services.AddSingleton<IMoveValidator, NullMoveValidator>();
builder.Services.AddSingleton<RoomGroupRegistry>();
builder.Services.AddSingleton(provider =>
{
    var spawns = provider.GetRequiredService<ISpawnConfigProvider>();
    var groups = provider.GetRequiredService<RoomGroupRegistry>();
    var validator = provider.GetRequiredService<IMoveValidator>();
    return new RoomManager(spawns.GetSpawns, content.GetMap, validator, groups.CreateSink);
});
builder.Services.AddHostedService<RoomTickService>();

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

app.Run();
