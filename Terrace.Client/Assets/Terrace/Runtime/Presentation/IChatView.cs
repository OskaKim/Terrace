using System;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// ChatPresenter から見たチャットの入力欄。欄は言われた通りに出し入れし、キーと入力を知らせるだけ。
    /// 開けるか・送るか・閉じたら何が起きるかは ChatPresenter が決める。
    /// </summary>
    public interface IChatView
    {
        /// <summary>入力中にするか。true なら入力欄を出してフォーカスを入れ、false なら外して隠す。</summary>
        void SetTyping(bool typing);

        /// <summary>入力欄の文字を入れ替える。</summary>
        void SetText(string text);

        /// <summary>入力できる文字数の上限。</summary>
        void SetCharacterLimit(int limit);

        /// <summary>入力していないときに、開くキー(Enter)が押された。</summary>
        event Action? OpenPressed;

        /// <summary>入力中に、送るキー(Enter)が押された(入力欄の文字)。</summary>
        event Action<string>? Submitted;

        /// <summary>入力中に、やめるキー(Esc)が押されたか、フォーカスが外れた。</summary>
        event Action? CancelPressed;
    }
}
