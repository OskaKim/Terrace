namespace Terrace.MasterData.Builder.Cli;

/// <summary>masterdata-build --input &lt;CSVフォルダ&gt; --output &lt;出力フォルダ&gt; [--verbose] [--no-timestamp]</summary>
public sealed class CommandLineOptions
{
    public string? Input { get; private set; }
    public string? Output { get; private set; }
    public bool Verbose { get; private set; }
    public bool NoTimestamp { get; private set; }
    public bool Help { get; private set; }
    public string? Error { get; private set; }

    public const string Usage =
        """
        使い方: masterdata-build --input <CSVフォルダ> --output <出力フォルダ> [--verbose] [--no-timestamp]

          --input, -i     テーブル名と同名の CSV(item.csv など)を置いたフォルダ
          --output, -o    master.bytes と manifest.json の出力先
          --verbose, -v   テーブル一覧や MasterMemory の生の検証結果も表示する
          --no-timestamp  manifest.json に生成日時(generatedAt)を書かない。同じ CSV から同じ manifest になる
          --help, -h      このヘルプ

        終了コード: 0 = 成功, 1 = エラーあり(出力は書き出さない), 2 = 引数エラー
        """;

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--input":
                case "-i":
                    if (++i >= args.Length) { options.Error = $"{arg} には値が必要です"; return options; }
                    options.Input = args[i];
                    break;
                case "--output":
                case "-o":
                    if (++i >= args.Length) { options.Error = $"{arg} には値が必要です"; return options; }
                    options.Output = args[i];
                    break;
                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;
                case "--no-timestamp":
                    options.NoTimestamp = true;
                    break;
                case "--help":
                case "-h":
                    options.Help = true;
                    return options;
                default:
                    options.Error = $"不明な引数です: {arg}";
                    return options;
            }
        }

        if (options.Input is null) options.Error = "--input を指定してください";
        else if (options.Output is null) options.Error = "--output を指定してください";
        return options;
    }
}
