using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>
    /// Order sheet: line items with quantity controls, subtotal and a local "confirm order" flow.
    /// There is no backend – the confirmation screen says so explicitly.
    /// </summary>
    public sealed class CartSheet : UIScreen
    {
        readonly RectTransform mPanel, mList, mEmpty, mFooter, mOrderView, mConfirmed;
        readonly Image mBackdrop;
        readonly TextMeshProUGUI mHeaderCount, mSubtotal, mNote, mOrderNumber, mOrderSummary;
        readonly Dictionary<string, CartRow> mRows = new Dictionary<string, CartRow>();

        public Button CloseButton { get; }
        public Button ConfirmButton { get; }
        public Button ClearButton { get; }
        public Button DoneButton { get; }
        public Button BrowseButton { get; }
        public bool ShowingConfirmation => mConfirmed.gameObject.activeSelf;
        public int RowCount => mRows.Count;
        public IReadOnlyDictionary<string, CartRow> Rows => mRows;

        public CartSheet(ARMenuApp app, RectTransform parent) : base(app, "CartSheet", parent)
        {
            mBackdrop = UI.Image(Root, "Backdrop", UISprites.White, new Color(0, 0, 0, 0.72f), true);
            mBackdrop.rectTransform.Stretch();
            UI.Button(mBackdrop, App.Back, false);

            var panel = UI.Rounded(Root, "Panel", T.background, 44, true);
            mPanel = panel.rectTransform;
            mPanel.anchorMin = new Vector2(0, 0);
            mPanel.anchorMax = new Vector2(1, 0.86f);
            mPanel.pivot = new Vector2(0.5f, 0f);
            mPanel.offsetMin = new Vector2(0, -48);
            mPanel.offsetMax = Vector2.zero;
            var safe = UI.Rect("Safe", mPanel).Stretch(0, 0, 0, 48);
            safe.gameObject.AddComponent<SafeAreaFitter>().top = false;

            var grabber = UI.Image(mPanel, "Grabber", UISprites.Rounded, UITheme.WithAlpha(T.textMuted, 0.6f));
            UI.MakeRounded(grabber, 4);
            grabber.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(88, 8), new Vector2(0, -18));

            // ---- order view
            mOrderView = UI.Rect("OrderView", safe).Stretch();
            var header = UI.Rect("Header", mOrderView).Strip(true, 150);
            UI.HStack(header.gameObject, 16, new RectOffset(48, 40, 40, 0), TextAnchor.MiddleLeft);
            var titles = UI.Rect("Titles", header);
            UI.VStack(titles.gameObject, 2);
            UI.Size(titles.gameObject, -1, -1, 1f);
            UI.Eyebrow(titles, "Your order", T.accent);
            mHeaderCount = UI.Text(titles, "Count", "", T.Display, 48f, T.textPrimary);
            CloseButton = UI.CircleButton(header, "Close", UISprites.Close, T.surfaceHigh, T.textPrimary, 88, App.Back);

            mList = UI.VerticalScroll(mOrderView, "Lines", out var scroll, new RectOffset(40, 40, 8, 24), 18);
            ((RectTransform)scroll.transform).Stretch(0, 0, 150, 330);

            mEmpty = UI.Rect("Empty", mOrderView).Stretch(0, 0, 150, 0);
            UI.VStack(mEmpty.gameObject, 18, new RectOffset(80, 80, 120, 0), TextAnchor.UpperCenter);
            UI.Icon(mEmpty, UISprites.Cart, T.textMuted, 120);
            UI.Text(mEmpty, "Title", "Your order is empty", T.Display, 46f, T.textPrimary, TextAlignmentOptions.Center);
            UI.Text(mEmpty, "Body", "Add dishes from the menu or the AR view and they will appear here.", T.Body, 28f,
                T.textSecondary, TextAlignmentOptions.Center);
            UI.Spacer(mEmpty, 20);
            BrowseButton = UI.PillButton(mEmpty, "Browse", "Browse the menu", UISprites.Back, T.accent, T.onAccent, 100, App.GoHome);

            mFooter = UI.Rect("Footer", mOrderView).Strip(false, 330);
            var footerBg = UI.Image(mFooter, "Bg", UISprites.White, T.backgroundRaised);
            footerBg.rectTransform.Stretch(0, 0, 0, -200);
            UI.Image(mFooter, "Line", UISprites.White, T.stroke).rectTransform.Strip(true, 2);
            var footer = UI.Rect("Content", mFooter).Stretch();
            UI.VStack(footer.gameObject, 14, new RectOffset(48, 48, 30, 40));
            var totalRow = UI.Rect("TotalRow", footer);
            UI.HStack(totalRow.gameObject, 12, null, TextAnchor.MiddleLeft);
            var subtotalLabel = UI.Text(totalRow, "Label", "Subtotal", T.Body, 30f, T.textSecondary);
            UI.Size(subtotalLabel.gameObject, -1, -1, 1f);
            mSubtotal = UI.Text(totalRow, "Value", "", T.Strong, 44f, T.accent, TextAlignmentOptions.Right);
            mSubtotal.textWrappingMode = TextWrappingModes.NoWrap;
            mNote = UI.Text(footer, "Note", "", T.Body, 22f, T.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            var buttons = UI.Rect("Buttons", footer);
            UI.HStack(buttons.gameObject, 18).childForceExpandHeight = true;
            ClearButton = UI.PillButton(buttons, "Clear", "Clear", null, T.surfaceHigh, T.accentDeep, 104, ClearOrder);
            UI.Size(ClearButton.gameObject, 220);
            ConfirmButton = UI.PillButton(buttons, "Confirm", "Confirm order", UISprites.Check, T.accent, T.onAccent, 104, Confirm);
            UI.Size(ConfirmButton.gameObject, -1, -1, 1f);

            // ---- confirmation view
            mConfirmed = UI.Rect("Confirmed", safe).Stretch();
            UI.VStack(mConfirmed.gameObject, 22, new RectOffset(72, 72, 140, 60), TextAnchor.UpperCenter);
            var checkHolder = UI.Rect("CheckHolder", mConfirmed);
            UI.Size(checkHolder.gameObject, -1, 180);
            var check = UI.Image(checkHolder, "Check", UISprites.Circle, T.accent);
            check.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(180, 180), Vector2.zero);
            UI.Image(check.transform, "Icon", UISprites.Check, T.onAccent).rectTransform.Stretch(42, 42, 42, 42);
            UI.Text(mConfirmed, "Title", "Order noted", T.Display, 64f, T.textPrimary, TextAlignmentOptions.Center);
            mOrderNumber = UI.Text(mConfirmed, "Number", "", T.Strong, 32f, T.accent, TextAlignmentOptions.Center);
            mOrderSummary = UI.Text(mConfirmed, "Summary", "", T.Body, 28f, T.textPrimary, TextAlignmentOptions.Center);
            mOrderSummary.lineSpacing = 10f;
            var disclaimer = UI.Rounded(mConfirmed, "Disclaimer", T.surfaceHigh, 28);
            UI.VStack(disclaimer.gameObject, 8, new RectOffset(32, 32, 28, 28));
            UI.Text(disclaimer.transform, "Text",
                "This is a local prototype: your order is saved on this device only and has not been sent to the kitchen. " +
                "Please show this screen to your server to place it.", T.Body, 25f, T.textSecondary, TextAlignmentOptions.Center)
                .lineSpacing = 8f;
            UI.Spacer(mConfirmed, 10);
            DoneButton = UI.PillButton(mConfirmed, "Done", "Done", null, T.accent, T.onAccent, 104, FinishOrder);
            var dev = App.Data.developer;
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                UI.Spacer(mConfirmed, 6);
                UI.Text(mConfirmed, "Watermark", "AR Menu by " + dev.name, T.Body, 22f, T.textMuted, TextAlignmentOptions.Center);
            }
            mConfirmed.gameObject.SetActive(false);

            App.Orders.CartChanged += Rebuild;
            App.ImagesChanged += () => { foreach (var r in mRows.Values) r.RefreshImage(); };
        }

        protected override void OnShow()
        {
            mConfirmed.gameObject.SetActive(false);
            mOrderView.gameObject.SetActive(true);
            Rebuild();
        }

        void Rebuild()
        {
            var lines = App.Orders.Lines;
            // Remove rows that are no longer in the cart.
            var stale = new List<string>();
            foreach (var id in mRows.Keys)
                if (App.Orders.QuantityOf(id) <= 0) stale.Add(id);
            foreach (var id in stale)
            {
                var row = mRows[id];
                mRows.Remove(id);
                row.Root.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(row.Root.gameObject);
                else UnityEngine.Object.DestroyImmediate(row.Root.gameObject);
            }
            // Add / update rows in cart order.
            for (var i = 0; i < lines.Count; i++)
            {
                var item = App.Data.Find(lines[i]);
                if (item == null) continue;
                if (!mRows.TryGetValue(item.id, out var row))
                {
                    row = new CartRow(mList, App, item);
                    mRows[item.id] = row;
                    if (IsVisible) row.AnimateIn();
                }
                row.Root.SetSiblingIndex(i);
                row.Refresh();
            }

            var units = App.Orders.TotalUnits;
            mHeaderCount.text = units == 1 ? "1 item" : units + " items";
            var empty = units == 0;
            mEmpty.gameObject.SetActive(empty);
            mFooter.gameObject.SetActive(!empty);
            mList.parent.parent.gameObject.SetActive(!empty);

            var total = App.Orders.Total(id => App.Data.Find(id)?.price ?? 0f, out var unpriced);
            mSubtotal.text = total > 0f ? App.FormatAmount(total) : unpriced ? "Price TBA" : App.FormatAmount(0f);
            mNote.text = unpriced
                ? "Some dishes do not have a price yet, so the subtotal is incomplete."
                : "Taxes and service charges, if any, are not included.";
        }

        void ClearOrder()
        {
            App.Orders.Clear();
            App.ShowToast("Order cleared", UISprites.Close);
        }

        void Confirm()
        {
            if (App.Orders.TotalUnits == 0) return;
            var number = UnityEngine.Random.Range(1000, 9999);
            mOrderNumber.text = "Order #" + number;
            var lines = new List<string>();
            foreach (var id in App.Orders.Lines)
            {
                var item = App.Data.Find(id);
                if (item != null) lines.Add(App.Orders.QuantityOf(id) + " x " + item.name);
            }
            mOrderSummary.text = string.Join("\n", lines);
            mOrderView.gameObject.SetActive(false);
            mConfirmed.gameObject.SetActive(true);
            var check = mConfirmed.GetChild(0).GetChild(0);
            check.localScale = Vector3.zero;
            Tween.Scale(check, Vector3.one, 0.5f, Ease.OutBack);
        }

        void FinishOrder()
        {
            App.Orders.Clear();
            App.GoHome();
        }

        protected override void AnimateIn()
        {
            Group.alpha = 1f;
            var c = mBackdrop.color;
            mBackdrop.color = new Color(c.r, c.g, c.b, 0f);
            Tween.To((mBackdrop, "fade"), 0.3f, t => mBackdrop.color = new Color(c.r, c.g, c.b, 0.72f * t));
            mPanel.anchoredPosition = new Vector2(0, -Root.rect.height);
            Tween.Move(mPanel, Vector2.zero, 0.45f, Ease.OutQuint);
        }

        protected override void AnimateOut(Action done)
        {
            var c = mBackdrop.color;
            var a = c.a;
            Tween.To((mBackdrop, "fade"), 0.22f, t => mBackdrop.color = new Color(c.r, c.g, c.b, Mathf.Lerp(a, 0f, t)));
            Tween.Move(mPanel, new Vector2(0, -Root.rect.height), 0.26f, Ease.InCubic, done);
        }

        /// <summary>One line in the order list.</summary>
        public sealed class CartRow
        {
            public readonly RectTransform Root;
            readonly MenuItemData mItem;
            readonly ARMenuApp mApp;
            readonly DishImage mImage;
            readonly TextMeshProUGUI mQty, mLineTotal, mUnit;
            public Button Minus { get; }
            public Button Plus { get; }

            public CartRow(Transform parent, ARMenuApp app, MenuItemData item)
            {
                mApp = app;
                mItem = item;
                var t = app.Theme;
                var bg = UI.Rounded(parent, "Line " + item.name, t.surface, 30);
                Root = bg.rectTransform;
                var stroke = UI.Rounded(bg.transform, "Stroke", t.stroke, 30, false, true);
                stroke.rectTransform.Stretch();
                stroke.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                UI.Size(bg.gameObject, -1, 176);
                var row = UI.HStack(bg.gameObject, 24, new RectOffset(20, 24, 20, 20));
                row.childForceExpandHeight = false;

                mImage = new DishImage(bg.transform, t, 22, false);
                UI.Size(mImage.Root.gameObject, 136, 136);

                var texts = UI.Rect("Texts", bg.transform);
                UI.VStack(texts.gameObject, 6, null, TextAnchor.MiddleLeft);
                UI.Size(texts.gameObject, -1, -1, 1f);
                var name = UI.Text(texts, "Name", item.name, t.Display, 38f, t.textPrimary);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                mUnit = UI.Text(texts, "Unit", "", t.Body, 24f, t.textSecondary);
                mLineTotal = UI.Text(texts, "LineTotal", "", t.Strong, 30f, t.accent);

                var stepper = UI.Rounded(bg.transform, "Stepper", t.surfaceHigh, 40);
                UI.HStack(stepper.gameObject, 0, new RectOffset(6, 6, 6, 6), TextAnchor.MiddleCenter);
                UI.Size(stepper.gameObject, 230, -1);
                var stepperLe = stepper.GetComponent<LayoutElement>();
                stepperLe.preferredHeight = stepperLe.minHeight = 80;
                Minus = UI.CircleButton(stepper.transform, "Minus", UISprites.Minus, t.surface, t.textPrimary, 68,
                    () => app.Orders.SetQuantity(item.id, app.Orders.QuantityOf(item.id) - 1));
                mQty = UI.Text(stepper.transform, "Qty", "", t.Strong, 30f, t.textPrimary, TextAlignmentOptions.Center);
                UI.Size(mQty.gameObject, -1, -1, 1f);
                Plus = UI.CircleButton(stepper.transform, "Plus", UISprites.Plus, t.accent, t.onAccent, 68,
                    () => app.Orders.SetQuantity(item.id, app.Orders.QuantityOf(item.id) + 1));
                // Centre the stepper vertically inside the row.
                row.childAlignment = TextAnchor.MiddleLeft;
                RefreshImage();
            }

            public int Quantity => mApp.Orders.QuantityOf(mItem.id);

            public void Refresh()
            {
                var q = Quantity;
                if (mQty.text != q.ToString() && !string.IsNullOrEmpty(mQty.text)) Tween.Punch(mQty.transform, 0.25f, 0.25f);
                mQty.text = q.ToString();
                mUnit.text = mItem.HasPrice ? mApp.FormatAmount(mItem.price) + " each" : "Price TBA";
                mLineTotal.text = mItem.HasPrice ? mApp.FormatAmount(mItem.price * q) : "";
                mLineTotal.gameObject.SetActive(mItem.HasPrice);
                Plus.interactable = q < OrderService.MaxQuantity;
            }

            public void RefreshImage() => mImage.Set(mApp.GetImage(mItem));

            public void AnimateIn()
            {
                Root.localScale = new Vector3(0.96f, 0.96f, 1f);
                Tween.Scale(Root, Vector3.one, 0.35f, Ease.OutBack);
            }
        }
    }

    /// <summary>Short confirmation message at the bottom of the screen.</summary>
    public sealed class Toast
    {
        readonly RectTransform mRoot;
        readonly CanvasGroup mGroup;
        readonly TextMeshProUGUI mLabel;
        readonly Image mIcon;
        public string LastMessage { get; private set; }

        public Toast(Transform parent, UITheme t)
        {
            var safe = UI.Rect("ToastArea", parent).Stretch();
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var bg = UI.Rounded(safe, "Toast", t.ink, 40);
            mRoot = bg.rectTransform;
            mRoot.Place(new Vector2(0.5f, 0f), new Vector2(0, 88), new Vector2(0, 190));
            bg.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UI.HStack(bg.gameObject, 16, new RectOffset(30, 36, 0, 0), TextAnchor.MiddleCenter).childForceExpandHeight = false;
            var stroke = UI.Rounded(bg.transform, "Stroke", UITheme.WithAlpha(t.accentLight, 0.35f), 40, false, true);
            stroke.rectTransform.Stretch();
            stroke.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            mIcon = UI.Icon(bg.transform, UISprites.Check, t.accentLight, 36);
            mLabel = UI.Text(bg.transform, "Text", "", t.Body, 27f, t.onInk, TextAlignmentOptions.Center);
            mLabel.textWrappingMode = TextWrappingModes.NoWrap;
            mGroup = bg.gameObject.AddComponent<CanvasGroup>();
            mGroup.alpha = 0f;
            mGroup.blocksRaycasts = false;
            mGroup.interactable = false;
        }

        public void Show(string message, Sprite icon)
        {
            LastMessage = message;
            mLabel.text = message;
            mIcon.sprite = icon ? icon : UISprites.Check;
            mRoot.transform.parent.SetAsLastSibling();
            mRoot.anchoredPosition = new Vector2(0, 160);
            Tween.Fade(mGroup, 1f, 0.25f);
            Tween.Move(mRoot, new Vector2(0, 190), 0.4f, Ease.OutBack);
            Tween.Delay((this, "hide"), 1.9f, () => Tween.Fade(mGroup, 0f, 0.3f));
        }
    }
}
