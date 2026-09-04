using MagicOnion;
using MagicOnion.Server;
using Terrace.Server.Accounts;
using Terrace.Shared;

namespace Terrace.Server.Services;

/// <summary>Unary サービスの実装。ロジックは AccountRegistry に委譲する。</summary>
public sealed class AccountService(AccountRegistry registry, ILogger<AccountService> logger)
    : ServiceBase<IAccountService>, IAccountService
{
    public UnaryResult<LoginResult> LoginAsync(string name)
    {
        var result = registry.Login(name);
        logger.LogInformation("Login name={Name} => playerId={PlayerId}", result.Character.Name, result.PlayerId);
        return UnaryResult.FromResult(result);
    }
}
