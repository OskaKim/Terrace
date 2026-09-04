using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>
/// 移動の検証フック。移動はクライアント権威なので初期実装は常に true を返すが、
/// 後で権威をサーバーへ移すときはここに速度・到達可能性などのチェックを入れる。
/// </summary>
public interface IMoveValidator
{
    /// <summary>proposed を受け入れてよいか。false なら状態を更新せず、他のクライアントへも中継しない。</summary>
    bool IsAcceptable(RoomPlayer player, MoveState proposed);
}

/// <summary>何も検証しない Null 実装(クライアント権威)。</summary>
public sealed class NullMoveValidator : IMoveValidator
{
    public bool IsAcceptable(RoomPlayer player, MoveState proposed) => true;
}
