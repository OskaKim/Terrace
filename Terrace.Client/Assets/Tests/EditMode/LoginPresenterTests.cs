using System;
using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Presentation;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>ログイン窓の Presenter を、Unity の窓の代わりに記録するだけの偽の窓で試す。</summary>
    [TestFixture]
    public class LoginPresenterTests
    {
        private static (LoginPresenter Presenter, FakeLoginView View, List<(string Name, string Address)> Online, Func<int> Offline) NewLogin()
        {
            var view = new FakeLoginView();
            var presenter = new LoginPresenter(view, "旅人123", "http://localhost:5000");
            var online = new List<(string, string)>();
            var offline = 0;
            presenter.OnlineRequested += (name, address) => online.Add((name, address));
            presenter.OfflineRequested += () => offline++;
            return (presenter, view, online, () => offline);
        }

        [Test]
        public void 始めは名前と接続先の初期値と案内の一行が出て押せる()
        {
            var (_, view, _, _) = NewLogin();

            Assert.AreEqual(("旅人123", "http://localhost:5000"), (view.PlayerName, view.ServerAddress));
            Assert.IsTrue(view.Interactable);
            Assert.AreEqual((LoginPresenter.DefaultStatus, false), (view.Status, view.StatusIsError));
        }

        [Test]
        public void 名前と接続先の前後の空白を落としてオンラインを選んだと知らせる()
        {
            var (_, view, online, _) = NewLogin();

            view.TypeName("  alice ");
            view.TypeServer(" 127.0.0.1:5000\t");
            view.ClickOnline();

            Assert.AreEqual(new[] { ("alice", "127.0.0.1:5000") }, online.ToArray());
        }

        [Test]
        public void Enterでオンラインをひとりで遊ぶでオフラインを選んだことになる()
        {
            var (_, view, online, offline) = NewLogin();

            view.PressEnter();
            view.ClickOffline();

            Assert.AreEqual(1, online.Count);
            Assert.AreEqual(1, offline());
        }

        [Test]
        public void 接続中は選べず終われば戻る()
        {
            var (presenter, view, online, offline) = NewLogin();

            presenter.SetBusy(true, "接続中…");
            view.ClickOnline();
            view.PressEnter();
            view.ClickOffline();

            Assert.IsFalse(view.Interactable);
            Assert.AreEqual("接続中…", view.Status);
            Assert.IsEmpty(online);
            Assert.AreEqual(0, offline());

            presenter.SetBusy(false, string.Empty);
            view.ClickOffline();
            Assert.IsTrue(view.Interactable);
            Assert.AreEqual(1, offline());
        }

        [Test]
        public void 失敗の理由を出すと状態が失敗になる()
        {
            var (presenter, view, _, _) = NewLogin();

            presenter.SetStatus("接続できませんでした", true);

            Assert.IsTrue(presenter.StatusIsError);
            Assert.AreEqual(("接続できませんでした", true), (view.Status, view.StatusIsError));
        }

        /// <summary>言われたことを覚えておき、押されたことは試験から起こす偽の窓。</summary>
        private sealed class FakeLoginView : ILoginView
        {
            public string PlayerName { get; private set; } = string.Empty;
            public string ServerAddress { get; private set; } = string.Empty;
            public bool Interactable { get; private set; }
            public string Status { get; private set; } = string.Empty;
            public bool StatusIsError { get; private set; }
            public bool Closed { get; private set; }

            public event Action<string>? PlayerNameChanged;
            public event Action<string>? ServerAddressChanged;
            public event Action? OnlineClicked;
            public event Action? OfflineClicked;
            public event Action? SubmitPressed;

            public void SetInputs(string playerName, string serverAddress)
            {
                PlayerName = playerName;
                ServerAddress = serverAddress;
            }

            public void SetInteractable(bool interactable) => Interactable = interactable;

            public void SetStatus(string text, bool isError)
            {
                Status = text;
                StatusIsError = isError;
            }

            public void Close() => Closed = true;

            public void TypeName(string value)
            {
                PlayerName = value;
                PlayerNameChanged?.Invoke(value);
            }

            public void TypeServer(string value)
            {
                ServerAddress = value;
                ServerAddressChanged?.Invoke(value);
            }

            public void ClickOnline() => OnlineClicked?.Invoke();
            public void ClickOffline() => OfflineClicked?.Invoke();
            public void PressEnter() => SubmitPressed?.Invoke();
        }
    }
}
