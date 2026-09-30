using Terrace.Shared;

namespace Terrace.Server.Accounts;

/// <summary>
/// インメモリのアカウント台帳。DB も認証も無し。
/// ログインのたびに新しいプレイヤー ID を発行する(同じ名前で複数人が同時に遊べる。名前は表示用)。
/// </summary>
public sealed class AccountRegistry
{
    /// <summary>新しいキャラクターが最初に立つマップ(テラスの町)。</summary>
    public const int StartMapId = 100;

    private int _nextPlayerId;
    private int _count;

    /// <summary>これまでに発行した ID の数。</summary>
    public int Count => _count;

    /// <summary>名前でログインし、新しいプレイヤー ID を返す。空なら "guest"。名前は 16 文字まで。</summary>
    public LoginResult Login(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "guest" : name.Trim();
        if (trimmed.Length > 16) trimmed = trimmed[..16];

        Interlocked.Increment(ref _count);
        return new LoginResult
        {
            PlayerId = Interlocked.Increment(ref _nextPlayerId),
            Character = new CharacterInfo { Name = trimmed, Level = 1, MapId = StartMapId, X = 0f, Y = 0f },
        };
    }
}
