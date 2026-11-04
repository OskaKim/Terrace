using Terrace.Shared;

namespace Terrace.Server.Accounts;

/// <summary>ログインを断る理由。</summary>
public enum LoginRejection
{
    None,

    /// <summary>通信の定義の版が違う。</summary>
    ProtocolMismatch,

    /// <summary>マスタ(master.bytes)が違う。</summary>
    MasterMismatch,

    /// <summary>合言葉が違う(合言葉の要るワールドで)。</summary>
    WrongPassword,
}

/// <summary>
/// ログインの前の検査: 版の握手と合言葉。純 C#(MagicOnion を知らない)。
/// マスタの sha256 は、送られてきたときだけ比べる(マスタを持たないテストクライアントのため)。サーバーがマスタを読めていなければ比べない。
/// </summary>
public sealed class LoginGate(string masterSha256, string? password)
{
    private readonly string _password = password ?? string.Empty;

    public string MasterSha256 { get; } = masterSha256;

    public bool PasswordRequired => _password.Length > 0;

    public LoginRejection Check(LoginRequest request)
    {
        if (request.ProtocolVersion != ProtocolVersion.Current) return LoginRejection.ProtocolMismatch;
        if (!string.IsNullOrEmpty(request.MasterSha256) && MasterSha256.Length > 0
            && !string.Equals(request.MasterSha256, MasterSha256, StringComparison.OrdinalIgnoreCase))
        {
            return LoginRejection.MasterMismatch;
        }
        if (PasswordRequired && !string.Equals(request.Password ?? string.Empty, _password, StringComparison.Ordinal)) return LoginRejection.WrongPassword;
        return LoginRejection.None;
    }

    /// <summary>断ったときに Client へ返す文言(ログイン窓にそのまま出る)。</summary>
    public string Describe(LoginRejection rejection, LoginRequest request) => rejection switch
    {
        LoginRejection.ProtocolMismatch => $"サーバーと版が違います(サーバー: {ProtocolVersion.Current}、この Client: {request.ProtocolVersion})。同じ版のゲームで入ってください",
        LoginRejection.MasterMismatch => $"サーバーとマスタが違います(サーバー: {ShortPair(MasterSha256, request.MasterSha256)})。同じ版のゲームで入ってください",
        LoginRejection.WrongPassword => string.IsNullOrEmpty(request.Password) ? "このワールドに入るには合言葉が要ります" : "合言葉が違います",
        _ => string.Empty,
    };

    /// <summary>先頭 8 文字で見分けられればそれで、見分けられなければ全部を並べる。</summary>
    private static string ShortPair(string server, string client)
    {
        static string Short(string sha256) => sha256.Length > 8 ? sha256[..8] : sha256;
        var (s, c) = Short(server) == Short(client) ? (server, client) : (Short(server), Short(client));
        return $"{s}、この Client: {c}";
    }
}
