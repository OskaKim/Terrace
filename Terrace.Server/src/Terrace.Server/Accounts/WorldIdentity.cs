using System.Security.Cryptography;

namespace Terrace.Server.Accounts;

/// <summary>
/// このサーバーのワールド。ID は Client がトークンを覚えるときの見出しになる(接続先のアドレスは変わることがあるため)。
/// 保存がまだ無いので、ID は起動のたびに作り直す(前の起動のキャラクターは残っていないので、古いトークンは使えなくてよい)。
/// </summary>
public sealed class WorldIdentity(string name, string? id = null)
{
    public string Id { get; } = string.IsNullOrEmpty(id) ? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) : id;

    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? "Terrace" : name.Trim();
}
