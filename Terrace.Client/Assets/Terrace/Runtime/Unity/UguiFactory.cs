using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// uGUI の窓をコードで組み立てるための小道具。窓(~View)ごとに同じものを書かないよう、ここにまとめる。
    /// 位置は「親の左上を原点に、右下向き」の座標で置く(At)。
    /// </summary>
    public sealed class UguiFactory
    {
        public static readonly Color TextDark = new Color(0.12f, 0.12f, 0.18f);

        public UguiFactory()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public Font Font { get; }

        /// <summary>
        /// 窓を載せる Canvas を作る(1280x720 を基準に、幅と高さの半々で拡大縮小)。
        /// camera を渡すと Screen Space - Camera、渡さなければ Screen Space - Overlay。EventSystem が無ければ作る。
        /// </summary>
        public static GameObject CreateCanvas(string name, Camera? camera, int sortingOrder)
        {
            var canvasGo = new GameObject(name);
            var canvas = canvasGo.AddComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvasGo;
        }

        public Image NewImage(string name, Transform parent, Sprite? sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = true;
            return image;
        }

        public Text NewText(string name, Transform parent, string value, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Font;
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

        /// <summary>絵つきのボタン。label が空なら文字を置かない。onClick が null なら押しても何もしない。</summary>
        public Button NewButton(string name, Transform parent, Sprite? sprite, string label, UnityAction? onClick, int fontSize)
        {
            var image = NewImage(name, parent, sprite, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(label))
            {
                var text = NewText("Label", image.transform, label, fontSize, TextAnchor.MiddleCenter, TextDark, FontStyle.Bold);
                Stretch(text.rectTransform);
            }
            return button;
        }

        /// <summary>親の左上を原点に、右下向きの座標で置く。</summary>
        public static void At(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>親いっぱいに広げる。</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>親いっぱいから、左右 x・上下 y だけ内側に寄せる。</summary>
        public static void Inset(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
