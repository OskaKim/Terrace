using System;
using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Client.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// NPC の店の窓(uGUI をコードで組み立てる)。メイプルストーリーの店を手本に、
    /// 左に NPC の商品、右に自分の持ち物を並べ、買う・売る・店を出るのボタンを置く。
    /// ShopPresenter に言われた通りに描き、押されたこと(と Esc)を知らせるだけ。何を出すか・押されたら何をするかは ShopPresenter。
    /// </summary>
    public sealed class ShopView : MonoBehaviour, IShopView
    {
        private const float WindowWidth = 800f;
        private const float WindowHeight = 560f;
        private const float RowHeight = 46f;

        private static readonly Color RowNormal = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color RowSelected = new Color(0.75f, 0.85f, 1f, 1f);

        private UguiFactory _ui = null!;
        private ArtLibrary? _art;

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
        private readonly List<(int ItemId, Image Row)> _goodsRows = new List<(int, Image)>();
        private readonly List<(int ItemId, Image Row)> _inventoryRows = new List<(int, Image)>();
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

        public event Action<int>? GoodsClicked;
        public event Action<int>? InventoryClicked;
        public event Action<ItemKind>? TabClicked;
        public event Action? BuyClicked;
        public event Action? SellClicked;
        public event Action? LeaveClicked;
        public event Action? OneClickSellToggled;
        public event Action? CancelPressed;

        public bool IsOpen => _root != null && _root.activeSelf;
        public int GoodsRowCount => _goodsRows.Count;
        public int InventoryRowCount => _inventoryRows.Count;

        public static ShopView Create(Camera camera, ArtLibrary? art)
        {
            var canvasGo = UguiFactory.CreateCanvas("ShopCanvas", camera, 100);
            var view = canvasGo.AddComponent<ShopView>();
            view._ui = new UguiFactory();
            view._art = art;
            view.LoadResources();
            view.BuildHierarchy();
            view._root!.SetActive(false);
            return view;
        }

        private void Update()
        {
            if (!IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CancelPressed?.Invoke();
        }

        // ---- IShopView ----

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetHeader(string title, string npcName, string shopName, string? npcSprite)
        {
            _title!.text = title;
            _npcName!.text = npcName;
            _shopName!.text = shopName;
            if (_npcPortrait != null)
            {
                var portrait = _art?.NpcSprite(npcSprite);
                _npcPortrait.sprite = portrait;
                _npcPortrait.enabled = portrait != null;
                _npcPortrait.preserveAspect = true;
            }
        }

        public void SetMeso(string meso) => _meso!.text = meso;

        public void SetOneClickSell(bool on)
        {
            if (_oneClickCheck != null) _oneClickCheck.sprite = on ? _checkOn : _checkOff;
        }

        public void SetInventoryTab(ItemKind tab)
        {
            for (var i = 0; i < _inventoryTabs.Count; i++)
            {
                var image = _inventoryTabs[i].GetComponent<Image>();
                if (image != null) image.sprite = (ItemKind)i == tab ? _panelYellow ?? _buttonYellow : _buttonGrey;
            }
        }

        public void SetGoods(IReadOnlyList<ShopRow> rows) => RebuildRows(_goodsContent!, _goodsRows, rows, id => GoodsClicked?.Invoke(id));

        public void SetInventory(IReadOnlyList<ShopRow> rows) => RebuildRows(_inventoryContent!, _inventoryRows, rows, id => InventoryClicked?.Invoke(id));

        public void SetSelection(int goodsItemId, int inventoryItemId)
        {
            foreach (var (itemId, row) in _goodsRows) row.color = itemId == goodsItemId ? RowSelected : RowNormal;
            foreach (var (itemId, row) in _inventoryRows) row.color = itemId == inventoryItemId ? RowSelected : RowNormal;
        }

        public void SetHint(string hint) => _hint!.text = hint;

        // ---- 組み立て ----

        private void RebuildRows(RectTransform content, List<(int ItemId, Image Row)> rows, IReadOnlyList<ShopRow> data, Action<int> clicked)
        {
            ClearRows(content, rows);
            foreach (var entry in data)
            {
                var itemId = entry.ItemId;
                rows.Add((itemId, NewRow(content, entry, () => clicked(itemId))));
            }
        }

        private void LoadResources()
        {
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
            var textDark = UguiFactory.TextDark;
            var root = _ui.NewImage("ShopWindow", transform, _panel, new Color(0.96f, 0.96f, 0.99f, 0.98f));
            _root = root.gameObject;
            var rootRect = root.rectTransform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);

            // タイトルバー
            var titleBar = _ui.NewImage("TitleBar", root.transform, _panelLine, new Color(0.30f, 0.45f, 0.75f, 1f));
            UguiFactory.At(titleBar.rectTransform, 8, 8, WindowWidth - 16, 36);
            _title = _ui.NewText("Title", titleBar.transform, "店", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UguiFactory.Stretch(_title.rectTransform);
            var close = _ui.NewButton("Close", titleBar.transform, _buttonGrey, "×", () => LeaveClicked?.Invoke(), 16);
            UguiFactory.At(close.GetComponent<RectTransform>(), WindowWidth - 16 - 40, 4, 34, 28);

            // 左: NPC の商品
            var left = _ui.NewImage("NpcPane", root.transform, _panelLine, new Color(1f, 1f, 1f, 0.55f));
            UguiFactory.At(left.rectTransform, 12, 52, 380, 464);
            _npcPortrait = _ui.NewImage("Portrait", left.transform, null, Color.white);
            UguiFactory.At(_npcPortrait.rectTransform, 14, 12, 66, 92);
            _npcName = _ui.NewText("NpcName", left.transform, "NPC", 16, TextAnchor.MiddleLeft, textDark, FontStyle.Bold);
            UguiFactory.At(_npcName.rectTransform, 92, 12, 160, 24);
            _shopName = _ui.NewText("ShopName", left.transform, "", 13, TextAnchor.MiddleLeft, new Color(0.35f, 0.35f, 0.45f));
            UguiFactory.At(_shopName.rectTransform, 92, 36, 160, 22);

            var oneClick = _ui.NewButton("OneClick", left.transform, null, "", () => OneClickSellToggled?.Invoke(), 12);
            UguiFactory.At(oneClick.GetComponent<RectTransform>(), 92, 64, 24, 24);
            _oneClickCheck = oneClick.GetComponent<Image>();
            _oneClickCheck.sprite = _checkOff;
            _oneClickCheck.color = Color.white;
            var oneClickLabel = _ui.NewText("OneClickLabel", left.transform, "ワンクリックで売却", 12, TextAnchor.MiddleLeft, textDark);
            UguiFactory.At(oneClickLabel.rectTransform, 120, 64, 150, 24);

            var leave = _ui.NewButton("Leave", left.transform, _buttonGrey, "店を出る", () => LeaveClicked?.Invoke(), 13);
            UguiFactory.At(leave.GetComponent<RectTransform>(), 258, 12, 108, 32);
            var buy = _ui.NewButton("Buy", left.transform, _buttonBlue, "アイテムを買う", () => BuyClicked?.Invoke(), 13);
            UguiFactory.At(buy.GetComponent<RectTransform>(), 258, 50, 108, 32);

            var goodsTab = _ui.NewButton("GoodsTab", left.transform, _panelYellow ?? _buttonYellow, "商品", null, 13);
            UguiFactory.At(goodsTab.GetComponent<RectTransform>(), 14, 108, 90, 28);
            var specialTab = _ui.NewButton("SpecialTab", left.transform, _buttonGrey, "スペシャル", null, 13);
            UguiFactory.At(specialTab.GetComponent<RectTransform>(), 108, 108, 90, 28);

            _goodsContent = NewScrollList("Goods", left.transform, 14, 142, 352, 308);

            // 右: 自分の持ち物
            var right = _ui.NewImage("PlayerPane", root.transform, _panelLine, new Color(1f, 1f, 1f, 0.55f));
            UguiFactory.At(right.rectTransform, 408, 52, 380, 464);
            var playerPortrait = _ui.NewImage("Portrait", right.transform, _art?.Hud("hud_p1"), Color.white);
            UguiFactory.At(playerPortrait.rectTransform, 14, 12, 60, 60);
            playerPortrait.preserveAspect = true;
            playerPortrait.enabled = playerPortrait.sprite != null;
            var mesoBox = _ui.NewImage("MesoBox", right.transform, _panelLine, new Color(0.98f, 0.98f, 0.9f, 1f));
            UguiFactory.At(mesoBox.rectTransform, 86, 24, 160, 30);
            var coinIcon = _ui.NewImage("Coin", mesoBox.transform, _coin, Color.white);
            UguiFactory.At(coinIcon.rectTransform, 6, 5, 20, 20);
            coinIcon.enabled = _coin != null;
            _meso = _ui.NewText("Meso", mesoBox.transform, "0", 15, TextAnchor.MiddleRight, textDark, FontStyle.Bold);
            UguiFactory.At(_meso.rectTransform, 30, 2, 122, 26);

            var sell = _ui.NewButton("Sell", right.transform, _buttonYellow, "アイテムを売る", () => SellClicked?.Invoke(), 13);
            UguiFactory.At(sell.GetComponent<RectTransform>(), 258, 24, 108, 32);

            _inventoryTabs.Clear();
            var tabNames = new[] { "装備", "消費", "その他" };
            for (var i = 0; i < tabNames.Length; i++)
            {
                var kind = (ItemKind)i;
                var tab = _ui.NewButton($"Tab{kind}", right.transform, _buttonGrey, tabNames[i], () => TabClicked?.Invoke(kind), 13);
                UguiFactory.At(tab.GetComponent<RectTransform>(), 14 + i * 94, 108, 90, 28);
                _inventoryTabs.Add(tab);
            }

            _inventoryContent = NewScrollList("Inventory", right.transform, 14, 142, 352, 308);

            _hint = _ui.NewText("Hint", root.transform, "", 12, TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.4f));
            UguiFactory.At(_hint.rectTransform, 12, WindowHeight - 36, WindowWidth - 24, 24);
        }

        private RectTransform NewScrollList(string name, Transform parent, float x, float y, float width, float height)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            UguiFactory.At(scrollRect, x, y, width, height);
            var scroll = scrollGo.AddComponent<ScrollRect>();

            var viewport = _ui.NewImage("Viewport", scrollGo.transform, _panelLine, new Color(1f, 1f, 1f, 0.7f));
            UguiFactory.Stretch(viewport.rectTransform);
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

        private Image NewRow(RectTransform content, ShopRow entry, UnityAction onClick)
        {
            var row = _ui.NewImage($"Row {entry.ItemName}", content, _buttonGrey, RowNormal);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            button.onClick.AddListener(onClick);

            var icon = _ui.NewImage("Icon", row.transform, _art?.Item(entry.ItemId), Color.white);
            UguiFactory.At(icon.rectTransform, 8, 5, 36, 36);
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;

            var nameLabel = _ui.NewText("Name", row.transform, entry.Label, 14, TextAnchor.MiddleLeft, UguiFactory.TextDark);
            UguiFactory.At(nameLabel.rectTransform, 52, 4, 190, RowHeight - 8);

            var priceLabel = _ui.NewText("Price", row.transform, entry.PriceText, 13, TextAnchor.MiddleRight, new Color(0.2f, 0.3f, 0.2f));
            UguiFactory.At(priceLabel.rectTransform, 220, 4, 100, RowHeight - 8);
            if (_coin != null)
            {
                var coin = _ui.NewImage("Coin", row.transform, _coin, Color.white);
                UguiFactory.At(coin.rectTransform, 324, 13, 18, 18);
            }
            return row;
        }

        private static void ClearRows(RectTransform content, List<(int ItemId, Image Row)> rows)
        {
            foreach (var (_, row) in rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            rows.Clear();
            for (var i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }
    }
}
