using System.Collections.Generic;
using System.IO;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// StreamingAssets/maps の全マップを読み、マップ ID で引けるようにする。
    /// ファイル名が "sample" で始まるもの(Terrace.Map のサンプル)は読み込まない。同じ ID が複数あれば先勝ち。
    /// </summary>
    public sealed class MapRegistry
    {
        private readonly Dictionary<int, MapData> _maps = new Dictionary<int, MapData>();
        private readonly Dictionary<int, string> _files = new Dictionary<int, string>();
        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyDictionary<int, MapData> Maps => _maps;
        public IReadOnlyList<string> Warnings => _warnings;
        public int Count => _maps.Count;

        public static MapRegistry LoadFromStreamingAssets()
        {
            var registry = new MapRegistry();
            if (!Directory.Exists(MapLoader.MapsDirectory))
            {
                registry._warnings.Add($"maps フォルダがありません: {MapLoader.MapsDirectory}");
                return registry;
            }

            var files = Directory.GetFiles(MapLoader.MapsDirectory, "*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (var path in files)
            {
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith("sample", System.StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var map = MapLoader.LoadFromStreamingAssets(fileName);
                    registry.Add(map, fileName);
                }
                catch (System.Exception ex)
                {
                    registry._warnings.Add($"{fileName}: 読めません ({ex.Message})");
                }
            }
            return registry;
        }

        public void Add(MapData map, string fileName)
        {
            if (_maps.ContainsKey(map.Id))
            {
                _warnings.Add($"{fileName}: マップ ID {map.Id} は {_files[map.Id]} と重複しています(先のものを使います)");
                return;
            }
            _maps[map.Id] = map;
            _files[map.Id] = fileName;
        }

        public MapData? Get(int mapId) => _maps.TryGetValue(mapId, out var map) ? map : null;

        public string? FileOf(int mapId) => _files.TryGetValue(mapId, out var file) ? file : null;

        public void LogWarnings(Object? context = null)
        {
            foreach (var warning in _warnings) Debug.LogWarning($"[maps] {warning}", context);
        }
    }
}
