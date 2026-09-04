using Terrace.Map;

namespace Terrace.Map.Tests;

internal static class Fixtures
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static string Broken(string name) => Path(System.IO.Path.Combine("broken", name));

    public static MapData LoadSample() => MapData.Load(Path("sample_map.json"));

    public static string TempFile(string name)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "terrace-map-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return System.IO.Path.Combine(dir, name);
    }
}
