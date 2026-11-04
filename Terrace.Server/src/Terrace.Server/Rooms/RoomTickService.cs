using System.Diagnostics;

namespace Terrace.Server.Rooms;

/// <summary>
/// 一定間隔で全ルームの Tick を回す。
/// 例外でループを抜けると BackgroundService の既定でホストごと止まるので、ログに出して次の Tick へ進む。
/// </summary>
public sealed class RoomTickService(RoomManager rooms, ILogger<RoomTickService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RoomTickService を開始しました (interval = {Interval} ms)", Interval.TotalMilliseconds);
        rooms.TickFailed += OnTickFailed;
        using var timer = new PeriodicTimer(Interval);
        var stopwatch = Stopwatch.StartNew();
        var last = stopwatch.Elapsed;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = stopwatch.Elapsed;
                var delta = (float)(now - last).TotalSeconds;
                last = now;
                try
                {
                    rooms.TickAll(delta);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TickAll が例外を投げました。次の Tick へ進みます");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
        finally
        {
            rooms.TickFailed -= OnTickFailed;
        }
    }

    private void OnTickFailed(Room room, Exception ex)
        => logger.LogError(ex, "Room map={MapId} の Tick が例外を投げました。ルームは残し、次の Tick でまた進めます", room.MapId);
}
