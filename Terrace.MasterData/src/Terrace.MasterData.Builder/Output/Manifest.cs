using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Terrace.MasterData.Builder.Build;

namespace Terrace.MasterData.Builder.Output;

/// <summary>
/// master.bytes に添える manifest.json。
/// クライアントとサーバーのマスタバージョン一致チェックに使う(SHA256 を比較する)。
/// </summary>
public sealed class Manifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public int FormatVersion { get; set; } = 1;

    /// <summary>生成日時(UTC, ISO 8601)。書かない指定(--no-timestamp)のときは null で、JSON に出さない。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GeneratedAt { get; set; }

    /// <summary>master.bytes の SHA256(小文字 16 進)。</summary>
    public string Sha256 { get; set; } = "";

    public long ByteLength { get; set; }

    public List<ManifestTable> Tables { get; set; } = new();

    /// <param name="generatedAt">書く生成日時。null なら生成日時を書かない</param>
    public static Manifest Create(byte[] binary, IEnumerable<TableBuildInfo> tables, DateTimeOffset? generatedAt)
    {
        return new Manifest
        {
            GeneratedAt = generatedAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            Sha256 = ComputeSha256(binary),
            ByteLength = binary.LongLength,
            Tables = tables
                .Select(t => new ManifestTable { Name = t.Schema.TableName, Type = t.Schema.Type.FullName ?? t.Schema.Type.Name, Count = t.RowCount })
                .ToList(),
        };
    }

    public static string ComputeSha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static Manifest FromJson(string json)
        => JsonSerializer.Deserialize<Manifest>(json, JsonOptions) ?? throw new InvalidDataException("manifest.json を読み取れません");
}

public sealed class ManifestTable
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Count { get; set; }
}
