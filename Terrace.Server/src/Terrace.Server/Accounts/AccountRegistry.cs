using System.Collections.Concurrent;
using Terrace.Shared;

namespace Terrace.Server.Accounts;

/// <summary>インメモリのアカウント台帳。名前 → プレイヤー ID。DB も認証も無し。</summary>
public sealed class AccountRegistry
{
    private readonly ConcurrentDictionary<string, LoginResult> _byName = new(StringComparer.Ordinal);
    private int _nextPlayerId;

    public int Count => _byName.Count;

    /// <summary>同じ名前なら同じ ID を返す。空なら "guest"。</summary>
    public LoginResult Login(string? name)
    {
        var key = string.IsNullOrWhiteSpace(name) ? "guest" : name.Trim();
        var result = _byName.GetOrAdd(key, n => new LoginResult
        {
            PlayerId = Interlocked.Increment(ref _nextPlayerId),
            Character = new CharacterInfo { Name = n, Level = 1, MapId = 1, X = 2f, Y = 0f },
        });

        // 呼び出し側が書き換えても台帳に影響しないよう複製して返す
        return new LoginResult
        {
            PlayerId = result.PlayerId,
            Character = new CharacterInfo
            {
                Name = result.Character.Name,
                Level = result.Character.Level,
                MapId = result.Character.MapId,
                X = result.Character.X,
                Y = result.Character.Y,
            },
        };
    }
}
