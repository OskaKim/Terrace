using System;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// LoginPresenter から見たログイン窓。窓は言われた通りに描き、押されたこと・入力が変わったことを知らせるだけ。
    /// 押せるかどうか・Enter で何が起きるかは LoginPresenter が決める。
    /// </summary>
    public interface ILoginView
    {
        /// <summary>名前と接続先の入力欄に値を入れる。</summary>
        void SetInputs(string playerName, string serverAddress);

        /// <summary>ボタンと入力欄を押せる(触れる)ようにするか。</summary>
        void SetInteractable(bool interactable);

        /// <summary>状態の一行。isError なら失敗として目立たせる。</summary>
        void SetStatus(string text, bool isError);

        /// <summary>窓を閉じる(壊す)。</summary>
        void Close();

        /// <summary>名前の入力が変わった。</summary>
        event Action<string>? PlayerNameChanged;

        /// <summary>接続先の入力が変わった。</summary>
        event Action<string>? ServerAddressChanged;

        /// <summary>「オンラインで遊ぶ」が押された。</summary>
        event Action? OnlineClicked;

        /// <summary>「ひとりで遊ぶ」が押された。</summary>
        event Action? OfflineClicked;

        /// <summary>決定のキー(Enter)が押された。</summary>
        event Action? SubmitPressed;
    }
}
