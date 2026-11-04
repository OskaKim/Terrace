using System.Diagnostics;
using Terrace.Server.Configuration;

namespace Terrace.Server.Hosting;

/// <summary>
/// 親のプロセス(ゲームから開いたワールドなら、その Client)を見張り、いなくなったら Server を止める。
/// 止めるときの保存は、ふつうに止めるときと同じ経路(<c>ApplicationStopping</c>)で行う。
/// <c>TerraceServerOptions.ParentProcessId</c> が 0 以下なら何もしない。
/// </summary>
public sealed class ParentProcessWatcher(TerraceServerOptions options, IHostApplicationLifetime lifetime, ILogger<ParentProcessWatcher> logger)
    : BackgroundService
{
    /// <summary>見に行く間隔(秒)。</summary>
    public const double IntervalSeconds = 1;

    /// <summary>そのプロセスが動いているか。</summary>
    public static bool IsAlive(int processId)
    {
        if (processId <= 0) return false;
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;   // もういない
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var parent = options.ParentProcessId;
        if (parent <= 0) return;

        if (!IsAlive(parent))
        {
            logger.LogWarning("親のプロセス {Parent} が起動の時点でいません。止めます", parent);
            lifetime.StopApplication();
            return;
        }

        logger.LogInformation("親のプロセス {Parent} を見張ります(いなくなったら止まる)", parent);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(IntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (IsAlive(parent)) continue;
                logger.LogWarning("親のプロセス {Parent} がいなくなりました。保存して止めます", parent);
                lifetime.StopApplication();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // ふつうに止まった
        }
    }
}
