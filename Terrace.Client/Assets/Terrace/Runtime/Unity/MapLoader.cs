using System.IO;
using Newtonsoft.Json;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// StreamingAssets/maps/*.json から MapData を読む。
    /// Terrace.Map の MapSerializer(System.Text.Json)は Unity に無いので、同じ JSON 形式(camelCase)を Newtonsoft で読む。
    /// </summary>
    public static class MapLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static string MapsDirectory => Path.Combine(Application.streamingAssetsPath, "maps");

        public static string PathOf(string fileName) => Path.Combine(MapsDirectory, fileName);

        public static MapData LoadFromStreamingAssets(string fileName)
        {
            var path = PathOf(fileName);
            if (!File.Exists(path)) throw new FileNotFoundException($"マップが見つかりません: {path}", path);
            return FromJson(File.ReadAllText(path));
        }

        public static MapData FromJson(string json)
        {
            var map = JsonConvert.DeserializeObject<MapData>(json, Settings);
            if (map == null) throw new InvalidDataException("マップ JSON を読み取れません");
            map.RebuildIndex();
            return map;
        }
    }
}
