using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Client.Presentation;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>チャットの入力欄の Presenter(通信はせず、送った本文は偽の送り口に残す)。</summary>
    [TestFixture]
    public class ChatPresenterTests
    {
        private const float Dt = 1f / 60f;

        private sealed class FakeChannel : IOnlineChannel
        {
            public PlayerInfo Self { get; } = new PlayerInfo { PlayerId = 1, Name = "alice" };
            public string ServerAddress => "fake://server";
            public List<string> Chats { get; } = new List<string>();
            public void Join(int mapId, MoveState state) { }
            public void Move(MoveState state) { }
            public void Attack(int enemyInstanceId, int damage) { }
            public void Pickup(int dropId) { }
            public void SendChat(string text) => Chats.Add(text);
        }

        private sealed class FakeChatView : IChatView
        {
            public bool Typing { get; private set; } = true;
            public string Text { get; private set; } = "(未設定)";
            public int Limit { get; private set; }

            public event Action? OpenPressed;
            public event Action<string>? Submitted;
            public event Action? CancelPressed;

            public void SetTyping(bool typing) => Typing = typing;
            public void SetText(string text) => Text = text;
            public void SetCharacterLimit(int limit) => Limit = limit;

            public void PressOpen() => OpenPressed?.Invoke();
            public void Submit(string text) => Submitted?.Invoke(text);
            public void PressCancel() => CancelPressed?.Invoke();
        }

        private static (GameSimulation Sim, FakeChannel Channel, OnlineInbox Inbox, FakeChatView View, ChatPresenter Presenter) Online(MapData? map = null)
        {
            var start = map ?? TestMaps.Field();
            var channel = new FakeChannel();
            var inbox = new OnlineInbox();
            var sim = new GameSimulation(
                start, TestMaps.Lookup, TestMaps.ItemName,
                random: new Random(1),
                mapLookup: id => id == start.Id ? start : null,
                items: TestMaps.Catalog,
                shops: new PlaceholderShopCatalog(TestMaps.Catalog),
                online: channel,
                inbox: inbox);
            var view = new FakeChatView();
            return (sim, channel, inbox, view, new ChatPresenter(sim, view));
        }

        [Test]
        public void 最初は閉じていて文字数の上限を入力欄に伝える()
        {
            var (_, _, _, view, presenter) = Online();

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsFalse(view.Typing);
            Assert.AreEqual(ChatLimits.MaxLength, view.Limit);
            Assert.IsTrue(presenter.IsAvailable);
        }

        [Test]
        public void Enterで開いてもう一度Enterで送って閉じる()
        {
            var (_, channel, _, view, presenter) = Online();

            view.PressOpen();
            Assert.IsTrue(presenter.IsTyping);
            Assert.IsTrue(view.Typing);

            view.Submit("  こんにちは  ");
            Assert.IsFalse(presenter.IsTyping);
            Assert.IsFalse(view.Typing);
            Assert.AreEqual(string.Empty, view.Text, "送ったら入力欄を空にする");
            Assert.AreEqual(new[] { "こんにちは" }, channel.Chats.ToArray());
        }

        [Test]
        public void 空の本文は送らずに閉じる()
        {
            var (_, channel, _, view, presenter) = Online();

            view.PressOpen();
            view.Submit("   ");

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsEmpty(channel.Chats);
        }

        [Test]
        public void Escで閉じて送らない()
        {
            var (_, channel, _, view, presenter) = Online();

            view.PressOpen();
            view.PressCancel();

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsFalse(view.Typing);
            Assert.IsEmpty(channel.Chats);
        }

        [Test]
        public void 閉じているときの送信とやめるは何もしない()
        {
            var (_, channel, _, view, presenter) = Online();

            view.Submit("開いていない");
            view.PressCancel();

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsEmpty(channel.Chats);
        }

        [Test]
        public void 店を開いている間は開けない()
        {
            var (sim, _, _, view, presenter) = Online(TestMaps.Town());
            sim.Trading.Interact(sim.Map.FindNpc(1)!);
            Assert.IsTrue(sim.Trading.IsOpen);

            view.PressOpen();

            Assert.IsFalse(presenter.IsTyping);
        }

        [Test]
        public void 断られたら本文を入力欄に戻す()
        {
            var (sim, _, inbox, view, _) = Online();
            view.PressOpen();
            view.Submit("連投です");
            Assert.AreEqual(string.Empty, view.Text);

            IGameHubReceiver server = inbox;
            server.OnNotification(new ChatRejectedNotification { Reason = ChatRejection.TooFast, Text = "連投です" });
            sim.Step(InputFrame.None, Dt);

            Assert.AreEqual("連投です", view.Text);
            Assert.IsFalse(view.Typing, "戻すだけで、勝手には開かない");
        }

        [Test]
        public void オフラインに切り替わったら閉じて開けなくなる()
        {
            var (sim, channel, inbox, view, presenter) = Online();
            view.PressOpen();

            inbox.MarkDisconnected("テスト");
            sim.Step(InputFrame.None, Dt);

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsFalse(view.Typing);
            Assert.IsFalse(presenter.IsAvailable);
            view.PressOpen();
            Assert.IsFalse(presenter.IsTyping);
            view.Submit("届かない");
            Assert.IsEmpty(channel.Chats);
        }

        [Test]
        public void ひとりで遊んでいるときは開けない()
        {
            var sim = TestMaps.NewSimulation();
            var view = new FakeChatView();
            var presenter = new ChatPresenter(sim, view);

            view.PressOpen();

            Assert.IsFalse(presenter.IsAvailable);
            Assert.IsFalse(presenter.IsTyping);
        }

        [Test]
        public void Disposeの後は入力欄の知らせを受けない()
        {
            var (_, channel, _, view, presenter) = Online();
            presenter.Dispose();

            view.PressOpen();
            view.Submit("届かない");

            Assert.IsFalse(presenter.IsTyping);
            Assert.IsEmpty(channel.Chats);
        }
    }

    [TestFixture]
    public class ChatBubbleLayoutTests
    {
        [Test]
        public void 幅は半角を1全角と絵文字を2と数える()
        {
            Assert.AreEqual(5, ChatBubbleLayout.WidthOf("hello"));
            Assert.AreEqual(10, ChatBubbleLayout.WidthOf("こんにちは"));
            Assert.AreEqual(4, ChatBubbleLayout.WidthOf("ＡＢ"));
            Assert.AreEqual(2, ChatBubbleLayout.WidthOf("🙂"));
            Assert.AreEqual(6, ChatBubbleLayout.WidthOf("hi🙂ok"));
        }

        [Test]
        public void 幅を超えたら折り返す()
        {
            var lines = ChatBubbleLayout.Wrap("あいうえおかきくけこ", 8, 5);

            Assert.AreEqual(new[] { "あいうえ", "おかきく", "けこ" }, lines.ToArray());
        }

        [Test]
        public void 絵文字のサロゲートペアは割らない()
        {
            var lines = ChatBubbleLayout.Wrap("a🙂🙂🙂", 3, 5);

            Assert.AreEqual(new[] { "a🙂", "🙂", "🙂" }, lines.ToArray());
        }

        [Test]
        public void 行数を超えたら切って三点を付ける()
        {
            var lines = ChatBubbleLayout.Wrap(new string('a', 30), 10, 2);

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(new string('a', 10), lines[0]);
            Assert.AreEqual(new string('a', 10 - ChatBubbleLayout.WidthOf(ChatBubbleLayout.Ellipsis)) + ChatBubbleLayout.Ellipsis, lines[1]);
            Assert.LessOrEqual(ChatBubbleLayout.WidthOf(lines[1]), 10);
        }

        [Test]
        public void 空や幅0では行を作らない()
        {
            Assert.IsEmpty(ChatBubbleLayout.Wrap("", 10, 3));
            Assert.IsEmpty(ChatBubbleLayout.Wrap("abc", 0, 3));
            Assert.IsEmpty(ChatBubbleLayout.Wrap("abc", 10, 0));
        }

        [Test]
        public void 既定の設定で上限の長さの本文は行数の上限に収まる()
        {
            var config = ChatBubbleConfig.Default;
            var lines = ChatBubbleLayout.Wrap(new string('あ', ChatLimits.MaxLength), config.WrapColumns, config.MaxLines);

            Assert.AreEqual(config.MaxLines, lines.Count);
            Assert.IsTrue(lines[lines.Count - 1].EndsWith(ChatBubbleLayout.Ellipsis));
        }
    }
}
