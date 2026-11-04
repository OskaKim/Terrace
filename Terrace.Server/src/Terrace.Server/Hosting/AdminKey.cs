using System.Security.Cryptography;
using System.Text;

namespace Terrace.Server.Hosting;

/// <summary>閉じる口などの管理の口の鍵の照合。</summary>
public static class AdminKey
{
    /// <summary>鍵を載せるヘッダー。Client(<c>LocalServer</c>)と同じ名前。</summary>
    public const string HeaderName = "X-Terrace-Admin-Key";

    /// <summary>provided が expected と同じか。expected が空なら、何を渡されても false(管理の口は無い)。</summary>
    public static bool Matches(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
