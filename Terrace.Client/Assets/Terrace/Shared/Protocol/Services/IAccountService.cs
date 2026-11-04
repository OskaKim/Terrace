using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>Unary サービス: サーバーの情報とログイン。</summary>
    public interface IAccountService : IService<IAccountService>
    {
        /// <summary>ログインの前に聞ける、ワールドの情報(版・マスタ・合言葉が要るか など)。</summary>
        UnaryResult<ServerInfo> GetServerInfoAsync();

        /// <summary>
        /// ログインする。トークンがこのワールドで知っているものなら同じキャラクターに戻り、無いか知らないものなら新しいキャラクターを作る。
        /// 版が違えば FailedPrecondition、合言葉が違えば PermissionDenied で断る。
        /// </summary>
        UnaryResult<LoginResult> LoginAsync(LoginRequest request);
    }
}
