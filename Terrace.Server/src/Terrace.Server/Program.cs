using MagicOnion.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Terrace.Server.Accounts;
using Terrace.Server.Configuration;
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

builder.Services.AddMagicOnion();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<AccountRegistry>();
builder.Services.AddSingleton<ISpawnConfigProvider, DummySpawnConfigProvider>();
builder.Services.AddSingleton<IMoveValidator, NullMoveValidator>();
builder.Services.AddSingleton<RoomGroupRegistry>();
builder.Services.AddSingleton(provider =>
{
    var spawns = provider.GetRequiredService<ISpawnConfigProvider>();
    var groups = provider.GetRequiredService<RoomGroupRegistry>();
    var validator = provider.GetRequiredService<IMoveValidator>();
    return new RoomManager(spawns.GetSpawns, validator, groups.CreateSink);
});
builder.Services.AddHostedService<RoomTickService>();

var app = builder.Build();

app.MapMagicOnionService();

// 動作確認用: ブラウザや curl で http://localhost:5001/ を開くと接続先とルーム状況が見える
app.MapGet("/", (RoomManager rooms, TerraceServerOptions options) => Results.Json(new
{
    status = "ok",
    grpc = options.GrpcUrl,
    rooms = rooms.Rooms.Select(room => new
    {
        mapId = room.MapId,
        players = room.Players.Select(p => new { p.Info.PlayerId, p.Info.Name, p.State.X, p.State.Y, motion = p.State.Motion.ToString() }),
        enemies = room.Enemies.Select(e => new { e.InstanceId, e.EnemyId, e.Hp, e.MaxHp, e.IsDead, e.X, e.Y }),
    }),
}));

// 起動時にダミーのスポーン設定でルームを 1 つ作る
var roomManager = app.Services.GetRequiredService<RoomManager>();
var initialRoom = roomManager.GetOrCreate(settings.InitialMapId);
app.Logger.LogInformation("Room map={MapId} を作成しました (敵 {Enemies} 体)", initialRoom.MapId, initialRoom.Enemies.Count);
app.Logger.LogInformation("gRPC (h2c): {GrpcUrl}   状態確認: {HttpUrl}", settings.GrpcUrl, settings.HttpUrl);

app.Run();
