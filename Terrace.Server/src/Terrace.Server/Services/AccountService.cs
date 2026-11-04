using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using Terrace.Server.Accounts;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Services;

/// <summary>Unary サービスの実装。検査は <see cref="LoginGate"/>、キャラクターは <see cref="AccountRegistry"/> に委ねる。</summary>
public sealed class AccountService(AccountRegistry registry, LoginGate gate, WorldIdentity world, RoomManager rooms, ILogger<AccountService> logger)
    : ServiceBase<IAccountService>, IAccountService
{
    public UnaryResult<ServerInfo> GetServerInfoAsync() => UnaryResult.FromResult(new ServerInfo
    {
        WorldId = world.Id,
        WorldName = world.Name,
        ProtocolVersion = ProtocolVersion.Current,
        MasterSha256 = gate.MasterSha256,
        PasswordRequired = gate.PasswordRequired,
        Players = rooms.Rooms.Sum(room => room.PlayerCount),
    });

    public UnaryResult<LoginResult> LoginAsync(LoginRequest request)
    {
        request ??= new LoginRequest();
        var rejection = gate.Check(request);
        if (rejection != LoginRejection.None)
        {
            logger.LogWarning("Login rejected name={Name}: {Rejection}", request.Name, rejection);
            var code = rejection == LoginRejection.WrongPassword ? StatusCode.PermissionDenied : StatusCode.FailedPrecondition;
            throw new ReturnStatusException(code, gate.Describe(rejection, request));
        }

        var returning = registry.FindByToken(request.Token) != null;
        var account = registry.Login(request.Name, request.Token);
        logger.LogInformation("Login name={Name} => playerId={PlayerId} ({Kind})", account.Name, account.PlayerId, returning ? "前のキャラクター" : "新しいキャラクター");
        return UnaryResult.FromResult(AccountRegistry.ToResult(account, world.Id));
    }
}
