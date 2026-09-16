using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>
/// ルームで起きた状態変化の通知先。Room は MagicOnion を知らず、この口だけを呼ぶ。
/// サーバーでは <see cref="Terrace.Server.Hubs.GroupRoomEventSink"/> がグループ配信に変換し、テストでは記録用の実装を差す。
/// </summary>
public interface IRoomEventSink
{
    /// <summary>プレイヤーが参加した(本人以外へ通知する想定)。</summary>
    void OnPlayerJoined(PlayerInfo player, MoveState state);

    void OnPlayerLeft(int playerId);

    /// <summary>プレイヤーの移動状態が更新された(本人以外へ通知する想定)。</summary>
    void OnPlayerMoved(int playerId, MoveState state);

    /// <summary>敵が(再)出現した。初期スポーン分は Snapshot で渡すため通知しない。</summary>
    void OnEnemySpawned(EnemyState enemy);

    void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage);

    void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds);

    /// <summary>生きている敵の位置(定期)。</summary>
    void OnEnemyMoved(IReadOnlyList<EnemyMoveState> enemies);

    void OnDropSpawned(IReadOnlyList<DropState> drops);

    /// <summary>落ちていたアイテムが消えた。playerId が拾った人(0 なら時間切れ)。</summary>
    void OnDropRemoved(int dropId, int playerId);
}
