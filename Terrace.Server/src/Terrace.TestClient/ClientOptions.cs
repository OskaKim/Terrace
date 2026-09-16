namespace Terrace.TestClient;

public enum ClientMode
{
    Patrol,
    Manual,
}

/// <summary>Terrace.TestClient --name &lt;表示名&gt; --map &lt;mapId&gt; --server &lt;URL&gt; [--mode patrol|manual] [--interval &lt;ms&gt;]</summary>
public sealed class ClientOptions
{
    public string Name { get; private set; } = string.Empty;
    public int MapId { get; private set; } = 1;
    public string Server { get; private set; } = "http://localhost:5000";
    public ClientMode Mode { get; private set; } = ClientMode.Patrol;
    public int IntervalMs { get; private set; } = 500;

    /// <summary>0 なら Ctrl+C まで動き続ける。正なら秒数経過後に自動で LeaveAsync して終了する(自動テスト用)。</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>patrol 中に定期的に最寄りの敵へ AttackAsync を送る。</summary>
    public bool Attack { get; private set; }

    /// <summary>参加位置。省略時はログイン結果の位置。</summary>
    public float? X { get; private set; }
    public float? Y { get; private set; }

    public string? Error { get; private set; }

    public const string Usage =
        """
        使い方: Terrace.TestClient --name <表示名> [--map <mapId>] [--server <URL>] [--mode patrol|manual]
                                   [--interval <ms>] [--duration <秒>] [--attack] [--x <X>] [--y <Y>]

          --name      表示名(必須)。ログインのたびに新しいプレイヤー ID が発行される
          --map       参加するマップ ID(既定 1)
          --server    サーバーの URL(既定 http://localhost:5000。TLS なしの HTTP/2)
          --mode      patrol: X を左右に往復させて MoveAsync を送り続ける(既定)
                      manual: 矢印キーで移動、スペースで最寄りの敵に AttackAsync、q で終了
          --interval  patrol の送信間隔ミリ秒(既定 500)
          --duration  指定秒数が経ったら自動で退室して終了する(既定 0 = Ctrl+C まで)
          --attack    patrol 中、4 回に 1 回は最寄りの敵に AttackAsync(ダメージ 10)を送る
          --x, --y    参加する位置(既定はログイン結果の位置)

        Ctrl+C で LeaveAsync してから終了します。
        """;

    public static ClientOptions Parse(string[] args)
    {
        var options = new ClientOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (++i >= args.Length) throw new ArgumentException($"{arg} には値が必要です");
                return args[i];
            }

            try
            {
                switch (arg)
                {
                    case "--name": options.Name = Next(); break;
                    case "--map": options.MapId = int.Parse(Next()); break;
                    case "--server": options.Server = Next(); break;
                    case "--mode":
                        options.Mode = Next().ToLowerInvariant() switch
                        {
                            "patrol" => ClientMode.Patrol,
                            "manual" => ClientMode.Manual,
                            var other => throw new ArgumentException($"不明なモードです: {other}"),
                        };
                        break;
                    case "--interval": options.IntervalMs = Math.Max(50, int.Parse(Next())); break;
                    case "--duration": options.DurationSeconds = Math.Max(0, int.Parse(Next())); break;
                    case "--attack": options.Attack = true; break;
                    case "--x": options.X = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--y": options.Y = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--help":
                    case "-h":
                        options.Error = "";
                        return options;
                    default:
                        options.Error = $"不明な引数です: {arg}";
                        return options;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                options.Error = ex.Message;
                return options;
            }
        }

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            options.Error = "--name を指定してください";
        }
        return options;
    }
}
