using System;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Shared;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// チャットの入力欄の Presenter。入力中かを持ち、送るのは <see cref="OnlineSession.SendChat"/> に任せる。
    ///
    /// 決まり:
    /// - Enter で入力欄を開き、もう一度 Enter で送って閉じる(空なら送らずに閉じる)。Esc で閉じる
    /// - 開けるのはオンラインのときだけ。店を開いている間も開けない
    /// - 入力中は移動の入力を止める(起動役が <see cref="IsTyping"/> を見て InputFrame.None にする)
    /// - 文字数は <see cref="ChatLimits.MaxLength"/> で切る
    /// - 断られたら(<see cref="ChatRejectedNotification"/>)、その本文を入力欄に戻す(次に開いたときに直して送れる)
    /// - オフラインに切り替わったら閉じる
    /// </summary>
    public sealed class ChatPresenter : IDisposable
    {
        private readonly GameSimulation _simulation;
        private readonly IChatView _view;
        private OnlineSession? _online;

        public ChatPresenter(GameSimulation simulation, IChatView view)
        {
            _simulation = simulation;
            _view = view;
            _online = simulation.Online;

            view.OpenPressed += Open;
            view.Submitted += Submit;
            view.CancelPressed += Cancel;
            if (_online != null) _online.ChatRejected += OnRejected;
            simulation.WentOffline += OnWentOffline;

            view.SetCharacterLimit(ChatLimits.MaxLength);
            view.SetTyping(false);
        }

        /// <summary>入力中か。この間は移動の入力とキーの切り替え(消音・ミニマップ)を止める。</summary>
        public bool IsTyping { get; private set; }

        /// <summary>チャットを使えるか(オンラインのときだけ)。</summary>
        public bool IsAvailable => _online != null && _simulation.Online == _online;

        public void Dispose()
        {
            _view.OpenPressed -= Open;
            _view.Submitted -= Submit;
            _view.CancelPressed -= Cancel;
            Unsubscribe();
            _simulation.WentOffline -= OnWentOffline;
        }

        public void Open()
        {
            if (IsTyping || !IsAvailable || _simulation.Trading.IsOpen) return;
            IsTyping = true;
            _view.SetTyping(true);
        }

        public void Submit(string text)
        {
            if (!IsTyping) return;
            Close();
            _view.SetText(string.Empty);
            if (IsAvailable) _online!.SendChat(text);
        }

        public void Cancel()
        {
            if (!IsTyping) return;
            Close();
        }

        private void Close()
        {
            IsTyping = false;
            _view.SetTyping(false);
        }

        private void OnRejected(ChatRejectedNotification rejected) => _view.SetText(rejected.Text);

        private void OnWentOffline(string reason)
        {
            Unsubscribe();
            if (IsTyping) Close();
        }

        private void Unsubscribe()
        {
            if (_online == null) return;
            _online.ChatRejected -= OnRejected;
            _online = null;
        }
    }
}
