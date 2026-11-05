using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>Launch splash: cafe logo, "AR Menu Experience" and the creator credit. Tap to skip.</summary>
    public sealed class SplashScreen : UIScreen
    {
        const float HoldSeconds = 2.2f;
        readonly RectTransform mLogo;
        readonly CanvasGroup mTexts;
        public Button SkipButton { get; }

        public SplashScreen(ARMenuApp app, RectTransform parent) : base(app, "Splash", parent)
        {
            var bg = UI.Image(Root, "Background", UISprites.White, T.ink, true);
            bg.rectTransform.Stretch();
            SkipButton = UI.Button(bg, Dismiss, false);

            var glow = UI.Image(Root, "Glow", UISprites.Glow, UITheme.WithAlpha(T.accent, 0.55f));
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500, 1500), new Vector2(0, 160));
            var glow2 = UI.Image(Root, "Glow2", UISprites.Glow, UITheme.WithAlpha(T.accentLight, 0.25f));
            glow2.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(700, 700), new Vector2(0, 160));

            var center = UI.Rect("Center", Root).Place(new Vector2(0.5f, 0.5f), new Vector2(900, 700), new Vector2(0, 120));
            UI.VStack(center.gameObject, 18, null, TextAnchor.MiddleCenter, false);

            var logo = UI.Image(center, "Logo", UISprites.Circle, T.accentLight);
            UI.Size(logo.gameObject, 220, 220);
            mLogo = logo.rectTransform;
            var name = Placeholder.Strip(App.Data.restaurant.name, "Cafe");
            var mono = name.Split(' ')[0];
            var monoText = UI.Text(logo.transform, "Monogram", mono, T.Display, mono.Length > 1 ? 66f : 110f, T.ink,
                TextAlignmentOptions.Center);
            monoText.rectTransform.Stretch(0, 0, 0, 8);
            UI.Image(logo.transform, "Ring", UISprites.Ring, UITheme.WithAlpha(T.ink, 0.25f)).rectTransform.Stretch(10, 10, 10, 10);

            var textsRt = UI.Rect("Texts", center);
            UI.VStack(textsRt.gameObject, 10, new RectOffset(0, 0, 24, 0), TextAnchor.UpperCenter, false);
            mTexts = textsRt.gameObject.AddComponent<CanvasGroup>();
            var title = UI.Text(textsRt, "Name", name.ToUpperInvariant(), T.Display, 92f, T.onInk, TextAlignmentOptions.Center);
            title.characterSpacing = 16f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UI.Eyebrow(textsRt, "AR Menu Experience", T.accentLight, 24f).alignment = TextAlignmentOptions.Center;

            var dev = App.Data.developer;
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                var footer = UI.Rect("Credit", Root).Strip(false, 140, 90, 60);
                UI.VStack(footer.gameObject, 6, null, TextAnchor.UpperCenter);
                UI.Eyebrow(footer, "Done by", UITheme.WithAlpha(T.onInk, 0.55f), 18f).alignment = TextAlignmentOptions.Center;
                UI.Text(footer, "Name", dev.DoneBy, T.Display, 40f, T.accentLight, TextAlignmentOptions.Center);
            }
        }

        protected override void AnimateIn()
        {
            Group.alpha = 1f;
            mLogo.localScale = Vector3.one * 0.6f;
            Tween.Scale(mLogo, Vector3.one, 0.8f, Ease.OutBack);
            mTexts.alpha = 0f;
            Tween.Fade(mTexts, 1f, 0.6f, null, Ease.OutCubic, 0.35f);
            Tween.Delay((this, "auto"), HoldSeconds, Dismiss);
        }

        protected override void AnimateOut(Action done) => Tween.Fade(Group, 0f, 0.5f, done);

        public void Dismiss()
        {
            if (!IsVisible) return;
            Tween.Kill((this, "auto"));
            Hide();
        }
    }

    /// <summary>Credits & acknowledgements for the creator, Asset Store packages, SDKs and fonts (from menu_data.json).</summary>
    public sealed class CreditsSheet : UIScreen
    {
        readonly RectTransform mPanel;
        readonly Image mBackdrop;
        public Button CloseButton { get; }
        public int EntryCount { get; }

        public CreditsSheet(ARMenuApp app, RectTransform parent) : base(app, "CreditsSheet", parent)
        {
            mBackdrop = UI.Image(Root, "Backdrop", UISprites.White, new Color(0, 0, 0, 0.6f), true);
            mBackdrop.rectTransform.Stretch();
            UI.Button(mBackdrop, App.Back, false);

            var panel = UI.Rounded(Root, "Panel", T.background, 44, true);
            mPanel = panel.rectTransform.Stretch(0, 0, 56, -48);
            var safe = UI.Rect("Safe", mPanel).Stretch(0, 0, 0, 48);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var header = UI.Rect("Header", safe).Strip(true, 150);
            UI.HStack(header.gameObject, 16, new RectOffset(48, 40, 40, 0), TextAnchor.MiddleLeft);
            var titles = UI.Rect("Titles", header);
            UI.VStack(titles.gameObject, 2);
            UI.Size(titles.gameObject, -1, -1, 1f);
            UI.Eyebrow(titles, "Acknowledgements", T.accent);
            UI.Text(titles, "Title", "Credits", T.Display, 52f, T.textPrimary);
            CloseButton = UI.CircleButton(header, "Close", UISprites.Close, T.surfaceHigh, T.textPrimary, 88, App.Back);

            var content = UI.VerticalScroll(safe, "Scroll", out var scroll, new RectOffset(40, 40, 8, 80), 18);
            ((RectTransform)scroll.transform).Stretch(0, 0, 150, 0);

            BuildCreatorCard(content);

            var data = App.Data;
            string section = null;
            foreach (var entry in data.credits)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.title)) continue;
                if (IsCreatorEntry(entry, data.developer)) continue; // shown in the creator card
                if (entry.section != section)
                {
                    section = entry.section;
                    var head = UI.Eyebrow(content, section ?? "", T.accent, 20f);
                    head.margin = new Vector4(8, 22, 0, 0);
                }
                BuildEntry(content, entry);
                EntryCount++;
            }

            if (!string.IsNullOrWhiteSpace(data.creditsNote))
            {
                var note = UI.Text(content, "Note", data.creditsNote, T.Body, 22f, T.textMuted, TextAlignmentOptions.Center,
                    FontStyles.Italic);
                note.margin = new Vector4(20, 24, 20, 0);
                note.lineSpacing = 6f;
            }
        }

        // The main "Concept, Design & Development" credit is shown in the creator card at the top.
        static bool IsCreatorEntry(CreditEntry e, DeveloperInfo dev) =>
            !string.IsNullOrWhiteSpace(dev.name) &&
            (e.section ?? "").StartsWith("Concept", System.StringComparison.OrdinalIgnoreCase);

        void BuildCreatorCard(Transform content)
        {
            var dev = App.Data.developer;
            if (string.IsNullOrWhiteSpace(dev.name)) return;
            var card = UI.Rounded(content, "Creator", T.ink, 36);
            card.gameObject.AddComponent<RectMask2D>();
            UI.VStack(card.gameObject, 10, new RectOffset(44, 44, 44, 44), TextAnchor.UpperCenter);
            var glow = UI.Image(card.transform, "Glow", UISprites.Glow, UITheme.WithAlpha(T.accent, 0.6f));
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(900, 700), new Vector2(0, 300));
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glow.transform.SetSiblingIndex(0);

            var cafe = Placeholder.Strip(App.Data.restaurant.name, "Cafe");
            UI.Eyebrow(card.transform, cafe + "  AR Menu", T.accentLight, 20f).alignment = TextAlignmentOptions.Center;
            UI.Eyebrow(card.transform, "Done by", UITheme.WithAlpha(T.onInk, 0.6f), 18f).alignment = TextAlignmentOptions.Center;
            UI.Text(card.transform, "Name", dev.name, T.Display, 64f, T.onInk, TextAlignmentOptions.Center);
            if (!string.IsNullOrWhiteSpace(dev.role))
                UI.Text(card.transform, "Role", dev.role, T.Body, 24f, T.accentLight, TextAlignmentOptions.Center);
            if (!string.IsNullOrWhiteSpace(dev.link))
                UI.Text(card.transform, "Link", dev.link, T.Strong, 22f, UITheme.WithAlpha(T.onInk, 0.55f), TextAlignmentOptions.Center);
            foreach (var mate in dev.team)
            {
                if (string.IsNullOrWhiteSpace(mate)) continue;
                var line = UI.Image(card.transform, "Divider", UISprites.White, UITheme.WithAlpha(T.onInk, 0.15f));
                UI.Size(line.gameObject, -1, 2).flexibleWidth = 1f;
                UI.Eyebrow(card.transform, "Teammate", UITheme.WithAlpha(T.onInk, 0.6f), 18f).alignment = TextAlignmentOptions.Center;
                UI.Text(card.transform, "Teammate", mate.Trim(), T.Display, 48f, T.onInk, TextAlignmentOptions.Center);
            }
            UI.Text(card.transform, "Work", "AR experience, UI/UX design, menu content and Unity development", T.Body, 22f,
                UITheme.WithAlpha(T.onInk, 0.6f), TextAlignmentOptions.Center);
        }

        void BuildEntry(Transform content, CreditEntry e)
        {
            var card = UI.Rounded(content, "Credit " + e.title, T.surface, 28);
            var stroke = UI.Rounded(card.transform, "Stroke", T.stroke, 28, false, true);
            stroke.rectTransform.Stretch();
            stroke.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UI.VStack(card.gameObject, 6, new RectOffset(34, 34, 28, 28));
            UI.Text(card.transform, "Title", e.title, T.Display, 34f, T.textPrimary);
            if (!string.IsNullOrWhiteSpace(e.author))
                UI.Text(card.transform, "Author", e.author, T.Body, 25f, T.accentDeep);
            if (!string.IsNullOrWhiteSpace(e.detail))
                UI.Text(card.transform, "Detail", e.detail, T.Body, 23f, T.textSecondary);
            if (!string.IsNullOrWhiteSpace(e.license))
            {
                var row = UI.Rect("LicenseRow", card.transform);
                UI.HStack(row.gameObject, 0, new RectOffset(0, 0, 8, 0));
                FoodBadges.Pill(row, T, e.license, null, T.surfaceHigh, T.accentDeep, false, 40f, 17f);
            }
        }

        protected override void AnimateIn()
        {
            Group.alpha = 1f;
            var c = mBackdrop.color;
            mBackdrop.color = new Color(c.r, c.g, c.b, 0f);
            Tween.To((mBackdrop, "fade"), 0.3f, t => mBackdrop.color = new Color(c.r, c.g, c.b, 0.6f * t));
            mPanel.anchoredPosition = new Vector2(0, -Root.rect.height - 200f);
            Tween.Move(mPanel, Vector2.zero, 0.45f, Ease.OutQuint);
        }

        protected override void AnimateOut(Action done)
        {
            var c = mBackdrop.color;
            var a = c.a;
            Tween.To((mBackdrop, "fade"), 0.22f, t => mBackdrop.color = new Color(c.r, c.g, c.b, Mathf.Lerp(a, 0f, t)));
            Tween.Move(mPanel, new Vector2(0, -Root.rect.height - 200f), 0.26f, Ease.InCubic, done);
        }
    }
}
