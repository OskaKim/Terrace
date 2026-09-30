using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 起動時の窓。名前と接続先を入れて「オンラインで遊ぶ」か「ひとりで遊ぶ」を選ぶ。uGUI をコードで組み立てる。
    ///
    ///   ┌──────────── Terrace ────────────┐
    ///   │ 名前     [旅人123            ]  │
    ///   │ サーバー [http://localhost:5000]  │
    ///   │ [ オンラインで遊ぶ ] [ ひとりで遊ぶ ] │
    ///   │ 状態の一行(接続中… / 失敗の理由)     │
    ///   └────────────────────────────────┘
    ///
    /// 入力は PlayerPrefs に覚える。押された結果はイベントで GameBootstrap に渡すだけで、接続そのものはしない。
    /// </summary>
    public sealed class LoginWindow : MonoBehaviour
    {
        private const float Width = 520f;
        private const float Height = 330f;
        private static readonly Color TextDark = new Color(0.12f, 0.12f, 0.18f);

        private Font? _font;
        private ArtLibrary? _art;
        private GameObject? _root;
        private InputField? _name;
        private InputField? _server;
        private Button? _online;
        private Button? _offline;
        private Text? _status;

        /// <summary>オンラインが選ばれた(名前, 接続先)。</summary>
        public event Action<string, string>? OnlineRequested;

        /// <summary>ひとりで遊ぶが選ばれた。</summary>
        public event Action? OfflineRequested;

        public bool IsOpen => _root != null && _root.activeSelf;
        public string PlayerName => _name != null ? _name.text : string.Empty;
        public string ServerAddress => _server != null ? _server.text : string.Empty;

        public static LoginWindow Create(ArtLibrary? art, string playerName, string serverAddress)
        {
            var canvasGo = new GameObject("LoginCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var window = canvasGo.AddComponent<LoginWindow>();
            window._art = art;
            window._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            window.Build(playerName, serverAddress);
            return window;
        }

        /// <summary>接続中などで押せなくする。</summary>
        public void SetBusy(bool busy, string status)
        {
            if (_online != null) _online.interactable = !busy;
            if (_offline != null) _offline.interactable = !busy;
            if (_name != null) _name.interactable = !busy;
            if (_server != null) _server.interactable = !busy;
            SetStatus(status, false);
        }

        public void SetStatus(string text, bool isError)
        {
            if (_status == null) return;
            _status.text = text;
            _status.color = isError ? new Color(0.8f, 0.15f, 0.15f) : new Color(0.25f, 0.3f, 0.45f);
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        /// <summary>「オンラインで遊ぶ」を押したことにする(テスト用)。</summary>
        public void ClickOnline()
        {
            if (_online != null && !_online.interactable) return;
            OnlineRequested?.Invoke(PlayerName.Trim(), ServerAddress.Trim());
        }

        /// <summary>「ひとりで遊ぶ」を押したことにする(テスト用)。</summary>
        public void ClickOffline()
        {
            if (_offline != null && !_offline.interactable) return;
            OfflineRequested?.Invoke();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !IsOpen) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ClickOnline();
        }

        // ---- 組み立て ----

        private void Build(string playerName, string serverAddress)
        {
            // 背景を少し暗くする
            var shade = NewImage("Shade", transform, null, new Color(0f, 0f, 0f, 0.45f));
            Stretch(shade.rectTransform);
            _root = shade.gameObject;

            var panel = NewImage("LoginWindow", shade.transform, _art?.Ui("panel_grey"), new Color(0.96f, 0.96f, 0.99f, 0.98f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Width, Height);

            var titleBar = NewImage("TitleBar", panel.transform, _art?.Ui("panel_line_grey"), new Color(0.30f, 0.45f, 0.75f, 1f));
            At(titleBar.rectTransform, 8, 8, Width - 16, 48);
            var title = NewText("Title", titleBar.transform, "Terrace", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(title.rectTransform);

            var nameLabel = NewText("NameLabel", panel.transform, "名前", 16, TextAnchor.MiddleLeft, TextDark, FontStyle.Bold);
            At(nameLabel.rectTransform, 32, 82, 100, 40);
            _name = NewInput("Name", panel.transform, playerName, "表示名(16 文字まで)");
            _name.characterLimit = 16;
            At(_name.GetComponent<RectTransform>(), 132, 82, Width - 164, 40);

            var serverLabel = NewText("ServerLabel", panel.transform, "サーバー", 16, TextAnchor.MiddleLeft, TextDark, FontStyle.Bold);
            At(serverLabel.rectTransform, 32, 134, 100, 40);
            _server = NewInput("Server", panel.transform, serverAddress, "http://localhost:5000");
            At(_server.GetComponent<RectTransform>(), 132, 134, Width - 164, 40);

            _online = NewButton("Online", panel.transform, _art?.Ui("button_blue"), "オンラインで遊ぶ", ClickOnline);
            At(_online.GetComponent<RectTransform>(), 32, 200, 220, 50);
            _offline = NewButton("Offline", panel.transform, _art?.Ui("button_grey"), "ひとりで遊ぶ", ClickOffline);
            At(_offline.GetComponent<RectTransform>(), Width - 32 - 220, 200, 220, 50);

            _status = NewText("Status", panel.transform, "", 14, TextAnchor.MiddleCenter, TextDark);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            At(_status.rectTransform, 24, 262, Width - 48, 52);
            SetStatus("サーバーを立てていなくても「ひとりで遊ぶ」で遊べます", false);
        }

        private InputField NewInput(string name, Transform parent, string value, string placeholder)
        {
            var background = NewImage(name, parent, _art?.Ui("input"), Color.white);
            var input = background.gameObject.AddComponent<InputField>();

            var text = NewText("Text", background.transform, string.Empty, 16, TextAnchor.MiddleLeft, TextDark);
            text.supportRichText = false;
            Inset(text.rectTransform, 12f, 4f);
            var hint = NewText("Placeholder", background.transform, placeholder, 16, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.55f));
            hint.fontStyle = FontStyle.Italic;
            Inset(hint.rectTransform, 12f, 4f);

            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.text = value;
            return input;
        }

        private Image NewImage(string name, Transform parent, Sprite? sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private Text NewText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button NewButton(string name, Transform parent, Sprite? sprite, string label, UnityAction onClick)
        {
            var image = NewImage(name, parent, sprite, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = NewText("Label", image.transform, label, 17, TextAnchor.MiddleCenter, TextDark, FontStyle.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        private static void At(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Inset(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
