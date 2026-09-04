namespace Terrace.MasterData.Tests;

internal static class Fixtures
{
    public static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>フィクスチャを一時フォルダへ複製して返す(ファイルを消したり足したりする試験用)。</summary>
    public static string CopyToTemp(string name)
    {
        var target = TempDir();
        foreach (var file in Directory.EnumerateFiles(Dir(name)))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
        return target;
    }

    public static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "terrace-masterdata-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
