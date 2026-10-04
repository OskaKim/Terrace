using System;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// ログイン窓の Presenter。名前と接続先、押せる・押せない(接続中)、状態の一行を持ち、
    /// 「オンラインで遊ぶ」「ひとりで遊ぶ」が選ばれたことを知らせる。接続そのものはしない(起動の流れが知らせを受けて繋ぐ)。
    ///
    /// 窓の決まり: 接続中は選べない。Enter で「オンラインで遊ぶ」を押したことになる。名前と接続先は前後の空白を落として渡す。
    /// </summary>
    public sealed class LoginPresenter
    {
        public const string DefaultStatus = "サーバーを立てていなくても「ひとりで遊ぶ」で遊べます";

        private readonly ILoginView _view;

        public LoginPresenter(ILoginView view, string playerName, string serverAddress)
        {
            _view = view;
            PlayerName = playerName;
            ServerAddress = serverAddress;

            view.PlayerNameChanged += value => PlayerName = value;
            view.ServerAddressChanged += value => ServerAddress = value;
            view.OnlineClicked += ChooseOnline;
            view.OfflineClicked += ChooseOffline;
            view.SubmitPressed += ChooseOnline;

            view.SetInputs(playerName, serverAddress);
            view.SetInteractable(true);
            SetStatus(DefaultStatus, false);
        }

        /// <summary>入力中の名前。</summary>
        public string PlayerName { get; private set; }

        /// <summary>入力中の接続先。</summary>
        public string ServerAddress { get; private set; }

        /// <summary>接続中などで選べないか。</summary>
        public bool IsBusy { get; private set; }

        public string StatusText { get; private set; } = string.Empty;
        public bool StatusIsError { get; private set; }

        /// <summary>「オンラインで遊ぶ」が選ばれた(前後の空白を落とした名前, 接続先)。</summary>
        public event Action<string, string>? OnlineRequested;

        /// <summary>「ひとりで遊ぶ」が選ばれた。</summary>
        public event Action? OfflineRequested;

        /// <summary>接続中などで選べなくする(busy が false なら戻す)。状態の一行も替える。</summary>
        public void SetBusy(bool busy, string status)
        {
            IsBusy = busy;
            _view.SetInteractable(!busy);
            SetStatus(status, false);
        }

        public void SetStatus(string text, bool isError)
        {
            StatusText = text;
            StatusIsError = isError;
            _view.SetStatus(text, isError);
        }

        public void ChooseOnline()
        {
            if (IsBusy) return;
            OnlineRequested?.Invoke(PlayerName.Trim(), ServerAddress.Trim());
        }

        public void ChooseOffline()
        {
            if (IsBusy) return;
            OfflineRequested?.Invoke();
        }

        /// <summary>窓を閉じる。</summary>
        public void Close() => _view.Close();
    }
}
