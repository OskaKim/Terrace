using System;
using Terrace.Client.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// チャットの入力欄(uGUI をコードで組み立てる)。画面の左下に、入力中だけ出す。
    ///
    ///   [ チャット(Enter で送る / Esc でやめる)        ]
    ///
    /// ChatPresenter に言われた通りに出し入れし、Enter(開く・送る)と Esc・フォーカスが外れたこと(やめる)を知らせるだけ。
    /// 開けるか・送るかは ChatPresenter が決める。入力中に移動の入力を止めるのは起動役(GameBootstrap)。
    /// </summary>
    public sealed class ChatView : MonoBehaviour, IChatView
    {
        private const float Width = 560f;
        private const float Height = 36f;
        private const float Left = 16f;
        private const float Bottom = 6f;

        private UguiFactory _ui = null!;
        private ArtLibrary? _art;
        private GameObject? _bar;
        private InputField? _input;
        private bool _typing;
        private int _closedFrame = -1;

        public event Action? OpenPressed;
        public event Action<string>? Submitted;
        public event Action? CancelPressed;

        /// <summary>入力欄を出しているか。</summary>
        public bool IsOpen => _bar != null && _bar.activeSelf;

        /// <summary>入力欄の文字。</summary>
        public string Text => _input != null ? _input.text : string.Empty;

        public static ChatView Create(ArtLibrary? art)
        {
            var canvasGo = UguiFactory.CreateCanvas("ChatCanvas", null, 150);
            var view = canvasGo.AddComponent<ChatView>();
            view._art = art;
            view._ui = new UguiFactory();
            view.Build();
            return view;
        }

        // ---- テスト用 ----

        /// <summary>Enter で開いたことにする。</summary>
        public void PressOpen() => OpenPressed?.Invoke();

        /// <summary>入力欄に打ったことにする。</summary>
        public void Type(string text)
        {
            if (_input != null) _input.text = text;
        }

        /// <summary>Enter で送ったことにする。</summary>
        public void PressSubmit() => Submitted?.Invoke(Text);

        /// <summary>Esc でやめたことにする。</summary>
        public void PressCancel() => CancelPressed?.Invoke();

        private void Update()
        {
            // 閉じたのと同じフレームの Enter で、すぐ開き直さない
            if (_typing || Time.frameCount == _closedFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) OpenPressed?.Invoke();
        }

        // ---- IChatView ----

        public void SetTyping(bool typing)
        {
            if (_bar == null || _input == null || typing == _typing) return;
            _typing = typing;
            if (typing)
            {
                _bar.SetActive(true);
                _input.Select();
                _input.ActivateInputField();
            }
            else
            {
                // _typing を先に落とすので、ここで外したときの onEndEdit は知らせない
                _closedFrame = Time.frameCount;
                _input.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _input.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                _bar.SetActive(false);
            }
        }

        public void SetText(string text)
        {
            if (_input != null) _input.text = text;
        }

        public void SetCharacterLimit(int limit)
        {
            if (_input != null) _input.characterLimit = limit;
        }

        // ---- 組み立て ----

        private void Build()
        {
            var bar = _ui.NewImage("ChatBar", transform, _art?.Ui("panel_grey"), new Color(0.12f, 0.14f, 0.22f, 0.75f));
            var rect = bar.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(Left, Bottom);
            rect.sizeDelta = new Vector2(Width, Height);
            _bar = bar.gameObject;

            _input = _ui.NewInputField("ChatInput", bar.transform, _art?.Ui("input"), "チャット(Enter で送る / Esc でやめる)", 15);
            UguiFactory.Inset(_input.GetComponent<RectTransform>(), 3f, 3f);
            _input.onEndEdit.AddListener(OnEndEdit);

            _bar.SetActive(false);
        }

        /// <summary>入力が終わった。Enter なら送る、それ以外(Esc・フォーカスが外れた)ならやめる。</summary>
        private void OnEndEdit(string text)
        {
            if (!_typing) return;
            var keyboard = Keyboard.current;
            var submitted = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            if (submitted) Submitted?.Invoke(text);
            else CancelPressed?.Invoke();
        }
    }
}
