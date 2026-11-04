using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Terrace.Client.Online
{
    /// <summary>手元の Server の起動のしかた。どこにある Server を使うかは Unity 層(LocalServerLocator)が決める。</summary>
    public sealed class LocalServerOptions
    {
        /// <summary>起動するもの(自己完結の版なら Terrace.Server.exe、エディタなら dotnet)。</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>FileName の直後に渡す引数(dotnet で動かすときの Terrace.Server.dll のパス)。</summary>
        public IReadOnlyList<string> PrefixArguments { get; set; } = Array.Empty<string>();

        /// <summary>起動するときの作業フォルダ。空なら FileName のフォルダ。</summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>ワールドのフォルダ(Server の TerraceServerOptions.WorldDirectory)。</summary>
        public string WorldDirectory { get; set; } = string.Empty;

        /// <summary>ワールドを作るときの名前。</summary>
        public string WorldName { get; set; } = string.Empty;

        /// <summary>gRPC のポート。0 なら空いているポートを選ぶ。</summary>
        public int GrpcPort { get; set; }

        /// <summary>状態確認の HTTP のポート。0 なら空いているポートを選ぶ。</summary>
        public int HttpPort { get; set; }

        /// <summary>Server の標準出力と標準エラーを書き足すファイル。空なら書かない。</summary>
        public string LogPath { get; set; } = string.Empty;

        /// <summary>状態確認の口が応えるまで待つ秒数。過ぎたら止めて失敗にする。</summary>
        public float StartTimeoutSeconds { get; set; } = 30f;

        /// <summary>閉じる口を叩いてから、終わるまで待つ秒数。過ぎたら強制的に終わらせる。</summary>
        public float StopTimeoutSeconds { get; set; } = 10f;
    }

    /// <summary>手元の Server を起動できなかった。Message は人に見せる理由(Server の出力の最後の数行を含む)。</summary>
    public sealed class LocalServerException : Exception
    {
        public LocalServerException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 手元の Server(ゲームから開くワールド。ADR 0015)。同梱の Terrace.Server を子プロセスで起動し、localhost だけで待たせる。
    ///
    /// - 起動: ワールドのフォルダ・名前・ポート・親のプロセス(自分)・閉じる口の鍵(乱数)を起動引数で渡す
    /// - 待つ: 状態確認の口が応えるまで。その前にプロセスが終わったら、出力の最後の数行を添えて失敗にする
    /// - 閉じる: 閉じる口(POST /admin/shutdown)を叩き、保存して終わるのを待つ。待ちすぎたら強制的に終わらせる
    /// - このプロセスが落ちたときは、Server が親の見張りで自分から止まる
    ///
    /// UnityEngine に依存しない(.NET でも試せる)。
    /// </summary>
    public sealed class LocalServer : IDisposable
    {
        /// <summary>閉じる口の鍵を載せるヘッダー(Server の AdminKey.HeaderName と同じ)。</summary>
        public const string AdminKeyHeader = "X-Terrace-Admin-Key";

        /// <summary>覚えておく出力の行数(失敗の理由に添える)。</summary>
        public const int RecentLineCount = 12;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        private readonly Process _process;
        private readonly string _adminKey;
        private readonly float _stopTimeoutSeconds;
        private readonly Queue<string> _recent = new Queue<string>();
        private readonly object _gate = new object();
        private readonly StreamWriter? _log;
        private int _disposed;

        private LocalServer(Process process, int grpcPort, int httpPort, string adminKey, float stopTimeoutSeconds, StreamWriter? log)
        {
            _process = process;
            GrpcPort = grpcPort;
            HttpPort = httpPort;
            _adminKey = adminKey;
            _stopTimeoutSeconds = stopTimeoutSeconds;
            _log = log;
        }

        public int GrpcPort { get; }
        public int HttpPort { get; }

        /// <summary>繋ぎ先(MagicOnionConnection.ConnectAsync に渡す)。</summary>
        public string Address => $"http://localhost:{GrpcPort}";

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        /// <summary>Server の出力の最後の数行。</summary>
        public string RecentOutput
        {
            get { lock (_gate) return string.Join("\n", _recent); }
        }

        /// <summary>起動して、状態確認の口が応えるまで待つ。失敗したら LocalServerException(起動したプロセスは止める)。</summary>
        public static async Task<LocalServer> StartAsync(LocalServerOptions options, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(options.FileName)) throw new LocalServerException("起動する Server がありません");
            if (string.IsNullOrEmpty(options.WorldDirectory)) throw new LocalServerException("ワールドのフォルダがありません");

            var grpcPort = options.GrpcPort > 0 ? options.GrpcPort : FreePort();
            var httpPort = options.HttpPort > 0 ? options.HttpPort : FreePort(grpcPort);
            var adminKey = NewKey();

            var arguments = new List<string>(options.PrefixArguments)
            {
                $"--Terrace:GrpcPort={grpcPort}",
                $"--Terrace:HttpPort={httpPort}",
                "--Terrace:ListenAnyIP=false",
                "--Terrace:StatusListenAnyIP=false",
                $"--Terrace:WorldDirectory={options.WorldDirectory}",
                $"--Terrace:WorldName={options.WorldName}",
                $"--Terrace:ParentProcessId={CurrentProcessId()}",
                $"--Terrace:AdminKey={adminKey}",
            };

            var startInfo = new ProcessStartInfo(options.FileName, JoinArguments(arguments))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = string.IsNullOrEmpty(options.WorkingDirectory) ? Path.GetDirectoryName(options.FileName) ?? string.Empty : options.WorkingDirectory,
            };

            StreamWriter? log = null;
            if (!string.IsNullOrEmpty(options.LogPath))
            {
                var directory = Path.GetDirectoryName(options.LogPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                log = new StreamWriter(options.LogPath, append: true, Encoding.UTF8) { AutoFlush = true };
                log.WriteLine($"---- {DateTime.Now:yyyy-MM-dd HH:mm:ss} {options.FileName} {startInfo.Arguments.Replace(adminKey, "***")}");
            }

            Process process;
            try
            {
                process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                if (!process.Start()) throw new LocalServerException($"{options.FileName} を起動できませんでした");
            }
            catch (Exception ex) when (!(ex is LocalServerException))
            {
                log?.Dispose();
                throw new LocalServerException($"{options.FileName} を起動できませんでした: {ex.Message}", ex);
            }

            var server = new LocalServer(process, grpcPort, httpPort, adminKey, options.StopTimeoutSeconds, log);
            process.OutputDataReceived += (_, e) => server.Remember(e.Data);
            process.ErrorDataReceived += (_, e) => server.Remember(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await server.WaitUntilReadyAsync(options.StartTimeoutSeconds, cancellationToken).ConfigureAwait(false);
                return server;
            }
            catch
            {
                server.Kill();
                server.Dispose();
                throw;
            }
        }

        /// <summary>閉じる口で閉じ、終わるのを待つ。待ちすぎたら強制的に終わらせる。</summary>
        public async Task StopAsync()
        {
            if (HasExited) return;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{HttpPort}/admin/shutdown");
                request.Headers.Add(AdminKeyHeader, _adminKey);
                using var response = await Http.SendAsync(request).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 口が応えなくても、下で待ってから強制的に終わらせる
            }

            var deadline = DateTime.UtcNow.AddSeconds(_stopTimeoutSeconds);
            while (!HasExited && DateTime.UtcNow < deadline) await Task.Delay(100).ConfigureAwait(false);
            if (!HasExited)
            {
                Remember($"[local-server] {_stopTimeoutSeconds} 秒で終わらなかったので、強制的に終わらせます");
                Kill();
            }
        }

        /// <summary>閉じて(待てるだけ待ち)、片付ける。何度呼んでもよい。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(_stopTimeoutSeconds + 1f));
            }
            catch (Exception)
            {
                Kill();
            }
            finally
            {
                _process.Dispose();
                lock (_gate) _log?.Dispose();
            }
        }

        private async Task WaitUntilReadyAsync(float timeoutSeconds, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasExited)
                {
                    // 終わった直後は出力が届ききっていないことがあるので、少し待つ
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    throw new LocalServerException($"Server が起動の途中で終わりました(終了コード {ExitCodeOrUnknown()})\n{RecentOutput}");
                }

                try
                {
                    using var response = await Http.GetAsync($"http://localhost:{HttpPort}/", cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException)
                {
                    // まだ待ち受けていない
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 1 回分の問い合わせが時間切れ
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new LocalServerException($"Server が {timeoutSeconds:F0} 秒以内に応えませんでした\n{RecentOutput}");
                }
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        private void Remember(string? line)
        {
            if (line == null) return;
            lock (_gate)
            {
                _recent.Enqueue(line);
                while (_recent.Count > RecentLineCount) _recent.Dequeue();
                try
                {
                    _log?.WriteLine(line);
                }
                catch (ObjectDisposedException)
                {
                    // 片付けた後に届いた行
                }
            }
        }

        private void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill();
            }
            catch (Exception)
            {
                // もう終わっている
            }
        }

        private string ExitCodeOrUnknown()
        {
            try
            {
                return _process.ExitCode.ToString();
            }
            catch (InvalidOperationException)
            {
                return "不明";
            }
        }

        private static int CurrentProcessId()
        {
            using var current = Process.GetCurrentProcess();
            return current.Id;
        }

        /// <summary>空いている TCP のポート(except とは別のもの)。</summary>
        private static int FreePort(int except = 0)
        {
            while (true)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                if (port != except) return port;
            }
        }

        private static string NewKey()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        /// <summary>引数を 1 つの文字列にする(空白や " を含むものは Windows の決まりで囲む)。</summary>
        public static string JoinArguments(IEnumerable<string> arguments)
        {
            var builder = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(Quote(argument));
            }
            return builder.ToString();
        }

        private static string Quote(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return argument;

            var builder = new StringBuilder("\"");
            var backslashes = 0;
            foreach (var c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                }
                else
                {
                    builder.Append('\\', backslashes);
                    builder.Append(c);
                }
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}
