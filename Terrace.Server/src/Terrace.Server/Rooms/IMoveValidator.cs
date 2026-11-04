using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>
/// 移動の検証フック。移動はクライアント権威なので、ここで断るのは壊れた値だけにする(<see cref="BasicMoveValidator"/>)。
/// 速度や到達可能性の検査を入れるときも、ここに足す。
/// </summary>
public interface IMoveValidator
{
    /// <summary>
    /// proposed を受け入れてよいか。false なら状態を更新せず、他のクライアントへも中継しない(参加なら断る)。
    /// map は今のマップ(マップの無いルームでは null)。current は今の状態(参加のときは null)。
    /// </summary>
    bool IsAcceptable(MapData? map, RoomPlayer? current, MoveState proposed);
}

/// <summary>何も検証しない Null 実装(テスト用)。</summary>
public sealed class NullMoveValidator : IMoveValidator
{
    public bool IsAcceptable(MapData? map, RoomPlayer? current, MoveState proposed) => true;
}

/// <summary>
/// 壊れた値を断る(Server の既定)。改ざんした Client が、他の人の画面や Server を壊せないようにする。
///
/// - 座標と速度が有限(NaN・無限大でない)
/// - 向き(<see cref="Facing"/>)と動作(<see cref="MotionState"/>)が定義された値
/// - マップがあれば、境界を <see cref="BoundsMargin"/> だけ広げた範囲の中
///
/// 速度の大きさや、前の位置から届くか(テレポート)は見ない(ADR 0014。手触りへの響きを見てから)。
/// </summary>
public sealed class BasicMoveValidator : IMoveValidator
{
    /// <summary>
    /// 境界の外へこれだけはみ出すまでは受け取る。Client は X を境界に収めるが、Y は落死するまで下端より下がる
    /// (Client の <c>MotorConfig.FallDeathMargin</c>)ので、それより大きく取る。
    /// </summary>
    public const float BoundsMargin = 10f;

    public bool IsAcceptable(MapData? map, RoomPlayer? current, MoveState proposed)
    {
        if (proposed is null) return false;
        if (!float.IsFinite(proposed.X) || !float.IsFinite(proposed.Y)) return false;
        if (!float.IsFinite(proposed.VelocityX) || !float.IsFinite(proposed.VelocityY)) return false;
        if (!Enum.IsDefined(proposed.Facing) || !Enum.IsDefined(proposed.Motion)) return false;

        var bounds = map?.Bounds;
        if (bounds is null || !bounds.IsValid) return true;
        return proposed.X >= bounds.Left - BoundsMargin && proposed.X <= bounds.Right + BoundsMargin
            && proposed.Y >= bounds.Bottom - BoundsMargin && proposed.Y <= bounds.Top + BoundsMargin;
    }
}
