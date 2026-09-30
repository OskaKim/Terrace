using System.Diagnostics;

namespace Terrace.Server.Rooms;

/// <summary>一定間隔で全ルームの Tick を回す。</summary>
public sealed class RoomTickService(RoomManager rooms, ILogger<RoomTickService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RoomTickService を開始しました (interval = {Interval} ms)", Interval.TotalMilliseconds);
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
                rooms.TickAll(delta);
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
    }
}
