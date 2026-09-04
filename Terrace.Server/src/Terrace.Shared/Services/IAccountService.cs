using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>Unary サービス: ログイン。</summary>
    public interface IAccountService : IService<IAccountService>
    {
        /// <summary>表示名でログインし、プレイヤー ID とキャラ情報を返す。同じ名前なら同じ ID(インメモリ)。</summary>
        UnaryResult<LoginResult> LoginAsync(string name);
    }
}
