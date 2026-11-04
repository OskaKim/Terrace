using System.Security.Cryptography;
using System.Text;
using Terrace.Shared;

namespace Terrace.Server.Accounts;

/// <summary>
/// ワールドのキャラクター 1 人。本人はトークンで見分ける(名前は表示用で、同じ名前の人がいてもよい)。
/// トークンそのものは持たず、sha256 だけを持つ(ワールドのファイルを人に渡しても、トークンは漏れない)。
/// </summary>
public sealed class Account(int playerId, string name, string tokenSha256)
{
    public int PlayerId { get; } = playerId;
    public string Name { get; } = name;

    /// <summary>トークンの sha256(16 進の小文字)。</summary>
    public string TokenSha256 { get; } = tokenSha256;

    public PlayerInfo ToPlayerInfo() => new() { PlayerId = PlayerId, Name = Name };
}

/// <summary>ログインの結果。Token は本人に返すトークン(新しく作ったときは新しいもの、戻ったときは送られてきたもの)。</summary>
public readonly record struct LoginOutcome(Account Account, string Token, bool IsNew);

/// <summary>
/// アカウント台帳(ワールド 1 つ分)。初めてのログインでキャラクターを作ってトークンを発行し、
/// 同じトークンのログインと参加は同じキャラクターとして扱う。
/// 保存はしない。ワールドのフォルダに残すのは <see cref="Saves.WorldSave"/>(<see cref="Restore"/> と <see cref="AccountCreated"/> を使う)。
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
    private readonly Dictionary<string, Account> _byTokenSha256 = new(StringComparer.Ordinal);
    private int _lastPlayerId;

    /// <summary>新しいキャラクターを作った(ロックの外で知らせる)。保存に使う。</summary>
    public event Action<Account>? AccountCreated;

    /// <summary>このワールドのキャラクターの数。</summary>
    public int Count
    {
        get { lock (_gate) return _byTokenSha256.Count; }
    }

    /// <summary>最後に発行したプレイヤー ID(次は 1 つ大きい番号を出す)。</summary>
    public int LastPlayerId
    {
        get { lock (_gate) return _lastPlayerId; }
    }

    /// <summary>今いるキャラクター(プレイヤー ID の順)。</summary>
    public IReadOnlyList<Account> Accounts
    {
        get { lock (_gate) return _byTokenSha256.Values.OrderBy(a => a.PlayerId).ToList(); }
    }

    /// <summary>
    /// ログインする。token が知っているものなら、そのキャラクターを返す(名前は前のまま)。
    /// 無いか知らないものなら、name で新しいキャラクターを作り、新しいトークンを発行する。name が空なら "guest"。
    /// </summary>
    public LoginOutcome Login(string? name, string? token)
    {
        LoginOutcome outcome;
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(token) && _byTokenSha256.TryGetValue(HashToken(token), out var known))
            {
                return new LoginOutcome(known, token, false);
            }

            var newToken = NewToken();
            var account = new Account(++_lastPlayerId, NormalizeName(name), HashToken(newToken));
            _byTokenSha256.Add(account.TokenSha256, account);
            outcome = new LoginOutcome(account, newToken, true);
        }
        AccountCreated?.Invoke(outcome.Account);
        return outcome;
    }

    /// <summary>トークンのキャラクター。知らないトークンなら null。</summary>
    public Account? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var hash = HashToken(token);
        lock (_gate) return _byTokenSha256.TryGetValue(hash, out var account) ? account : null;
    }

    /// <summary>保存してあったキャラクターを戻す(起動のときに 1 度だけ)。lastPlayerId は、戻したキャラクターの ID の最大より小さければそちらに合わせる。</summary>
    public void Restore(IEnumerable<Account> accounts, int lastPlayerId)
    {
        lock (_gate)
        {
            foreach (var account in accounts) _byTokenSha256[account.TokenSha256] = account;
            _lastPlayerId = Math.Max(lastPlayerId, _byTokenSha256.Count == 0 ? 0 : _byTokenSha256.Values.Max(a => a.PlayerId));
        }
    }

    /// <summary>ログインの結果(本人だけに返す)。</summary>
    public static LoginResult ToResult(LoginOutcome outcome, string worldId) => new()
    {
        PlayerId = outcome.Account.PlayerId,
        Character = new CharacterInfo { Name = outcome.Account.Name, Level = 1, MapId = StartMapId, X = 0f, Y = 0f },
        Token = outcome.Token,
        WorldId = worldId,
    };

    /// <summary>トークンの sha256(16 進の小文字)。</summary>
    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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
            if (!_byTokenSha256.ContainsKey(HashToken(token))) return token;
        }
    }
}
