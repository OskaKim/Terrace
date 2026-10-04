using Terrace.Client.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// マウスのクリックを NPC への話しかけにする。クリックした画面の位置をワールド座標に直し、
    /// その上にいる NPC に話しかける(店なら開く)。店を開いている間はクリックで話しかけない。
    /// </summary>
    public sealed class PointerInteraction
    {
        private readonly GameSimulation _simulation;
        private readonly Camera _camera;
        private readonly MapViewSet _mapViews;

        public PointerInteraction(GameSimulation simulation, Camera camera, MapViewSet mapViews)
        {
            _simulation = simulation;
            _camera = camera;
            _mapViews = mapViews;
        }

        /// <summary>このフレームに左クリックされていれば、その位置をクリックしたとして扱う(毎フレーム)。</summary>
        public void Update()
        {
            if (_simulation.Trading.IsOpen) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var screen = mouse.position.ReadValue();
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            TryClickWorld(new Vector2(world.x, world.y));
        }

        /// <summary>ワールド座標をクリックしたとして扱う。NPC の上なら話しかけて true。</summary>
        public bool TryClickWorld(Vector2 world)
        {
            foreach (var view in _mapViews.NpcViews)
            {
                if (view.Npc != null && view.Contains(world))
                {
                    _simulation.Trading.Interact(view.Npc);
                    return true;
                }
            }
            return false;
        }
    }
}
