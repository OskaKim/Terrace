namespace Terrace.Server.Accounts;

/// <summary>
/// プレイヤー ID と、それを使っている接続の対応。1 つの ID を使えるのは、最後に参加した 1 接続だけ。純 C#。
///
/// 同じ ID で別の接続が参加すると、先の接続を置き換える(再接続で、切れた古い接続が残っているときなど)。
/// 置き換えられた接続は持ち主でなくなり、その接続の退出は後から来た接続を消さない。
/// 参加と退出の処理は、渡された処理をロックの中で呼ぶ。同じ ID の参加と退出が入り混じると、
/// 配信のグループ(キーは ID)やルームから、後から来た接続を消してしまうため。
/// 発行した本人かどうかは確かめない(認証が無い。docs/roadmap.md の既知の不整合)。
/// </summary>
public sealed class PlayerConnections
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Owner> _owners = new();

    private readonly record struct Owner(object Connection, int MapId);

    /// <summary>
    /// connection が mapId で playerId を使い始める。ロックの中で join を呼ぶ。
    /// 別の接続が使っていれば、その接続がいたマップを join に渡す(join の中で外す)。いなければ null。
    /// </summary>
    public void Join(int playerId, object connection, int mapId, Action<int?> join)
    {
        lock (_gate)
        {
            int? replacedMapId = _owners.TryGetValue(playerId, out var owner) && !ReferenceEquals(owner.Connection, connection)
                ? owner.MapId
                : null;
            join(replacedMapId);
            _owners[playerId] = new Owner(connection, mapId);
        }
    }

    /// <summary>
    /// connection が playerId の持ち主なら、ロックの中で leave を呼んでから外し、true を返す。
    /// 置き換えられた後(持ち主でない)なら何もせず false。
    /// </summary>
    public bool Leave(int playerId, object connection, Action leave)
    {
        lock (_gate)
        {
            if (!IsOwnerCore(playerId, connection)) return false;
            leave();
            _owners.Remove(playerId);
            return true;
        }
    }

    /// <summary>connection が今 playerId を使っているか。置き換えられた接続は false。</summary>
    public bool IsOwner(int playerId, object connection)
    {
        lock (_gate) return IsOwnerCore(playerId, connection);
    }

    private bool IsOwnerCore(int playerId, object connection)
        => _owners.TryGetValue(playerId, out var owner) && ReferenceEquals(owner.Connection, connection);
}
