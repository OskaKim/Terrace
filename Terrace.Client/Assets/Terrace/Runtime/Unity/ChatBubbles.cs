using System;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Client.Presentation;
using Terrace.Shared;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// キャラクターの頭の上に出す吹き出し(自分の PlayerView と RemotePlayerView の子)。
    /// 文字の折り返しは ChatBubbleLayout、出しておく秒数と幅は ChatBubbleConfig。
    /// 親(キャラクター)は向きで左右に反転するので、文字が鏡文字にならないよう打ち消す。
    /// </summary>
    public sealed class ChatBubble : MonoBehaviour
    {
        /// <summary>キャラクターの足元から吹き出しの下端までの高さ(ユニット)。</summary>
        public const float OffsetY = 2.1f;

        /// <summary>1 行の高さ(ユニット)。半角 1 文字の幅はこの半分とみなす。</summary>
        public const float LineHeight = 0.34f;

        private const float Padding = 0.14f;
        private const int FontSize = 48;
        private const float CharacterSize = LineHeight * 10f / FontSize;

        private ChatBubbleConfig _config = ChatBubbleConfig.Default;
        private TextMesh? _text;
        private SpriteRenderer? _background;
        private float _remaining;

        /// <summary>出ているか。</summary>
        public bool IsShowing => gameObject.activeSelf && _remaining > 0f;

        /// <summary>出している文字(折り返した後)。</summary>
        public string Text => _text != null ? _text.text : string.Empty;

        /// <summary>owner(キャラクターの見た目)の子に、隠した吹き出しを作る。</summary>
        public static ChatBubble Attach(Transform owner, ChatBubbleConfig config)
        {
            var go = new GameObject("ChatBubble");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = new Vector3(0f, OffsetY, -0.5f);
            var bubble = go.AddComponent<ChatBubble>();
            bubble._config = config;
            bubble.Build();
            go.SetActive(false);
            return bubble;
        }

        public void Show(string text)
        {
            if (_text == null || _background == null) return;
            var lines = ChatBubbleLayout.Wrap(text, _config.WrapColumns, _config.MaxLines);
            if (lines.Count == 0) return;

            _text.text = string.Join("\n", lines);
            var columns = 0;
            foreach (var line in lines) columns = Math.Max(columns, ChatBubbleLayout.WidthOf(line));
            var width = columns * LineHeight * 0.5f + Padding * 2f;
            var height = lines.Count * LineHeight + Padding * 2f;
            // SpriteFactory の絵は下端の中央が原点なので、吹き出しの下端が OffsetY の高さに来る
            _background.transform.localScale = new Vector3(width, height, 1f);
            _background.transform.localPosition = Vector3.zero;
            _text.transform.localPosition = new Vector3(0f, height - Padding, -0.01f);

            _remaining = _config.Seconds;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _remaining = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) Hide();
        }

        private void LateUpdate()
        {
            // 親が左右に反転していたら打ち消す(文字は常に左から右へ読める向き)
            var parent = transform.parent;
            if (parent == null) return;
            transform.localScale = new Vector3(parent.localScale.x < 0f ? -1f : 1f, 1f, 1f);
        }

        private void Build()
        {
            _background = SpriteFactory.CreateRenderer("Background", transform, SpriteFactory.RoundedRect(), new Color(1f, 1f, 1f, 0.92f), 1f, 1f, 40);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(transform, false);
            _text = textGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            _text.fontSize = FontSize;
            _text.characterSize = CharacterSize;
            _text.anchor = TextAnchor.UpperCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = new Color(0.12f, 0.12f, 0.18f);
            var renderer = textGo.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = textGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 41;
        }
    }

    /// <summary>
    /// 届いたチャットを、同じマップにいる発言者の頭の上に出す。別のマップの人の発言は、メッセージ欄にだけ出る(GameNarrator)。
    /// </summary>
    public sealed class ChatBubbleSync : IDisposable
    {
        private readonly GameSimulation _simulation;
        private readonly PlayerView _self;
        private readonly RemotePlayersViewSync _remotes;
        private OnlineSession? _online;

        public ChatBubbleSync(GameSimulation simulation, PlayerView self, RemotePlayersViewSync remotes)
        {
            _simulation = simulation;
            _self = self;
            _remotes = remotes;
            _online = simulation.Online;
            if (_online != null) _online.ChatReceived += OnChat;
            simulation.WentOffline += OnWentOffline;
        }

        public void Dispose()
        {
            Unsubscribe();
            _simulation.WentOffline -= OnWentOffline;
        }

        private void OnChat(ChatNotification chat)
        {
            if (_online == null || chat.MapId != _simulation.Map.Id) return;
            var bubble = chat.PlayerId == _online.Self.PlayerId ? _self.Bubble : _remotes.Find(chat.PlayerId)?.Bubble;
            if (bubble != null) bubble.Show(chat.Text);
        }

        private void OnWentOffline(string reason) => Unsubscribe();

        private void Unsubscribe()
        {
            if (_online == null) return;
            _online.ChatReceived -= OnChat;
            _online = null;
        }
    }
}
