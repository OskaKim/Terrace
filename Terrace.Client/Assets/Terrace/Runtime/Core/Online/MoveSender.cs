using System;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// 自分の移動状態をいつ送るかを決める。毎フレーム送ると多すぎるので間引く。
    ///   - 向き・状態(歩く/跳ぶ/攻撃…)が変わったら、ほぼすぐ送る(UrgentInterval)
    ///   - 位置が動いていれば MinInterval ごとに送る
    ///   - 何も変わらなくても HeartbeatInterval ごとに送る(取りこぼしの保険)
    /// </summary>
    public sealed class MoveSender
    {
        public const float PositionEpsilon = 0.02f;

        private MoveState? _last;
        private float _sinceSent;

        public float MinInterval { get; set; } = 0.1f;
        public float UrgentInterval { get; set; } = 0.03f;
        public float HeartbeatInterval { get; set; } = 1.0f;

        /// <summary>送った回数(表示・テスト用)。</summary>
        public int SentCount { get; private set; }

        /// <summary>時間を進め、今 current を送るべきかを返す。</summary>
        public bool ShouldSend(MoveState current, float dt)
        {
            _sinceSent += dt;
            if (_last == null) return true;

            var urgent = current.Motion != _last.Motion || current.Facing != _last.Facing;
            if (urgent && _sinceSent >= UrgentInterval) return true;

            var moved = Math.Abs(current.X - _last.X) > PositionEpsilon || Math.Abs(current.Y - _last.Y) > PositionEpsilon;
            if (moved && _sinceSent >= MinInterval) return true;

            return _sinceSent >= HeartbeatInterval;
        }

        public void MarkSent(MoveState state)
        {
            _last = state.Clone();
            _sinceSent = 0f;
            SentCount++;
        }

        /// <summary>マップ移動などで「次は必ず送る」状態に戻す。</summary>
        public void Reset()
        {
            _last = null;
            _sinceSent = 0f;
        }
    }
}
