namespace Terrace.Shared
{
    /// <summary>チャットの届く範囲。今はワールド全体だけ。足すときは末尾の次の番号にする。</summary>
    public enum ChatChannel
    {
        /// <summary>ワールド(サーバー)に参加している全員。</summary>
        World = 0,
    }

    /// <summary>チャットを断った理由(<see cref="ChatRejectedNotification"/>)。</summary>
    public enum ChatRejection
    {
        /// <summary>本文が <see cref="ChatLimits.MaxLength"/> より長い。</summary>
        TooLong = 1,

        /// <summary>続けて送りすぎた(連投の間引き)。</summary>
        TooFast = 2,
    }

    /// <summary>チャットの決まりのうち、Client と Server で同じ値を使うもの。</summary>
    public static class ChatLimits
    {
        /// <summary>本文の長さの上限(前後の空白と制御文字を落とした後の UTF-16 の文字数)。Client の入力欄もこれで切る。</summary>
        public const int MaxLength = 100;
    }
}
