using System.Security.Cryptography;
using Terrace.Shared;

namespace Terrace.Server.Accounts;

/// <summary>ワールドのキャラクター 1 人。本人はトークンで見分ける(名前は表示用で、同じ名前の人がいてもよい)。</summary>
public sealed class Account(int playerId, string name, string token)
{
    public int PlayerId { get; } = playerId;
    public string Name { get; } = name;

    /// <summary>本人を示す、推測できない乱数。ログを含め、外に出さない(本人への LoginResult だけ)。</summary>
    public string Token { get; } = token;

    public PlayerInfo ToPlayerInfo() => new() { PlayerId = PlayerId, Name = Name };
}

/// <summary>
/// インメモリのアカウント台帳(ワールド 1 つ分)。保存はまだ無いので、サーバーを止めると消える。
/// 初めてのログインでキャラクターを作ってトークンを発行し、同じトークンのログインと参加は同じキャラクターとして扱う。
/// </summary>
public sealed class AccountRegistry
{
    /// <summary>新しいキャラクターが最初に立つマップ(テラスの町)。</summary>
    public const int StartMapId = 100;

    /// <summary>名前の長さの上限。長ければ切る。</summary>
    public const int MaxNameLength = 16;

    /// <summary>トークンの乱数のバイト数。</summary>
    public const int TokenBytes = 32;

    private readonly object _gate = new();
    private readonly Dictionary<string, Account> _byToken = new(StringComparer.Ordinal);
    private int _nextPlayerId;

    /// <summary>このワールドのキャラクターの数。</summary>
    public int Count
    {
        get { lock (_gate) return _byToken.Count; }
    }

    /// <summary>
    /// ログインする。token が知っているものなら、そのキャラクターを返す(名前は前のまま)。
    /// 無いか知らないものなら、name で新しいキャラクターを作り、新しいトークンを発行する。name が空なら "guest"。
    /// </summary>
    public Account Login(string? name, string? token)
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(token) && _byToken.TryGetValue(token, out var known)) return known;

            var account = new Account(++_nextPlayerId, NormalizeName(name), NewToken());
            _byToken.Add(account.Token, account);
            return account;
        }
    }

    /// <summary>トークンのキャラクター。知らないトークンなら null。</summary>
    public Account? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        lock (_gate) return _byToken.TryGetValue(token, out var account) ? account : null;
    }

    /// <summary>ログインの結果(本人だけに返す)。</summary>
    public static LoginResult ToResult(Account account, string worldId) => new()
    {
        PlayerId = account.PlayerId,
        Character = new CharacterInfo { Name = account.Name, Level = 1, MapId = StartMapId, X = 0f, Y = 0f },
        Token = account.Token,
        WorldId = worldId,
    };

    private static string NormalizeName(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "guest" : name.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private string NewToken()
    {
        // 衝突はまず起きないが、起きたら引き直す
        while (true)
        {
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));
            if (!_byToken.ContainsKey(token)) return token;
        }
    }
}
