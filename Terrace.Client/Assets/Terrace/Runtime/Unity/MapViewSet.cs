using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 今いるマップの見た目一式: 地形(MapView)・敵と落とし物(WorldViewSync)・NPC(NpcView)・背景(ParallaxBackdrop)と、
    /// カメラの範囲と空の色。マップが変わったら作り直し(MapChanged)、世界が差し替わったら敵と落とし物だけ作り直す(WorldReplaced)。
    /// プレイヤー・他のプレイヤー・HUD・店の窓はマップが変わっても作り直さないので、ここには無い。
    /// </summary>
    public sealed class MapViewSet
    {
        private readonly GameSimulation _simulation;
        private readonly Camera _camera;
        private readonly CameraRig _cameraRig;
        private readonly ArtLibrary? _art;
        private readonly bool _showSpawnMarkers;
        private readonly PlayerView? _playerView;
        private readonly Object? _logContext;
        private readonly List<NpcView> _npcViews = new List<NpcView>();
        private GameObject? _mapRoot;
        private GameObject? _worldRoot;
        private GameObject? _npcRoot;
        private GameObject? _backdrop;
        private WorldViewSync? _worldView;

        public MapViewSet(GameSimulation simulation, Camera camera, CameraRig cameraRig, ArtLibrary? art, bool showSpawnMarkers, PlayerView? playerView, Object? logContext)
        {
            _simulation = simulation;
            _camera = camera;
            _cameraRig = cameraRig;
            _art = art;
            _showSpawnMarkers = showSpawnMarkers;
            _playerView = playerView;
            _logContext = logContext;
            simulation.Travel.MapChanged += OnMapChanged;
            simulation.WorldReplaced += OnWorldReplaced;
        }

        /// <summary>今のマップの NPC の見た目(クリックの当たり判定に使う)。</summary>
        public IReadOnlyList<NpcView> NpcViews => _npcViews;

        /// <summary>今のマップの見た目を作る。</summary>
        public void Build(MapData map)
        {
            _mapRoot = new GameObject("Map");
            _mapRoot.AddComponent<MapView>().Build(map, _showSpawnMarkers, _art);

            _worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(_worldRoot.transform, _simulation.World, _art);

            _npcRoot = new GameObject("Npcs");
            _npcViews.Clear();
            foreach (var npc in map.Npcs)
            {
                var go = new GameObject($"Npc {npc.Name}#{npc.Id}");
                go.transform.SetParent(_npcRoot.transform, false);
                var view = go.AddComponent<NpcView>();
                view.Bind(npc, _art);
                _npcViews.Add(view);
            }

            _cameraRig.Bind(_simulation);
            var theme = _art?.Theme(map.Theme);
            if (theme != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = theme.SkyColor;
                if (theme.Background != null)
                {
                    _backdrop = new GameObject("Backdrop");
                    _backdrop.AddComponent<ParallaxBackdrop>().Bind(_camera, theme.Background, _camera.orthographicSize);
                }
            }

            _playerView?.Sync();
        }

        /// <summary>敵と落とし物の見た目をシミュレーションに合わせる(毎フレーム)。</summary>
        public void Sync() => _worldView?.Sync(_simulation.World);

        /// <summary>見た目を片付け、購読をやめる。</summary>
        public void Dispose()
        {
            _simulation.Travel.MapChanged -= OnMapChanged;
            _simulation.WorldReplaced -= OnWorldReplaced;
            TearDown();
        }

        private void OnMapChanged(MapData previous, MapData next)
        {
            TearDown();
            Build(next);
            Debug.Log($"[game] map changed: {previous.Name} -> {next.Name} (enemies={_simulation.World.Enemies.Count}, npcs={next.Npcs.Count})", _logContext);
        }

        private void OnWorldReplaced(WorldState world)
        {
            if (_worldRoot != null) Object.Destroy(_worldRoot);
            _worldRoot = new GameObject("World");
            _worldView = new WorldViewSync(_worldRoot.transform, world, _art);
        }

        private void TearDown()
        {
            if (_mapRoot != null) Object.Destroy(_mapRoot);
            if (_worldRoot != null) Object.Destroy(_worldRoot);
            if (_npcRoot != null) Object.Destroy(_npcRoot);
            if (_backdrop != null) Object.Destroy(_backdrop);
            _mapRoot = null;
            _worldRoot = null;
            _npcRoot = null;
            _backdrop = null;
            _worldView = null;
            _npcViews.Clear();
        }
    }
}
