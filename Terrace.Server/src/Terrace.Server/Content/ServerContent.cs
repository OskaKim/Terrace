using Terrace.Map;
using Terrace.MasterData;
using Terrace.MasterData.Builder.Build;
using Terrace.MasterData.Builder.Output;
using Terrace.Server.Rooms;

namespace Terrace.Server.Content;

/// <summary>
/// サーバーが使う内容物(マップとマスタ)。
/// - マップ: content/maps/*.json(Terrace.Map/maps からビルド時に複製。sample* は除く)
/// - マスタ: content/master.bytes があれば読み、無ければ content/csv から masterdata-build と同じパイプラインで組み立てる
/// </summary>
public sealed class ServerContent : IEnemyStatsProvider
{
    private readonly Dictionary<int, MapData> _maps;

    private ServerContent(Dictionary<int, MapData> maps, MemoryDatabase? master, string masterSource, string masterSha256, IReadOnlyList<string> warnings)
    {
        _maps = maps;
        Master = master;
        MasterSource = masterSource;
        MasterSha256 = masterSha256;
        Warnings = warnings;
    }

    public IReadOnlyDictionary<int, MapData> Maps => _maps;
    public MemoryDatabase? Master { get; }
    public string MasterSource { get; }

    /// <summary>読んだマスタ(master.bytes、または CSV から組み立てたもの)の sha256。Client の manifest の sha256 と同じ形。読めなければ空。</summary>
    public string MasterSha256 { get; }
    public IReadOnlyList<string> Warnings { get; }

    public MapData? GetMap(int mapId) => _maps.TryGetValue(mapId, out var map) ? map : null;

    public EnemyStats? Get(int enemyId)
    {
        if (Master == null || !Master.EnemyTable.TryFindByEnemyId(enemyId, out var enemy)) return null;
        return new EnemyStats(enemy.EnemyId, enemy.Name, enemy.Hp, enemy.Attack, enemy.DropItemIds ?? Array.Empty<int>());
    }

    public static ServerContent Load(string contentRoot)
    {
        var warnings = new List<string>();
        var maps = new Dictionary<int, MapData>();

        var mapsDirectory = Path.Combine(contentRoot, "maps");
        if (Directory.Exists(mapsDirectory))
        {
            foreach (var path in Directory.GetFiles(mapsDirectory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith("sample", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var map = MapData.Load(path);
                    if (!maps.TryAdd(map.Id, map))
                    {
                        warnings.Add($"{fileName}: マップ ID {map.Id} が重複しています(先のものを使います)");
                        continue;
                    }
                    foreach (var issue in map.Validate())
                    {
                        warnings.Add($"{fileName}: {issue}");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"{fileName}: 読めません ({ex.Message})");
                }
            }
        }
        else
        {
            warnings.Add($"maps フォルダがありません: {mapsDirectory}");
        }

        MemoryDatabase? master = null;
        var masterSource = "(none)";
        var masterSha256 = string.Empty;
        var masterBytes = Path.Combine(contentRoot, "master.bytes");
        var csvDirectory = Path.Combine(contentRoot, "csv");
        if (File.Exists(masterBytes))
        {
            var bytes = File.ReadAllBytes(masterBytes);
            master = new MemoryDatabase(bytes);
            masterSource = masterBytes;
            masterSha256 = Manifest.ComputeSha256(bytes);
        }
        else if (Directory.Exists(csvDirectory))
        {
            var result = BuildPipeline.Run(csvDirectory);
            if (result.Success && result.Binary != null)
            {
                master = new MemoryDatabase(result.Binary);
                masterSha256 = Manifest.ComputeSha256(result.Binary);
                masterSource = $"{csvDirectory} (built, sha256 {masterSha256[..8]})";
            }
            else
            {
                foreach (var diagnostic in result.Diagnostics.Errors) warnings.Add($"master: {diagnostic}");
            }
        }
        else
        {
            warnings.Add($"master.bytes も csv フォルダもありません: {contentRoot}");
        }

        return new ServerContent(maps, master, masterSource, masterSha256, warnings);
    }
}
