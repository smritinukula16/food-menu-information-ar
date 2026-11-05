using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>Small helper API for building uGUI hierarchies from code with consistent styling.</summary>
    public static class UI
    {
        public static UITheme Theme;
        public const int UILayer = 5;

        // ------------------------------------------------------------------ transforms

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor to a point (anchor == pivot) with a fixed size and offset.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        /// <summary>Full-width strip anchored to the top (or bottom) edge.</summary>
        public static RectTransform Strip(this RectTransform rt, bool top, float height, float offset = 0, float sideInset = 0)
        {
            var y = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0, y);
            rt.anchorMax = new Vector2(1, y);
            rt.pivot = new Vector2(0.5f, y);
            rt.sizeDelta = new Vector2(-sideInset * 2f, height);
            rt.anchoredPosition = new Vector2(0, top ? -offset : offset);
            return rt;
        }

        // ------------------------------------------------------------------ graphics

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Rounded(Transform parent, string name, Color color, float radius, bool raycast = false, bool outline = false)
        {
            var img = Image(parent, name, outline ? UISprites.RoundedOutline : UISprites.Rounded, color, raycast);
            MakeRounded(img, radius, outline);
            return img;
        }

        public static void MakeRounded(Image img, float radius, bool outline = false)
        {
            img.sprite = outline ? UISprites.RoundedOutline : UISprites.Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UISprites.SliceRadius / Mathf.Max(1f, radius);
        }

        /// <summary>A soft shadow placed behind a rounded element (call before creating the element).</summary>
        public static Image Shadow(Transform parent, float spread = 36f, float yOffset = -14f, float strength = 1f)
        {
            var c = Theme != null ? Theme.shadow : new Color(0, 0, 0, 0.3f);
            var img = Image(parent, "Shadow", UISprites.Shadow, new Color(c.r, c.g, c.b, c.a * strength));
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.4f;
            img.rectTransform.Stretch(-spread, -spread, -spread - yOffset, -spread + yOffset);
            return img;
        }

        public static Image Icon(Transform parent, Sprite sprite, Color color, float size)
        {
            var img = Image(parent, sprite ? sprite.name + "Icon" : "Icon", sprite, color);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            return tmp;
        }

        /// <summary>Small upper-case label with letter spacing.</summary>
        public static TextMeshProUGUI Eyebrow(Transform parent, string text, Color color, float size = 22f)
        {
            var t = Text(parent, "Eyebrow", text.ToUpperInvariant(), Theme.Strong, size, color);
            t.characterSpacing = 14f;
            return t;
        }

        /// <summary>Shows a value, or a muted "[Placeholder]" when the value is a TODO/empty.</summary>
        public static void SetValue(TextMeshProUGUI label, string value, string fallback, bool italicPlaceholder = true)
        {
            if (Placeholder.Is(value))
            {
                label.text = "<size=75%><color=#" + ColorUtility.ToHtmlStringRGB(Theme.accentDeep) + ">[PLACEHOLDER]</color></size> " +
                             Placeholder.Strip(value, fallback);
                label.color = Theme.textMuted;
                label.fontStyle = italicPlaceholder ? FontStyles.Italic : FontStyles.Normal;
            }
            else
            {
                label.text = value.Trim();
                label.fontStyle = FontStyles.Normal;
            }
        }

        // ------------------------------------------------------------------ buttons

        public static Button Button(Image target, Action onClick, bool pressFeedback = true)
        {
            target.raycastTarget = true;
            var btn = target.gameObject.AddComponent<Button>();
            btn.targetGraphic = target;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            if (pressFeedback) target.gameObject.AddComponent<PressFeedback>();
            return btn;
        }

        /// <summary>Pill / rounded button with an optional icon and a label.</summary>
        public static Button PillButton(Transform parent, string name, string label, Sprite icon, Color bg, Color fg,
            float height, Action onClick, float radius = -1f, bool outline = false, float fontSize = 30f)
        {
            var img = Rounded(parent, name, bg, radius < 0 ? height / 2f : radius, true, outline);
            var btn = Button(img, onClick);
            var row = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14;
            row.padding = new RectOffset(32, 32, 0, 0);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            if (icon) Icon(img.transform, icon, fg, height * 0.42f);
            if (!string.IsNullOrEmpty(label))
            {
                var t = Text(img.transform, "Label", label, Theme.Strong, fontSize, fg, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            return btn;
        }

        /// <summary>Circular icon button.</summary>
        public static Button CircleButton(Transform parent, string name, Sprite icon, Color bg, Color fg, float size,
            Action onClick, float iconScale = 0.46f)
        {
            var img = Image(parent, name, UISprites.Circle, bg, true);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var btn = Button(img, onClick);
            var ic = Image(img.transform, "Icon", icon, fg);
            ic.preserveAspect = true;
            ic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(size * iconScale, size * iconScale), Vector2.zero);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            return btn;
        }

        // ------------------------------------------------------------------ layout

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Size(GameObject go, float prefWidth = -1, float prefHeight = -1, float flexWidth = -1,
            float flexHeight = -1)
        {
            // Note: no '??' here — Unity's fake-null objects break null-coalescing in the editor.
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            if (prefWidth >= 0) { le.preferredWidth = prefWidth; le.minWidth = prefWidth; }
            if (prefHeight >= 0) { le.preferredHeight = prefHeight; le.minHeight = prefHeight; }
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0) le.flexibleHeight = flexHeight;
            return le;
        }

        public static RectTransform Spacer(Transform parent, float height = -1, float flexWidth = -1)
        {
            var rt = Rect("Spacer", parent);
            Size(rt.gameObject, -1, height, flexWidth);
            return rt;
        }

        public static void FitHeight(GameObject go)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>Vertical scroll view; returns the content transform (vertical stack, auto height).</summary>
        public static RectTransform VerticalScroll(Transform parent, string name, out ScrollRect scroll, RectOffset padding,
            float spacing)
        {
            var root = Rect(name, parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); // catches drags in empty space
            hit.color = new Color(0, 0, 0, 0);
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VStack(content.gameObject, spacing, padding);
            FitHeight(content.gameObject);

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        public static void SetActive(Component c, bool active)
        {
            if (c && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
    }
}
