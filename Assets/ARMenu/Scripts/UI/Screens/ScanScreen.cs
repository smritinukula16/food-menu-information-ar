using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    public enum ScanState { Searching, Detected, Viewing }

    /// <summary>
    /// AR camera overlay: scanning frame + status while searching, detection feedback and loading,
    /// then a compact dish card, dish switcher and model tools while the model is shown on the marker.
    /// The camera feed stays visible; only actual controls receive touches so Lean Touch keeps working.
    /// </summary>
    public sealed class ScanScreen : UIScreen
    {
        const float FrameSize = 700f, CornerLength = 120f, CornerThickness = 10f;
        const float DetectedHold = 0.85f;
        static readonly Vector2 ViewPanelPosition = new Vector2(0, 44);

        Image mLineGlow, mLineCore;

        readonly RectTransform mFrame, mScanLine, mCheck;
        readonly CanvasGroup mFrameGroup, mSearchPanel, mViewPanel, mTools, mHint, mChipsGroup;
        readonly List<Image> mCorners = new List<Image>();
        readonly Image mStatusSpinner, mStatusIcon;
        readonly TextMeshProUGUI mStatusTitle, mStatusText, mTip;
        readonly DishImage mThumb;
        readonly TextMeshProUGUI mDishName, mDishPrice;
        readonly Image mDishDiet;
        readonly Image mAutoRotateBg, mAutoRotateIcon;
        readonly List<Chip> mChips = new List<Chip>();
        string mStatusBase = "";
        bool mLost;

        public ScanState State { get; private set; } = ScanState.Searching;
        public Button BackButton { get; }
        public Button DetailsButton { get; }
        public Button AddButton { get; }
        public Button AutoRotateButton { get; }
        public Button ResetButton { get; }
        public Button RescanButton { get; }
        public CartButton Cart { get; }
        public IReadOnlyList<Chip> DishChips => mChips;
        public string StatusTitle => mStatusTitle.text;

        public ScanScreen(ARMenuApp app, RectTransform parent) : base(app, "ScanScreen", parent)
        {
            // Legibility gradients over the camera feed (no raycasts).
            var topShade = UI.Image(Root, "TopShade", UISprites.FadeUp, new Color(0, 0, 0, 0.7f));
            topShade.rectTransform.Strip(true, 420);
            var bottomShade = UI.Image(Root, "BottomShade", UISprites.FadeUp, new Color(0, 0, 0, 0.78f));
            bottomShade.rectTransform.Strip(false, 760);
            bottomShade.rectTransform.pivot = new Vector2(0.5f, 1f);
            bottomShade.rectTransform.localScale = new Vector3(1, -1, 1);

            var safe = CreateSafeArea(Root);

            // ---- top bar
            var top = UI.Rect("TopBar", safe).Strip(true, 132);
            UI.HStack(top.gameObject, 22, new RectOffset(40, 40, 28, 0), TextAnchor.MiddleLeft);
            var glass = UITheme.WithAlpha(T.ink, 0.62f);
            BackButton = UI.CircleButton(top, "Back", UISprites.Back, glass, T.onInk, 92, App.Back);
            var titles = UI.Rect("Titles", top);
            UI.VStack(titles.gameObject, 2, null, TextAnchor.MiddleLeft);
            UI.Size(titles.gameObject, -1, -1, 1f);
            UI.Eyebrow(titles, "AR view", T.accentLight, 20f);
            UI.Text(titles, "Title", "Scan the Menu Card", T.Display, 40f, T.onInk).textWrappingMode = TextWrappingModes.NoWrap;
            Cart = new CartButton(top, App, 92, glass, T.onInk);

            // ---- dish switcher
            var chipsRoot = UI.Rect("DishSwitcher", safe);
            chipsRoot.Place(new Vector2(0.5f, 1f), new Vector2(0, 80), new Vector2(0, -156));
            mChipsGroup = chipsRoot.gameObject.AddComponent<CanvasGroup>();
            UI.HStack(chipsRoot.gameObject, 14, null, TextAnchor.MiddleCenter).childForceExpandHeight = true;
            chipsRoot.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var item in App.Data.items)
            {
                if (App.Presenter == null || !App.Presenter.HasModel(item.modelKey)) continue;
                var it = item;
                mChips.Add(new Chip(chipsRoot, T, item.id, item.name, null, 76, _ => SelectDish(it, true), 26f));
            }

            // ---- scanning frame
            mFrame = UI.Rect("ScanFrame", safe).Place(new Vector2(0.5f, 0.5f), new Vector2(FrameSize, FrameSize), new Vector2(0, 70));
            mFrameGroup = mFrame.gameObject.AddComponent<CanvasGroup>();
            mFrameGroup.blocksRaycasts = false;
            var corners = UI.Rect("Corners", mFrame).Stretch();
            corners.gameObject.AddComponent<Pulse>();
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            {
                AddCornerBar(corners, new Vector2(sx * (FrameSize - CornerLength) / 2f, sy * (FrameSize - CornerThickness) / 2f),
                    new Vector2(CornerLength, CornerThickness));
                AddCornerBar(corners, new Vector2(sx * (FrameSize - CornerThickness) / 2f, sy * (FrameSize - CornerLength) / 2f),
                    new Vector2(CornerThickness, CornerLength));
            }

            var line = UI.Rect("ScanLine", mFrame).Place(new Vector2(0.5f, 0.5f), new Vector2(FrameSize - 80, 90), Vector2.zero);
            mScanLine = line;
            mLineGlow = UI.Image(line, "Glow", UISprites.Glow, UITheme.WithAlpha(T.accentLight, 0.45f));
            mLineGlow.rectTransform.Stretch();
            mLineCore = UI.Rounded(line, "Core", T.accentLight, 3);
            mLineCore.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(FrameSize - 120, 5), Vector2.zero);

            var check = UI.Image(mFrame, "Detected", UISprites.Circle, T.accent);
            mCheck = check.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(150, 150), Vector2.zero);
            UI.Image(check.transform, "Icon", UISprites.Check, T.onAccent).rectTransform.Stretch(34, 34, 34, 34);
            mCheck.localScale = Vector3.zero;

            // ---- status card (searching / detected)
            var search = UI.Rounded(safe, "StatusCard", UITheme.WithAlpha(T.background, 0.9f), 36, true);
            search.rectTransform.Strip(false, 248, 44, 40);
            mSearchPanel = search.gameObject.AddComponent<CanvasGroup>();
            AddStroke(search.transform, 36);
            UI.VStack(search.gameObject, 14, new RectOffset(40, 40, 36, 34));
            var statusRow = UI.Rect("Row", search.transform);
            UI.HStack(statusRow.gameObject, 26, null, TextAnchor.MiddleLeft);
            var spinnerHolder = UI.Rect("Indicator", statusRow);
            UI.Size(spinnerHolder.gameObject, 64, 64);
            mStatusSpinner = UI.Image(spinnerHolder, "Spinner", UISprites.Spinner, T.accent);
            mStatusSpinner.rectTransform.Stretch();
            mStatusSpinner.gameObject.AddComponent<SpinnerRotation>();
            mStatusIcon = UI.Image(spinnerHolder, "Icon", UISprites.Scan, T.accent);
            mStatusIcon.rectTransform.Stretch(14, 14, 14, 14);
            var statusTexts = UI.Rect("Texts", statusRow);
            UI.VStack(statusTexts.gameObject, 4);
            UI.Size(statusTexts.gameObject, -1, -1, 1f);
            mStatusTitle = UI.Text(statusTexts, "Title", "", T.Display, 36f, T.textPrimary);
            mStatusText = UI.Text(statusTexts, "Status", "", T.Body, 26f, T.textSecondary);
            mTip = UI.Text(search.transform, "Tip", "Hold your phone steady over the 2616 menu card in good light.", T.Body, 23f,
                T.textMuted);

            // ---- dish card (viewing)
            var view = UI.Rounded(safe, "DishCard", UITheme.WithAlpha(T.background, 0.92f), 36, true);
            view.rectTransform.Strip(false, 320, 44, 40);
            mViewPanel = view.gameObject.AddComponent<CanvasGroup>();
            AddStroke(view.transform, 36);
            UI.VStack(view.gameObject, 24, new RectOffset(32, 32, 30, 30));
            var dishRow = UI.Rect("Dish", view.transform);
            UI.HStack(dishRow.gameObject, 26, null, TextAnchor.MiddleLeft);
            mThumb = new DishImage(dishRow, T, 22, false);
            UI.Size(mThumb.Root.gameObject, 118, 118);
            var dishTexts = UI.Rect("Texts", dishRow);
            UI.VStack(dishTexts.gameObject, 6);
            UI.Size(dishTexts.gameObject, -1, -1, 1f);
            mDishName = UI.Text(dishTexts, "Name", "", T.Display, 42f, T.textPrimary);
            mDishName.textWrappingMode = TextWrappingModes.NoWrap;
            mDishName.overflowMode = TextOverflowModes.Ellipsis;
            var meta = UI.Rect("Meta", dishTexts);
            UI.HStack(meta.gameObject, 12, null, TextAnchor.MiddleLeft);
            mDishDiet = FoodBadges.DietMark(meta, T, Diet.Unspecified, 28);
            mDishPrice = UI.Text(meta, "Price", "", T.Strong, 32f, T.accent);
            mDishPrice.textWrappingMode = TextWrappingModes.NoWrap;

            var buttons = UI.Rect("Buttons", view.transform);
            var bl = UI.HStack(buttons.gameObject, 18);
            bl.childForceExpandWidth = true;
            DetailsButton = UI.PillButton(buttons, "Details", "Details", UISprites.Info, T.surfaceHigh, T.textPrimary, 96,
                () => { if (App.SelectedItem != null) App.OpenDetail(App.SelectedItem); });
            AddButton = UI.PillButton(buttons, "AddToOrder", "Add to order", UISprites.Plus, T.accent, T.onAccent, 96,
                () => { if (App.SelectedItem != null) App.AddToCart(App.SelectedItem, 1); });
            UI.Size(DetailsButton.gameObject, -1, -1, 1f);
            UI.Size(AddButton.gameObject, -1, -1, 1f);

            // ---- gesture hint
            var hint = UI.Rounded(safe, "GestureHint", UITheme.WithAlpha(T.background, 0.82f), 30);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(0, 404));
            hint.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UI.HStack(hint.gameObject, 0, new RectOffset(30, 30, 0, 0), TextAnchor.MiddleCenter).childForceExpandHeight = true;
            UI.Text(hint.transform, "Text", "Drag to move  ·  Pinch to resize  ·  Twist to rotate", T.Body, 23f,
                T.textSecondary, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            mHint = hint.gameObject.AddComponent<CanvasGroup>();
            mHint.blocksRaycasts = false;

            // ---- creator watermark (always visible over the camera feed)
            var dev = App.Data.developer;
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                var mark = UI.Text(safe, "Watermark", "AR Menu by " + dev.name, T.Strong, 19f,
                    UITheme.WithAlpha(T.onInk, 0.6f), TextAlignmentOptions.Center);
                mark.characterSpacing = 6f;
                mark.rectTransform.Strip(false, 34, 6);
            }

            // ---- model tools
            var tools = UI.Rect("Tools", safe).Place(new Vector2(1f, 0.5f), new Vector2(130, 420), new Vector2(-24, 120));
            mTools = tools.gameObject.AddComponent<CanvasGroup>();
            UI.VStack(tools.gameObject, 22, null, TextAnchor.MiddleCenter);
            AutoRotateButton = Tool(tools, UISprites.Rotate, "Rotate", () => SetAutoRotate(!App.Presenter.AutoRotate), out mAutoRotateBg, out mAutoRotateIcon);
            ResetButton = Tool(tools, UISprites.Recenter, "Reset", () => App.Presenter?.ResetView(), out _, out _);
            RescanButton = Tool(tools, UISprites.Scan, "Rescan", Rescan, out _, out _);

            SetPanel(mViewPanel, false, false);
            SetPanel(mTools, false, false);
            SetPanel(mHint, false, false);
        }

        void AddCornerBar(Transform parent, Vector2 pos, Vector2 size)
        {
            var bar = UI.Rounded(parent, "Corner", T.onInk, CornerThickness / 2f);
            bar.rectTransform.Place(new Vector2(0.5f, 0.5f), size, pos);
            mCorners.Add(bar);
        }

        void AddStroke(Transform parent, float radius)
        {
            var s = UI.Rounded(parent, "Stroke", UITheme.WithAlpha(T.accent, 0.28f), radius, false, true);
            s.rectTransform.Stretch();
            s.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        Button Tool(Transform parent, Sprite icon, string label, Action onClick, out Image bg, out Image ic)
        {
            var holder = UI.Rect("Tool " + label, parent);
            UI.VStack(holder.gameObject, 6, null, TextAnchor.UpperCenter, false);
            var btn = UI.CircleButton(holder, "Button", icon, UITheme.WithAlpha(T.ink, 0.62f), T.onInk, 92, onClick, 0.5f);
            bg = btn.GetComponent<Image>();
            ic = btn.transform.Find("Icon").GetComponent<Image>();
            var text = UI.Text(holder, "Label", label, T.Strong, 19f, T.onInk, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return btn;
        }

        void SetPanel(CanvasGroup g, bool visible, bool animate, float delay = 0f)
        {
            if (g == null) return;
            // The frame and the hint are decorative: never let them block touches meant for the model.
            var decorative = g == mFrameGroup || g == mHint;
            g.interactable = visible;
            g.blocksRaycasts = visible && !decorative;
            if (!animate)
            {
                Tween.Kill((g, "fade"));
                g.alpha = visible ? 1f : 0f;
                return;
            }
            Tween.Fade(g, visible ? 1f : 0f, visible ? 0.35f : 0.2f, null, Ease.OutCubic, delay);
        }

        // ------------------------------------------------------------------ flow

        protected override void OnShow()
        {
            Tween.Kill((this, "detect"));
            mLost = false;
            RefreshChips();
            RefreshDish();
            SetAutoRotate(App.Presenter != null && App.Presenter.AutoRotate);
            if (App.Marker != null && App.Marker.IsTracked) BeginDetected();
            else EnterSearching(false);
        }

        protected override void OnHide()
        {
            Tween.Kill((this, "detect"));
            Tween.Kill((this, "hint"));
            App.Presenter?.Conceal();
        }

        public void OnMarkerFound()
        {
            if (!IsVisible) return;
            if (State == ScanState.Searching) BeginDetected();
        }

        public void OnMarkerLost()
        {
            if (!IsVisible) return;
            Tween.Kill((this, "detect"));
            mLost = true;
            App.Presenter?.SetTracked(false);
            EnterSearching(true);
        }

        void EnterSearching(bool lost)
        {
            State = ScanState.Searching;
            App.Presenter?.PrepareReveal();
            SetCornerColor(T.onInk);
            Tween.Kill((mCheck, "scale"));
            mCheck.localScale = Vector3.zero;
            mFrame.localScale = Vector3.one;
            mScanLine.gameObject.SetActive(true);
            SetPanel(mFrameGroup, true, true);
            SetPanel(mSearchPanel, true, true);
            SetPanel(mViewPanel, false, true);
            SetPanel(mTools, false, true);
            SetPanel(mHint, false, true);

            mStatusSpinner.color = lost ? T.warning : T.accent;
            mStatusIcon.sprite = UISprites.Scan;
            mStatusIcon.color = lost ? T.warning : T.accent;
            mStatusTitle.text = lost ? "Menu card lost" : "Point your camera at the menu card";
            mStatusTitle.color = lost ? T.warning : T.textPrimary;
            var dish = App.SelectedItem != null ? App.SelectedItem.name : "Your dish";
            mStatusBase = lost ? "Point your camera back at the menu card" : "Looking for the menu card — " + dish + " will appear on it";
            mStatusText.text = mStatusBase;
            mTip.gameObject.SetActive(true);
        }

        void BeginDetected()
        {
            State = ScanState.Detected;
            App.Presenter?.PrepareReveal();
            SetCornerColor(T.accentLight);
            mScanLine.gameObject.SetActive(false);
            Tween.Scale(mFrame, Vector3.one * 0.86f, 0.45f, Ease.OutBack);
            mCheck.localScale = Vector3.zero;
            Tween.Scale(mCheck, Vector3.one, 0.45f, Ease.OutBack);

            mStatusSpinner.color = T.accent;
            mStatusIcon.sprite = UISprites.Check;
            mStatusIcon.color = T.accent;
            mStatusTitle.text = "Menu card found";
            mStatusTitle.color = T.accent;
            var dish = App.SelectedItem != null ? App.SelectedItem.name : "dish";
            mStatusBase = "Serving your " + dish;
            mStatusText.text = mStatusBase;
            mTip.gameObject.SetActive(false);

            Tween.Delay((this, "detect"), mLost ? DetectedHold * 0.5f : DetectedHold, EnterViewing);
        }

        void EnterViewing()
        {
            State = ScanState.Viewing;
            mLost = false;
            SetPanel(mFrameGroup, false, true);
            SetPanel(mSearchPanel, false, true);
            SetPanel(mViewPanel, true, true, 0.1f);
            SetPanel(mTools, true, true, 0.2f);
            var rt = (RectTransform)mViewPanel.transform;
            rt.anchoredPosition = ViewPanelPosition + new Vector2(0, -40);
            Tween.Move(rt, ViewPanelPosition, 0.45f, Ease.OutQuint, null, 0.1f);
            RefreshDish();
            App.Presenter?.Reveal(App.Marker == null || App.Marker.IsTracked);

            SetPanel(mHint, true, true, 0.5f);
            Tween.Delay((this, "hint"), 5f, () => SetPanel(mHint, false, true));
        }

        /// <summary>Switch the dish while the AR view is already open (e.g. "Back to the AR view" from details).</summary>
        public void SwitchTo(MenuItemData item)
        {
            if (item == null) return;
            if (State == ScanState.Viewing && App.Presenter != null && App.Presenter.ActiveKey == item.modelKey &&
                App.Presenter.IsPresenting)
            {
                RefreshChips();
                RefreshDish();
                return;
            }
            SelectDish(item, true);
        }

        void SelectDish(MenuItemData item, bool animate)
        {
            if (item == null) return;
            App.SelectItem(item);
            RefreshChips();
            RefreshDish();
            if (State == ScanState.Viewing)
            {
                App.Presenter?.Select(item.modelKey);
                App.Presenter?.Reveal(App.Marker == null || App.Marker.IsTracked);
                Tween.Punch(mViewPanel.transform, 0.03f, 0.3f);
            }
            else if (State == ScanState.Searching)
            {
                App.Presenter?.Select(item.modelKey);
                App.Presenter?.PrepareReveal();
                EnterSearching(mLost);
            }
        }

        void Rescan()
        {
            Tween.Kill((this, "detect"));
            App.Presenter?.ResetView();
            mLost = false;
            EnterSearching(false);
            // If the marker is still in view, detection runs again right away.
            if (App.Marker != null && App.Marker.IsTracked)
                Tween.Delay((this, "detect"), 0.6f, BeginDetected);
        }

        void SetAutoRotate(bool on)
        {
            if (App.Presenter != null) App.Presenter.AutoRotate = on;
            if (mAutoRotateBg) mAutoRotateBg.color = on ? T.accent : UITheme.WithAlpha(T.ink, 0.62f);
            if (mAutoRotateIcon) mAutoRotateIcon.color = on ? T.onAccent : T.onInk;
        }

        void SetCornerColor(Color c)
        {
            foreach (var corner in mCorners)
            {
                var a = corner.color.a;
                corner.color = new Color(c.r, c.g, c.b, a);
            }
        }

        void RefreshChips()
        {
            var id = App.SelectedItem != null ? App.SelectedItem.id : null;
            foreach (var chip in mChips) chip.SetSelected(chip.Id == id);
            mChipsGroup.gameObject.SetActive(mChips.Count > 1);
        }

        void RefreshDish()
        {
            var item = App.SelectedItem;
            if (item == null) return;
            mDishName.text = item.name;
            App.ApplyPrice(mDishPrice, item, 32f);
            FoodBadges.SetDiet(mDishDiet, T, item.DietType);
            mThumb.Set(App.GetImage(item));
        }

        /// <summary>Per-frame animation (scan line sweep and status dots).</summary>
        public void Tick()
        {
            if (!IsVisible) return;
            if (State == ScanState.Searching && mScanLine.gameObject.activeSelf)
            {
                var half = FrameSize * 0.5f - 60f;
                var s = Mathf.Sin(Time.unscaledTime * 1.7f);
                mScanLine.anchoredPosition = new Vector2(0f, s * half);
                var edge = 1f - Mathf.Pow(Mathf.Abs(s), 6f);
                mLineGlow.color = UITheme.WithAlpha(mLineGlow.color, 0.35f * edge);
                mLineCore.color = UITheme.WithAlpha(mLineCore.color, edge);
            }
            if (State != ScanState.Viewing && !mLost)
            {
                var dots = (int)(Time.unscaledTime * 2.5f) % 4;
                mStatusText.text = mStatusBase + new string('.', dots);
            }
        }
    }
}
