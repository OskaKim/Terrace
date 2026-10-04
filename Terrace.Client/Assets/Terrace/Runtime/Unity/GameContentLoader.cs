using System;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>ゲームを始めるのに読み込む物の束。</summary>
    public sealed class GameContent
    {
        public GameContent(MapRegistry maps, MapData startMap, MasterDataRepository? masterData, ArtLibrary? art)
        {
            Maps = maps;
            StartMap = startMap;
            MasterData = masterData;
            Art = art;
        }

        /// <summary>StreamingAssets/maps の全マップ。</summary>
        public MapRegistry Maps { get; }

        /// <summary>始めるマップ。</summary>
        public MapData StartMap { get; }

        /// <summary>master.bytes。読めなければ null(仮の定義で動く)。</summary>
        public MasterDataRepository? MasterData { get; }

        /// <summary>Kenney の素材。Resources に無ければ null(生成スプライトで動く)。</summary>
        public ArtLibrary? Art { get; }
    }

    /// <summary>
    /// マップ一式(StreamingAssets/maps/*.json)・マスタ(master.bytes)・絵を読み、GameContent にまとめる。
    /// マスタと絵は無くても動けるようにし、何を使うかをログに残す。
    /// </summary>
    public static class GameContentLoader
    {
        /// <param name="startMapId">始めるマップ ID(maps/*.json の id)。</param>
        /// <param name="mapFileName">指定するとマップ一覧の代わりにこのファイルだけを読んで始める(テスト用)。</param>
        /// <param name="art">読み込み済みの素材の台帳(素材が無ければ IsAvailable が false)。</param>
        /// <param name="logContext">ログを出すときに添える物。</param>
        public static GameContent Load(int startMapId, string mapFileName, ArtLibrary art, UnityEngine.Object? logContext)
        {
            // マップ
            var maps = MapRegistry.LoadFromStreamingAssets();
            maps.LogWarnings(logContext);
            MapData map;
            if (!string.IsNullOrEmpty(mapFileName))
            {
                map = MapLoader.LoadFromStreamingAssets(mapFileName);
                if (maps.Get(map.Id) == null) maps.Add(map, mapFileName);
            }
            else
            {
                map = maps.Get(startMapId) ?? throw new InvalidOperationException($"マップ ID {startMapId} が maps/ にありません (読めたのは {maps.Count} 枚)");
            }
            foreach (var issue in map.Validate())
            {
                Debug.LogWarning($"[map] {issue}", logContext);
            }

            // マスタ
            MasterDataRepository? masterData = null;
            try
            {
                masterData = MasterDataRepository.LoadFromStreamingAssets();
                Debug.Log($"[masterdata] loaded: items={masterData.ItemCount} enemies={masterData.EnemyCount} quests={masterData.QuestCount} levels={masterData.LevelCount} sha={masterData.ShortVersion}", logContext);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[masterdata] master.bytes を読めませんでした。仮の定義で続行します: {ex.Message}", logContext);
            }

            // 素材
            if (art.IsAvailable)
            {
                Debug.Log("[art] Kenney Platformer Art Deluxe / UI Pack (CC0) の素材を使います", logContext);
            }
            else
            {
                Debug.LogWarning("[art] Resources/Terrace/Art/Kenney に素材が無いため、生成スプライトで続行します", logContext);
            }

            return new GameContent(maps, map, masterData, art.IsAvailable ? art : null);
        }
    }
}
