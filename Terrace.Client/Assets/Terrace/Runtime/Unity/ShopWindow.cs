using System.Collections.Generic;
using Terrace.Client.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// NPC の店の窓(uGUI をコードで組み立てる)。メイプルストーリーの店を手本に、
    /// 左に NPC の商品、右に自分の持ち物を並べ、買う・売る・店を出るのボタンを置く。
    /// 表示は GameSimulation の ShopSession を読むだけで、勘定は Core 側が行う。
    /// </summary>
    public sealed class ShopWindow : MonoBehaviour
    {
        private const float WindowWidth = 800f;
        private const float WindowHeight = 560f;
        private const float RowHeight = 46f;

        private static readonly Color TextDark = new Color(0.12f, 0.12f, 0.18f);
        private static readonly Color RowNormal = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color RowSelected = new Color(0.75f, 0.85f, 1f, 1f);

        private GameSimulation? _simulation;
        private ShopSession? _session;
        private ArtLibrary? _art;
        private Font? _font;

        private GameObject? _root;
        private Text? _title;
        private Text? _npcName;
        private Text? _shopName;
        private Text? _meso;
        private Text? _hint;
        private Image? _npcPortrait;
        private Image? _oneClickCheck;
        private RectTransform? _goodsContent;
        private RectTransform? _inventoryContent;
        private readonly List<Image> _goodsRows = new List<Image>();
        private readonly List<Image> _inventoryRows = new List<Image>();
        private readonly List<Button> _inventoryTabs = new List<Button>();

        private Sprite? _panel;
        private Sprite? _panelLine;
        private Sprite? _buttonGrey;
        private Sprite? _buttonBlue;
        private Sprite? _buttonYellow;
        private Sprite? _panelYellow;
        private Sprite? _checkOn;
        private Sprite? _checkOff;
        private Sprite? _coin;

        private ItemKind _inventoryTab = ItemKind.Use;
        private bool _oneClickSell;

        public bool IsOpen => _root != null && _root.activeSelf;
        public ShopSession? Session => _session;
        public int SelectedGoodsItemId { get; private set; } = -1;
        public int SelectedInventoryItemId { get; private set; } = -1;
        public ItemKind InventoryTab => _inventoryTab;
        public int GoodsRowCount => _goodsRows.Count;
        public int InventoryRowCount => _inventoryRows.Count;

        public static ShopWindow Create(Camera camera, ArtLibrary? art)
        {
            var canvasGo = new GameObject("ShopCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var window = canvasGo.AddComponent<ShopWindow>();
            window._art = art;
            window.LoadResources();
            window.BuildHierarchy();
            window._root!.SetActive(false);
            return window;
        }

        public void Bind(GameSimulation simulation)
        {
            _simulation = simulation;
            simulation.Trading.ShopOpened += Open;
            simulation.Trading.ShopClosed += Close;
            if (simulation.Trading.ActiveShop != null) Open(simulation.Trading.ActiveShop);
        }

        private void OnDestroy()
        {
            if (_simulation != null)
            {
                _simulation.Trading.ShopOpened -= Open;
                _simulation.Trading.ShopClosed -= Close;
            }
        }

        private void Update()
        {
            if (!IsOpen || _simulation == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _simulation.Trading.CloseShop();
            }
        }

        // ---- 開閉と更新 ----

        private void Open(ShopSession session)
        {
            if (_session != null) _session.Changed -= Refresh;
            _session = session;
            _session.Changed += Refresh;
            SelectedGoodsItemId = -1;
            SelectedInventoryItemId = -1;
            _root!.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if (_session != null) _session.Changed -= Refresh;
            _session = null;
            if (_root != null) _root.SetActive(false);
        }

        public void SelectInventoryTab(ItemKind kind)
        {
            _inventoryTab = kind;
            SelectedInventoryItemId = -1;
            Refresh();
        }

        public void Refresh()
        {
            if (_session == null || _root == null) return;
            var npc = _session.Npc;
            _title!.text = $"{npc.Name} の店";
            _npcName!.text = npc.Name;
            _shopName!.text = _session.Shop.Name;
            _meso!.text = $"{_session.Player.Meso:N0}";
            if (_npcPortrait != null)
            {
                var portrait = _art?.NpcSprite(npc.Sprite);
                _npcPortrait.sprite = portrait;
                _npcPortrait.enabled = portrait != null;
                _npcPortrait.preserveAspect = true;
            }
            if (_oneClickCheck != null) _oneClickCheck.sprite = _oneClickSell ? _checkOn : _checkOff;

            for (var i = 0; i < _inventoryTabs.Count; i++)
            {
                var image = _inventoryTabs[i].GetComponent<Image>();
                if (image != null) image.sprite = (ItemKind)i == _inventoryTab ? _panelYellow ?? _buttonYellow : _buttonGrey;
            }

            RebuildGoods();
            RebuildInventory();
            _hint!.text = _oneClickSell
                ? "持ち物をクリックすると即売却。商品をクリックして選び「買う」。Esc で閉じる"
                : "行をクリックして選び、「買う」「売る」を押す。Esc で閉じる";
        }

        private void RebuildGoods()
        {
            ClearRows(_goodsContent!, _goodsRows);
            var goods = _session!.Goods;
            for (var i = 0; i < goods.Count; i++)
            {
                var entry = goods[i];
                var itemId = entry.Item.ItemId;
                var row = NewRow(_goodsContent!, entry.Item, $"{entry.Price:N0} メソ", null, () =>
                {
                    SelectedGoodsItemId = itemId;
                    HighlightRows();
                });
                _goodsRows.Add(row);
            }
            HighlightRows();
        }

        private void RebuildInventory()
        {
            ClearRows(_inventoryContent!, _inventoryRows);
            var entries = _session!.Inventory(_inventoryTab);
            foreach (var entry in entries)
            {
                var itemId = entry.Item.ItemId;
                var row = NewRow(_inventoryContent!, entry.Item, $"{entry.SellPrice:N0} メソ", $"x{entry.Count}", () =>
                {
                    SelectedInventoryItemId = itemId;
                    HighlightRows();
                    if (_oneClickSell) _simulation?.Trading.Sell(itemId);
                });
                _inventoryRows.Add(row);
            }
            HighlightRows();
        }

        private void HighlightRows()
        {
            var goods = _session?.Goods;
            for (var i = 0; i < _goodsRows.Count && goods != null && i < goods.Count; i++)
            {
                _goodsRows[i].color = goods[i].Item.ItemId == SelectedGoodsItemId ? RowSelected : RowNormal;
            }
            var inventory = _session?.Inventory(_inventoryTab);
            for (var i = 0; i < _inventoryRows.Count && inventory != null && i < inventory.Count; i++)
            {
                _inventoryRows[i].color = inventory[i].Item.ItemId == SelectedInventoryItemId ? RowSelected : RowNormal;
            }
        }

        // ---- ボタン ----

        public void ClickBuy()
        {
            if (_simulation == null || _session == null) return;
            if (SelectedGoodsItemId < 0)
            {
                _simulation.Messages.Add(_simulation.Time, "買う商品をクリックして選んでください");
                return;
            }
            _simulation.Trading.Buy(SelectedGoodsItemId);
        }

        public void ClickSell()
        {
            if (_simulation == null || _session == null) return;
            if (SelectedInventoryItemId < 0)
            {
                _simulation.Messages.Add(_simulation.Time, "売る持ち物をクリックして選んでください");
                return;
            }
            _simulation.Trading.Sell(SelectedInventoryItemId);
            if (_session.Player.CountOf(SelectedInventoryItemId) == 0) SelectedInventoryItemId = -1;
        }

        public void ClickLeave() => _simulation?.Trading.CloseShop();

        public void ToggleOneClickSell()
        {
            _oneClickSell = !_oneClickSell;
            Refresh();
        }

        // ---- 組み立て ----

        private void LoadResources()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _panel = _art?.Ui("panel_grey");
            _panelLine = _art?.Ui("panel_line_grey");
            _buttonGrey = _art?.Ui("button_grey");
            _buttonBlue = _art?.Ui("button_blue");
            _buttonYellow = _art?.Ui("button_yellow");
            _panelYellow = _art?.Ui("panel_yellow");
            _checkOn = _art?.Ui("check_on");
            _checkOff = _art?.Ui("check_off");
            _coin = _art?.Hud("hud_coins");
        }

        private void BuildHierarchy()
        {
            var root = NewImage("ShopWindow", transform, _panel, new Color(0.96f, 0.96f, 0.99f, 0.98f));
            _root = root.gameObject;
            var rootRect = root.rectTransform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);

            // タイトルバー
            var titleBar = NewImage("TitleBar", root.transform, _panelLine, new Color(0.30f, 0.45f, 0.75f, 1f));
            At(titleBar.rectTransform, 8, 8, WindowWidth - 16, 36);
            _title = NewText("Title", titleBar.transform, "店", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(_title.rectTransform);
            var close = NewButton("Close", titleBar.transform, _buttonGrey, "×", ClickLeave, 16);
            At(close.GetComponent<RectTransform>(), WindowWidth - 16 - 40, 4, 34, 28);

            // 左: NPC の商品
            var left = NewImage("NpcPane", root.transform, _panelLine, new Color(1f, 1f, 1f, 0.55f));
            At(left.rectTransform, 12, 52, 380, 464);
            _npcPortrait = NewImage("Portrait", left.transform, null, Color.white);
            At(_npcPortrait.rectTransform, 14, 12, 66, 92);
            _npcName = NewText("NpcName", left.transform, "NPC", 16, TextAnchor.MiddleLeft, TextDark, FontStyle.Bold);
            At(_npcName.rectTransform, 92, 12, 160, 24);
            _shopName = NewText("ShopName", left.transform, "", 13, TextAnchor.MiddleLeft, new Color(0.35f, 0.35f, 0.45f));
            At(_shopName.rectTransform, 92, 36, 160, 22);

            var oneClick = NewButton("OneClick", left.transform, null, "", ToggleOneClickSell, 12);
            At(oneClick.GetComponent<RectTransform>(), 92, 64, 24, 24);
            _oneClickCheck = oneClick.GetComponent<Image>();
            _oneClickCheck.sprite = _checkOff;
            _oneClickCheck.color = Color.white;
            var oneClickLabel = NewText("OneClickLabel", left.transform, "ワンクリックで売却", 12, TextAnchor.MiddleLeft, TextDark);
            At(oneClickLabel.rectTransform, 120, 64, 150, 24);

            var leave = NewButton("Leave", left.transform, _buttonGrey, "店を出る", ClickLeave, 13);
            At(leave.GetComponent<RectTransform>(), 258, 12, 108, 32);
            var buy = NewButton("Buy", left.transform, _buttonBlue, "アイテムを買う", ClickBuy, 13);
            At(buy.GetComponent<RectTransform>(), 258, 50, 108, 32);

            var goodsTab = NewButton("GoodsTab", left.transform, _panelYellow ?? _buttonYellow, "商品", 13, null);
            At(goodsTab.GetComponent<RectTransform>(), 14, 108, 90, 28);
            var specialTab = NewButton("SpecialTab", left.transform, _buttonGrey, "スペシャル", 13, null);
            At(specialTab.GetComponent<RectTransform>(), 108, 108, 90, 28);

            _goodsContent = NewScrollList("Goods", left.transform, 14, 142, 352, 308);

            // 右: 自分の持ち物
            var right = NewImage("PlayerPane", root.transform, _panelLine, new Color(1f, 1f, 1f, 0.55f));
            At(right.rectTransform, 408, 52, 380, 464);
            var playerPortrait = NewImage("Portrait", right.transform, _art?.Hud("hud_p1"), Color.white);
            At(playerPortrait.rectTransform, 14, 12, 60, 60);
            playerPortrait.preserveAspect = true;
            playerPortrait.enabled = playerPortrait.sprite != null;
            var mesoBox = NewImage("MesoBox", right.transform, _panelLine, new Color(0.98f, 0.98f, 0.9f, 1f));
            At(mesoBox.rectTransform, 86, 24, 160, 30);
            var coinIcon = NewImage("Coin", mesoBox.transform, _coin, Color.white);
            At(coinIcon.rectTransform, 6, 5, 20, 20);
            coinIcon.enabled = _coin != null;
            _meso = NewText("Meso", mesoBox.transform, "0", 15, TextAnchor.MiddleRight, TextDark, FontStyle.Bold);
            At(_meso.rectTransform, 30, 2, 122, 26);

            var sell = NewButton("Sell", right.transform, _buttonYellow, "アイテムを売る", ClickSell, 13);
            At(sell.GetComponent<RectTransform>(), 258, 24, 108, 32);

            _inventoryTabs.Clear();
            var tabNames = new[] { "装備", "消費", "その他" };
            for (var i = 0; i < tabNames.Length; i++)
            {
                var kind = (ItemKind)i;
                var tab = NewButton($"Tab{kind}", right.transform, _buttonGrey, tabNames[i], () => SelectInventoryTab(kind), 13);
                At(tab.GetComponent<RectTransform>(), 14 + i * 94, 108, 90, 28);
                _inventoryTabs.Add(tab);
            }

            _inventoryContent = NewScrollList("Inventory", right.transform, 14, 142, 352, 308);

            _hint = NewText("Hint", root.transform, "", 12, TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.4f));
            At(_hint.rectTransform, 12, WindowHeight - 36, WindowWidth - 24, 24);
        }

        private RectTransform NewScrollList(string name, Transform parent, float x, float y, float width, float height)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            At(scrollRect, x, y, width, height);
            var scroll = scrollGo.AddComponent<ScrollRect>();

            var viewport = NewImage("Viewport", scrollGo.transform, _panelLine, new Color(1f, 1f, 1f, 0.7f));
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return content;
        }

        private Image NewRow(RectTransform content, ItemInfo item, string priceText, string? countText, UnityAction onClick)
        {
            var row = NewImage($"Row {item.Name}", content, _buttonGrey, RowNormal);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            button.onClick.AddListener(onClick);

            var icon = NewImage("Icon", row.transform, _art?.Item(item.ItemId), Color.white);
            At(icon.rectTransform, 8, 5, 36, 36);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;

            var nameLabel = NewText("Name", row.transform, countText == null ? item.Name : $"{item.Name}  {countText}", 14, TextAnchor.MiddleLeft, TextDark);
            At(nameLabel.rectTransform, 52, 4, 190, RowHeight - 8);

            var priceLabel = NewText("Price", row.transform, priceText, 13, TextAnchor.MiddleRight, new Color(0.2f, 0.3f, 0.2f));
            At(priceLabel.rectTransform, 220, 4, 100, RowHeight - 8);
            if (_coin != null)
            {
                var coin = NewImage("Coin", row.transform, _coin, Color.white);
                At(coin.rectTransform, 324, 13, 18, 18);
            }
            return row;
        }

        private static void ClearRows(RectTransform content, List<Image> rows)
        {
            foreach (var row in rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            rows.Clear();
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        private Image NewImage(string name, Transform parent, Sprite? sprite, Color color)
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

        private Text NewText(string name, Transform parent, string text, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = color;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Button NewButton(string name, Transform parent, Sprite? sprite, string label, UnityAction? onClick, int fontSize)
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

        private Button NewButton(string name, Transform parent, Sprite? sprite, string label, int fontSize, UnityAction? onClick)
            => NewButton(name, parent, sprite, label, onClick, fontSize);

        /// <summary>親の左上を原点に、右下向きの座標で置く。</summary>
        private static void At(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
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

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
