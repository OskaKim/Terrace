using MagicOnion;

namespace Terrace.Shared
{
    /// <summary>Unary サービス: ログイン。</summary>
    public interface IAccountService : IService<IAccountService>
    {
        /// <summary>表示名でログインし、プレイヤー ID とキャラ情報を返す。ID はログインのたびに新しく発行する(インメモリ)。</summary>
        UnaryResult<LoginResult> LoginAsync(string name);
    }
}
