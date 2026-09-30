using Terrace.MasterData.Builder.Build;
using Terrace.MasterData.Builder.Cli;
using Terrace.MasterData.Builder.Output;

namespace Terrace.MasterData.Tests;

public class BuildCommandTests
{
    [Fact]
    public void 成功時は終了コード0で出力ファイルを書く()
    {
        var output = Fixtures.TempDir();
        var writer = new StringWriter();

        var code = BuildCommand.Run(["--input", Fixtures.Dir("valid"), "--output", output, "--verbose"], writer);

        Assert.Equal(BuildCommand.ExitSuccess, code);
        var binary = Path.Combine(output, BuildPipeline.BinaryFileName);
        var manifestPath = Path.Combine(output, BuildPipeline.ManifestFileName);
        Assert.True(File.Exists(binary));
        Assert.True(File.Exists(manifestPath));

        var manifest = Manifest.FromJson(File.ReadAllText(manifestPath));
        Assert.Equal(Manifest.ComputeSha256(File.ReadAllBytes(binary)), manifest.Sha256);
        Assert.Contains("成功:", writer.ToString());
    }

    [Fact]
    public void 検証エラーがあれば終了コード1で何も書かない()
    {
        var output = Fixtures.TempDir();
        var writer = new StringWriter();

        var code = BuildCommand.Run(["--input", Fixtures.Dir("broken_reference"), "--output", output], writer);

        Assert.Equal(BuildCommand.ExitFailure, code);
        Assert.False(File.Exists(Path.Combine(output, BuildPipeline.BinaryFileName)));
        var text = writer.ToString();
        Assert.Contains("quest.csv:3  [RewardItemId] item.csv に存在しないIDを参照しています (value = 999)", text);
        Assert.Contains("失敗:", text);
    }

    [Fact]
    public void 引数が足りなければ終了コード2()
    {
        var writer = new StringWriter();

        Assert.Equal(BuildCommand.ExitUsage, BuildCommand.Run(["--input", "x"], writer));
        Assert.Equal(BuildCommand.ExitUsage, BuildCommand.Run(["--bogus"], writer));
        Assert.Contains("使い方", writer.ToString());
    }

    [Fact]
    public void helpは終了コード0()
    {
        var writer = new StringWriter();
        Assert.Equal(BuildCommand.ExitSuccess, BuildCommand.Run(["--help"], writer));
        Assert.Contains("--input", writer.ToString());
    }

    [Fact]
    public void 入力フォルダが無ければ終了コード1()
    {
        var writer = new StringWriter();
        var code = BuildCommand.Run(["--input", Path.Combine(Fixtures.TempDir(), "nope"), "--output", Fixtures.TempDir()], writer);
        Assert.Equal(BuildCommand.ExitFailure, code);
        Assert.Contains("入力フォルダが見つかりません", writer.ToString());
    }
}
