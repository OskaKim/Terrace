using Terrace.Client.Core;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>プレイヤーを追いかけ、ワールド境界の外を映さないカメラ。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public float OrthographicSize = 7f;
        public float FollowSpeed = 8f;
        public Vector2 Offset = new Vector2(0f, 1.5f);

        private Camera? _camera;
        private GameSimulation? _simulation;
        private WorldBounds? _bounds;

        public void Bind(GameSimulation simulation)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = OrthographicSize;
            _simulation = simulation;
            _bounds = simulation.Map.Bounds;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (_simulation == null) return;
            transform.position = Clamp(Target());
        }

        private void LateUpdate()
        {
            if (_simulation == null || _camera == null) return;
            var target = Clamp(Target());
            var t = 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, t);
        }

        private Vector3 Target()
        {
            var motor = _simulation!.Motor;
            return new Vector3(motor.X + Offset.x, motor.Y + Offset.y, -10f);
        }

        private Vector3 Clamp(Vector3 position)
        {
            if (_camera == null || _bounds == null || !_bounds.IsValid) return position;

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;

            position.x = _bounds.Width <= halfWidth * 2f
                ? (_bounds.Left + _bounds.Right) * 0.5f
                : Mathf.Clamp(position.x, _bounds.Left + halfWidth, _bounds.Right - halfWidth);
            position.y = _bounds.Height <= halfHeight * 2f
                ? (_bounds.Bottom + _bounds.Top) * 0.5f
                : Mathf.Clamp(position.y, _bounds.Bottom + halfHeight, _bounds.Top - halfHeight);
            position.z = -10f;
            return position;
        }
    }
}
