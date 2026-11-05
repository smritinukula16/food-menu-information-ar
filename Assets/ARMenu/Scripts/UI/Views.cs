using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>Base class for full-screen layers (menu, AR scan) and sheets (detail, cart).</summary>
    public abstract class UIScreen
    {
        public readonly RectTransform Root;
        public readonly CanvasGroup Group;
        protected readonly ARMenuApp App;
        protected UITheme T => App.Theme;

        public bool IsVisible { get; private set; }

        protected UIScreen(ARMenuApp app, string name, RectTransform parent)
        {
            App = app;
            Root = UI.Rect(name, parent).Stretch();
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Root.gameObject.SetActive(false);
        }

        /// <summary>A child that fits the device safe area (notch / home indicator).</summary>
        protected RectTransform CreateSafeArea(Transform parent, bool top = true, bool bottom = true)
        {
            var safe = UI.Rect("SafeArea", parent).Stretch();
            var fitter = safe.gameObject.AddComponent<SafeAreaFitter>();
            fitter.top = top;
            fitter.bottom = bottom;
            return safe;
        }

        public void Show(bool animate = true)
        {
            var wasVisible = IsVisible;
            IsVisible = true;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Group.interactable = true;
            Group.blocksRaycasts = true;
            OnShow();
            if (!wasVisible || Group.alpha < 1f)
            {
                if (animate) AnimateIn();
                else Group.alpha = 1f;
            }
        }

        public void Hide(bool animate = true)
        {
            if (!IsVisible && !Root.gameObject.activeSelf) return;
            IsVisible = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            OnHide();
            if (animate) AnimateOut(() => { if (!IsVisible) Root.gameObject.SetActive(false); });
            else
            {
                Group.alpha = 0f;
                Root.gameObject.SetActive(false);
            }
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        protected virtual void AnimateIn()
        {
            Group.alpha = 0f;
            Tween.Fade(Group, 1f, 0.28f);
        }

        protected virtual void AnimateOut(Action done) => Tween.Fade(Group, 0f, 0.2f, done);
    }

    /// <summary>Rounded food image: real photo if provided, otherwise a studio render of the 3D model.</summary>
    public sealed class DishImage
    {
        public readonly RectTransform Root;
        readonly RawImage mImage;
        readonly Image mPlaceholder;

        public DishImage(Transform parent, UITheme t, float radius, bool glow = true)
        {
            var frame = UI.Rounded(parent, "DishImage", t.surfaceHigh, radius);
            Root = frame.rectTransform;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            if (glow)
            {
                var g = UI.Image(Root, "Glow", UISprites.Glow, UITheme.WithAlpha(Color.white, 0.85f));
                g.rectTransform.Stretch(-40, -40, -10, -70);
            }
            mPlaceholder = UI.Image(Root, "Placeholder", UISprites.Plate, UITheme.WithAlpha(t.textMuted, 0.6f));
            mPlaceholder.preserveAspect = true;
            mPlaceholder.rectTransform.Stretch().localScale = Vector3.one * 0.5f;

            var rawRt = UI.Rect("Photo", Root).Stretch();
            mImage = rawRt.gameObject.AddComponent<RawImage>();
            mImage.raycastTarget = false;
            mImage.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            mImage.enabled = false;
        }

        public void Set(Texture texture)
        {
            mImage.texture = texture;
            mImage.enabled = texture != null;
            mPlaceholder.enabled = texture == null;
            if (texture != null)
            {
                var fitter = mImage.GetComponent<AspectRatioFitter>();
                fitter.aspectRatio = texture.height > 0 ? (float)texture.width / texture.height : 1f;
            }
        }
    }

    /// <summary>Factory for the small food indicators (diet mark, labels, spice).</summary>
    public static class FoodBadges
    {
        public static Image DietMark(Transform parent, UITheme t, Diet diet, float size)
        {
            var icon = UI.Icon(parent, diet == Diet.NonVegetarian ? UISprites.NonVegMark : UISprites.VegMark,
                diet == Diet.NonVegetarian ? t.nonVeg : t.veg, size);
            icon.gameObject.SetActive(diet != Diet.Unspecified);
            return icon;
        }

        public static void SetDiet(Image mark, UITheme t, Diet diet)
        {
            mark.gameObject.SetActive(diet != Diet.Unspecified);
            mark.sprite = diet == Diet.NonVegetarian ? UISprites.NonVegMark : UISprites.VegMark;
            mark.color = diet == Diet.NonVegetarian ? t.nonVeg : t.veg;
        }

        /// <summary>Small pill label e.g. "POPULAR" / "CHEF'S SPECIAL".</summary>
        public static RectTransform Pill(Transform parent, UITheme t, string text, Sprite icon, Color bg, Color fg,
            bool outline = false, float height = 44f, float fontSize = 19f)
        {
            var img = UI.Rounded(parent, "Pill " + text, bg, height / 2f, false, outline);
            var row = UI.HStack(img.gameObject, 8, new RectOffset(18, 20, 0, 0), TextAnchor.MiddleCenter);
            row.childForceExpandHeight = false;
            if (icon) UI.Icon(img.transform, icon, fg, height * 0.5f);
            var label = UI.Text(img.transform, "Label", text.ToUpperInvariant(), t.Strong, fontSize, fg, TextAlignmentOptions.Center);
            label.characterSpacing = 8f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UI.Size(img.gameObject, -1, height);
            return img.rectTransform;
        }

        public static RectTransform PopularPill(Transform parent, UITheme t, float height = 44f) =>
            Pill(parent, t, "Popular", UISprites.Star, UITheme.WithAlpha(t.accentLight, 0.55f), t.accentDeep, false, height);

        public static RectTransform ChefPill(Transform parent, UITheme t, float height = 44f) =>
            Pill(parent, t, "Chef's Special", UISprites.Chef, t.ink, t.accentLight, false, height);

        /// <summary>Row of three flames, lit according to spice level (hidden when not specified).</summary>
        public static RectTransform SpiceMeter(Transform parent, UITheme t, float size, out Image[] flames)
        {
            var row = UI.Rect("Spice", parent);
            UI.HStack(row.gameObject, size * 0.08f);
            flames = new Image[3];
            for (var i = 0; i < 3; i++) flames[i] = UI.Icon(row, UISprites.Flame, t.spice, size);
            return row;
        }

        public static void SetSpice(RectTransform meter, Image[] flames, UITheme t, int level, bool hideWhenUnknownOrZero)
        {
            var known = level >= 0;
            meter.gameObject.SetActive(!hideWhenUnknownOrZero || level > 0);
            for (var i = 0; i < flames.Length; i++)
                flames[i].color = known && i < level ? t.spice : UITheme.WithAlpha(t.textMuted, 0.35f);
        }

        public static string SpiceLabel(int level)
        {
            switch (level)
            {
                case 0: return "Not spicy";
                case 1: return "Mild";
                case 2: return "Medium";
                case 3: return "Hot";
                default: return null;
            }
        }

        public static string DietLabel(Diet diet) =>
            diet == Diet.Vegetarian ? "Vegetarian" : diet == Diet.NonVegetarian ? "Non-vegetarian" : null;
    }

    /// <summary>[-] qty [+] control.</summary>
    public sealed class QuantityStepper
    {
        public readonly RectTransform Root;
        readonly TextMeshProUGUI mLabel;
        readonly Button mMinus, mPlus;
        public int Value { get; private set; } = 1;
        public int Min = 1;
        public int Max = OrderService.MaxQuantity;
        public event Action<int> Changed;

        public QuantityStepper(Transform parent, UITheme t, float height, float width)
        {
            var bg = UI.Rounded(parent, "Quantity", t.surfaceHigh, height / 2f);
            Root = bg.rectTransform;
            UI.Size(bg.gameObject, width, height);
            UI.HStack(bg.gameObject, 0, new RectOffset(6, 6, 6, 6), TextAnchor.MiddleCenter).childForceExpandWidth = false;

            var btnSize = height - 12f;
            mMinus = UI.CircleButton(Root, "Minus", UISprites.Minus, t.surface, t.textPrimary, btnSize, () => SetValue(Value - 1, true));
            mLabel = UI.Text(Root, "Value", "1", t.Strong, height * 0.38f, t.textPrimary, TextAlignmentOptions.Center);
            UI.Size(mLabel.gameObject, -1, -1, 1f);
            mPlus = UI.CircleButton(Root, "Plus", UISprites.Plus, t.accent, t.onAccent, btnSize, () => SetValue(Value + 1, true));
        }

        public void SetValue(int value, bool notify)
        {
            var clamped = Mathf.Clamp(value, Min, Max);
            var changed = clamped != Value;
            Value = clamped;
            mLabel.text = Value.ToString();
            mMinus.interactable = Value > Min;
            mPlus.interactable = Value < Max;
            if (changed && notify)
            {
                Tween.Punch(mLabel.transform, 0.2f, 0.25f);
                Changed?.Invoke(Value);
            }
        }

        public Button MinusButton => mMinus;
        public Button PlusButton => mPlus;
    }

    /// <summary>Circular cart button with a live item-count badge.</summary>
    public sealed class CartButton
    {
        public readonly Button Button;
        readonly RectTransform mBadge;
        readonly TextMeshProUGUI mCount;
        readonly OrderService mOrders;
        int mLast = -1;

        public CartButton(Transform parent, ARMenuApp app, float size, Color bg, Color? iconColor = null)
        {
            var t = app.Theme;
            mOrders = app.Orders;
            Button = UI.CircleButton(parent, "CartButton", UISprites.Cart, bg, iconColor ?? t.textPrimary, size, app.OpenCart, 0.5f);
            var badge = UI.Image(Button.transform, "Badge", UISprites.Circle, t.accent);
            mBadge = badge.rectTransform;
            mBadge.Place(new Vector2(1f, 1f), new Vector2(size * 0.46f, size * 0.46f), new Vector2(size * 0.08f, size * 0.08f));
            mCount = UI.Text(mBadge, "Count", "0", t.Strong, size * 0.24f, t.onAccent, TextAlignmentOptions.Center);
            mCount.rectTransform.Stretch();
            mOrders.CartChanged += Refresh;
            Refresh();
        }

        public void Refresh()
        {
            var count = mOrders.TotalUnits;
            mBadge.gameObject.SetActive(count > 0);
            mCount.text = count > 99 ? "99+" : count.ToString();
            if (mLast >= 0 && count > mLast) Tween.Punch(mBadge, 0.35f, 0.4f);
            mLast = count;
        }

        public string BadgeText => mBadge.gameObject.activeSelf ? mCount.text : "";
    }

    /// <summary>Selectable chip used for categories and the AR dish switcher.</summary>
    public sealed class Chip
    {
        public readonly Button Button;
        readonly Image mBg;
        readonly TextMeshProUGUI mLabel;
        readonly Image mIcon;
        readonly UITheme mTheme;
        public readonly string Id;

        public Chip(Transform parent, UITheme t, string id, string label, Sprite icon, float height, Action<string> onClick,
            float fontSize = 26f)
        {
            mTheme = t;
            Id = id;
            mBg = UI.Rounded(parent, "Chip " + label, t.surfaceHigh, height / 2f, true);
            Button = UI.Button(mBg, () => onClick?.Invoke(id));
            UI.HStack(mBg.gameObject, 10, new RectOffset(30, 30, 0, 0), TextAnchor.MiddleCenter);
            if (icon) mIcon = UI.Icon(mBg.transform, icon, t.textSecondary, height * 0.4f);
            mLabel = UI.Text(mBg.transform, "Label", label, t.Strong, fontSize, t.textSecondary, TextAlignmentOptions.Center);
            mLabel.textWrappingMode = TextWrappingModes.NoWrap;
            UI.Size(mBg.gameObject, -1, height);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            mBg.color = selected ? mTheme.ink : mTheme.surfaceHigh;
            var fg = selected ? mTheme.onInk : mTheme.accentDeep;
            mLabel.color = fg;
            if (mIcon) mIcon.color = fg;
        }
    }
}
