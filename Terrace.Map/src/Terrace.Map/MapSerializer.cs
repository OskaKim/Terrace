using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Terrace.Map
{
    /// <summary>
    /// マップの JSON 入出力(System.Text.Json)。
    /// - プロパティ名は camelCase、読み込みは大文字小文字を区別しない
    /// - 計算プロパティ(Left / Right / IsVertical など)は書き出さない
    /// - コメント(// と /* */)と末尾カンマを許容する
    ///
    /// このファイルだけが System.Text.Json に依存する。Unity など System.Text.Json の無い環境へ持ち込むときは
    /// このファイルを除外し、同じ JSON 形式を読めるシリアライザで MapData を復元する。
    /// </summary>
    public static class MapSerializer
    {
        private static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                IgnoreReadOnlyProperties = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        public static string ToJson(MapData map) => JsonSerializer.Serialize(map, Options);

        public static MapData FromJson(string json)
            => JsonSerializer.Deserialize<MapData>(json, Options) ?? throw new InvalidDataException("マップ JSON を読み取れません");

        public static MapData Load(string path) => FromJson(File.ReadAllText(path, Encoding.UTF8));

        public static void Save(MapData map, string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(path, ToJson(map), new UTF8Encoding(false));
        }
    }

    /// <summary>MapData.Load / Save。MapSerializer と同じファイルに置き、System.Text.Json の無い環境ではまとめて除外できるようにする。</summary>
    public sealed partial class MapData
    {
        public static MapData Load(string path) => MapSerializer.Load(path);

        public void Save(string path) => MapSerializer.Save(this, path);
    }
}
