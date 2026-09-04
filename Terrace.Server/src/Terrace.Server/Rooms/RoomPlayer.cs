using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>ルーム内のプレイヤー。</summary>
public sealed class RoomPlayer
{
    public RoomPlayer(PlayerInfo info, MoveState state)
    {
        Info = info;
        State = state;
    }

    public PlayerInfo Info { get; }

    /// <summary>最後に受け取った移動状態。</summary>
    public MoveState State { get; internal set; }

    public PlayerSnapshot ToSnapshot() => new() { Info = Info, State = State };

    public override string ToString() => $"{Info} {State}";
}
