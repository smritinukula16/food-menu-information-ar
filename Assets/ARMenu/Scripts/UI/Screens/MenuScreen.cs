using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>Home screen: restaurant header, welcome, AR call-to-action, categories and dish cards.</summary>
    public sealed class MenuScreen : UIScreen
    {
        public const string AllCategory = "__all";
        public const string FavouritesCategory = "__favourites";
        const float HeaderHeight = 156f;

        readonly List<Chip> mChips = new List<Chip>();
        readonly List<FoodCardView> mCards = new List<FoodCardView>();
        RectTransform mList, mEmpty;
        ScrollRect mScroll;
        TextMeshProUGUI mCountLabel, mSectionTitle, mEmptyTitle, mEmptyBody;
        Button mDockCart;
        TextMeshProUGUI mDockCartLabel;
        public string Category { get; private set; } = AllCategory;
        public CartButton HeaderCart { get; private set; }
        public Button ScanButton { get; private set; }
        public Button FavouritesButton { get; private set; }
        public Button CreditsButton { get; private set; }
        public IReadOnlyList<FoodCardView> Cards => mCards;
        public IReadOnlyList<Chip> Chips => mChips;
        public int VisibleCardCount { get; private set; }

        public MenuScreen(ARMenuApp app, RectTransform parent) : base(app, "MenuScreen", parent)
        {
            var bg = UI.Image(Root, "Background", UISprites.White, T.background, true);
            bg.rectTransform.Stretch();
            var glow = UI.Image(Root, "AmbientGlow", UISprites.Glow, UITheme.WithAlpha(T.accentLight, 0.45f));
            glow.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1500, 1500), new Vector2(520, 560));

            var safe = CreateSafeArea(Root);
            BuildHeader(safe);

            var content = UI.VerticalScroll(safe, "Scroll", out mScroll, new RectOffset(48, 48, 8, 320), 36);
            ((RectTransform)mScroll.transform).Stretch(0, 0, HeaderHeight, 0);

            BuildWelcome(content);
            BuildScanCard(content);
            BuildMenuHeader(content);
            BuildCategories(content);

            mList = UI.Rect("Dishes", content);
            UI.VStack(mList.gameObject, 28);
            foreach (var item in App.Data.items) mCards.Add(new FoodCardView(mList, App, item));
            BuildEmptyState(mList);
            BuildFooter(content);

            BuildDock(safe);

            App.Orders.CartChanged += RefreshDock;
            App.Orders.FavouriteChanged += id =>
            {
                foreach (var c in mCards) c.RefreshFavourite();
                if (Category == FavouritesCategory) ApplyFilter(false);
            };
            App.ImagesChanged += () => { foreach (var c in mCards) c.RefreshImage(); };

            SelectCategory(AllCategory, false);
            RefreshDock();
        }

        // ------------------------------------------------------------------ header

        void BuildHeader(Transform safe)
        {
            var header = UI.Rect("Header", safe).Strip(true, HeaderHeight);
            UI.HStack(header.gameObject, 26, new RectOffset(48, 48, 28, 12), TextAnchor.MiddleLeft);

            BuildLogo(header, 104f);

            var titles = UI.Rect("Titles", header);
            UI.VStack(titles.gameObject, 2, null, TextAnchor.MiddleLeft);
            UI.Size(titles.gameObject, -1, -1, 1f);
            UI.Eyebrow(titles, "Welcome to", T.accent, 20f);
            var name = UI.Text(titles, "RestaurantName", "", T.Display, 46f, T.textPrimary);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UI.SetValue(name, App.Data.restaurant.name, "Restaurant name");

            FavouritesButton = UI.CircleButton(header, "FavouritesButton", UISprites.HeartOutline, T.surfaceHigh, T.textPrimary, 92f,
                () => SelectCategory(FavouritesCategory, true), 0.46f);
            HeaderCart = new CartButton(header, App, 92f, T.surfaceHigh);
        }

        void BuildLogo(Transform parent, float size)
        {
            var circle = UI.Image(parent, "Logo", UISprites.Circle, T.ink);
            UI.Size(circle.gameObject, size, size);
            circle.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            Texture logo = null;
            var path = App.Data.restaurant.logoResource;
            if (!string.IsNullOrEmpty(path))
            {
                var sprite = Resources.Load<Sprite>(path);
                logo = sprite ? sprite.texture : Resources.Load<Texture2D>(path);
            }

            if (logo)
            {
                var raw = UI.Rect("LogoImage", circle.transform).Stretch().gameObject.AddComponent<RawImage>();
                raw.texture = logo;
                raw.raycastTarget = false;
            }
            else
            {
                var name = Placeholder.Strip(App.Data.restaurant.name, "Restaurant");
                // "2616 Cafe" -> "2616"; other names -> first letter.
                var firstWord = string.IsNullOrEmpty(name) ? "R" : name.Split(' ')[0];
                var letter = firstWord.Length <= 4 && char.IsDigit(firstWord[0]) ? firstWord : firstWord.Substring(0, 1).ToUpperInvariant();
                var mono = UI.Text(circle.transform, "Monogram", letter, T.Display, size * (letter.Length > 1 ? 0.30f : 0.46f),
                    T.accentLight, TextAlignmentOptions.Center);
                mono.rectTransform.Stretch(0, 0, 0, size * 0.04f);
            }
            var ring = UI.Image(circle.transform, "Ring", UISprites.Ring, UITheme.WithAlpha(T.accentLight, 0.9f));
            ring.rectTransform.Stretch();
        }

        // ------------------------------------------------------------------ content blocks

        static Image Card(Transform parent, string name, UITheme t, Color bg, Color stroke, float radius, RectOffset padding,
            float spacing)
        {
            var card = UI.Rounded(parent, name, bg, radius);
            var outline = UI.Rounded(card.transform, "Stroke", stroke, radius, false, true);
            outline.rectTransform.Stretch();
            outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UI.VStack(card.gameObject, spacing, padding);
            return card;
        }

        void BuildWelcome(Transform content)
        {
            var card = Card(content, "WelcomeCard", T, T.surface, UITheme.WithAlpha(T.accent, 0.18f), 36,
                new RectOffset(44, 44, 44, 44), 16);
            card.gameObject.AddComponent<RectMask2D>();
            var glow = UI.Image(card.transform, "Glow", UISprites.Glow, UITheme.WithAlpha(T.accentLight, 0.55f));
            glow.rectTransform.Place(new Vector2(1f, 1f), new Vector2(760, 760), new Vector2(300, 320));
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glow.transform.SetSiblingIndex(0);

            UI.Eyebrow(card.transform, "Welcome", T.accent);
            var headline = UI.Text(card.transform, "Headline", "", T.Display, 52f, T.textPrimary);
            headline.lineSpacing = -6f;
            UI.SetValue(headline, App.Data.restaurant.welcome, "Add a welcome message");
            var description = UI.Text(card.transform, "Description", "", T.Body, 28f, T.textSecondary);
            description.lineSpacing = 8f;
            UI.SetValue(description, App.Data.restaurant.description, "Add a short description of the restaurant");

            var divider = UI.Image(card.transform, "Divider", UISprites.White, UITheme.WithAlpha(T.accent, 0.2f));
            UI.Size(divider.gameObject, -1, 2);

            var features = UI.Rect("Features", card.transform);
            var row = UI.HStack(features.gameObject, 12, new RectOffset(0, 0, 8, 0));
            row.childForceExpandWidth = true;
            Feature(features, UISprites.Cube, "3D dishes");
            Feature(features, UISprites.Scan, "Scan & view");
            Feature(features, UISprites.Cart, "Easy ordering");
        }

        void Feature(Transform parent, Sprite icon, string label)
        {
            var item = UI.Rect("Feature " + label, parent);
            UI.HStack(item.gameObject, 12, null, TextAnchor.MiddleLeft);
            UI.Icon(item, icon, T.accent, 34);
            var t = UI.Text(item, "Label", label, T.Body, 24f, T.textSecondary);
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void BuildScanCard(Transform content)
        {
            var card = Card(content, "ScanCard", T, T.ink, UITheme.WithAlpha(T.accentLight, 0.35f), 36,
                new RectOffset(40, 40, 40, 40), 30);
            card.gameObject.AddComponent<RectMask2D>();
            var glow = UI.Image(card.transform, "Glow", UISprites.Glow, UITheme.WithAlpha(T.accent, 0.55f));
            glow.rectTransform.Place(new Vector2(0f, 1f), new Vector2(700, 700), new Vector2(-260, 300));
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glow.transform.SetSiblingIndex(0);

            var row = UI.Rect("Row", card.transform);
            UI.HStack(row.gameObject, 30, null, TextAnchor.MiddleLeft);
            var iconBg = UI.Image(row, "IconBg", UISprites.Circle, T.accentLight);
            UI.Size(iconBg.gameObject, 112, 112);
            var icon = UI.Image(iconBg.transform, "Icon", UISprites.Scan, T.ink);
            icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(58, 58), Vector2.zero);
            iconBg.gameObject.AddComponent<Pulse>().scaleAmount = 0.05f;

            var texts = UI.Rect("Texts", row);
            UI.VStack(texts.gameObject, 8);
            UI.Size(texts.gameObject, -1, -1, 1f);
            UI.Text(texts, "Title", "See it on your table", T.Display, 40f, T.onInk);
            UI.Text(texts, "Body", "Point your camera at the 2616 menu card to see each dish on your table in 3D.", T.Body, 26f,
                UITheme.WithAlpha(T.onInk, 0.72f)).lineSpacing = 6f;

            ScanButton = UI.PillButton(card.transform, "ScanButton", "Scan the Menu Card", UISprites.Scan, T.accentLight, T.ink, 100f,
                () => App.OpenScan(null));
        }

        void BuildMenuHeader(Transform content)
        {
            var row = UI.Rect("MenuHeader", content);
            UI.HStack(row.gameObject, 16, new RectOffset(0, 0, 18, 0), TextAnchor.LowerLeft);
            var titles = UI.Rect("Titles", row);
            UI.VStack(titles.gameObject, 4);
            UI.Size(titles.gameObject, -1, -1, 1f);
            UI.Eyebrow(titles, "Explore", T.accent);
            mSectionTitle = UI.Text(titles, "Title", "Our Menu", T.Display, 58f, T.textPrimary);
            mCountLabel = UI.Text(row, "Count", "", T.Body, 26f, T.textSecondary, TextAlignmentOptions.BottomRight);
            mCountLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void BuildCategories(Transform content)
        {
            var root = UI.Rect("Categories", content);
            UI.Size(root.gameObject, -1, 88);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var scroll = root.gameObject.AddComponent<NestedScrollRect>();

            var row = UI.Rect("Row", root);
            row.anchorMin = new Vector2(0, 0);
            row.anchorMax = new Vector2(0, 1);
            row.pivot = new Vector2(0, 0.5f);
            row.sizeDelta = Vector2.zero;
            UI.HStack(row.gameObject, 16).childForceExpandHeight = true;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = row;
            scroll.viewport = root;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.decelerationRate = 0.12f;

            mChips.Add(new Chip(row, T, AllCategory, "All", null, 84, id => SelectCategory(id, true)));
            foreach (var c in App.Data.categories) mChips.Add(new Chip(row, T, c, c, null, 84, id => SelectCategory(id, true)));
            mChips.Add(new Chip(row, T, FavouritesCategory, "Favourites", UISprites.Heart, 84, id => SelectCategory(id, true)));
        }

        void BuildEmptyState(Transform list)
        {
            var card = Card(list, "EmptyState", T, T.surface, UITheme.WithAlpha(T.stroke, 1f), 32,
                new RectOffset(48, 48, 56, 56), 14);
            card.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            mEmpty = card.rectTransform;
            UI.Icon(card.transform, UISprites.Plate, T.textMuted, 96);
            mEmptyTitle = UI.Text(card.transform, "Title", "", T.Display, 38f, T.textPrimary, TextAlignmentOptions.Center);
            mEmptyBody = UI.Text(card.transform, "Body", "", T.Body, 26f, T.textSecondary, TextAlignmentOptions.Center);
        }

        /// <summary>Brand footer: cafe sign-off, creator watermark and the Credits link.</summary>
        void BuildFooter(Transform content)
        {
            var footer = UI.Rect("Footer", content);
            UI.VStack(footer.gameObject, 10, new RectOffset(0, 0, 40, 0), TextAnchor.UpperCenter);
            var line = UI.Image(footer, "Divider", UISprites.White, T.stroke);
            UI.Size(line.gameObject, -1, 2);
            UI.Spacer(footer, 18);
            var cafe = Placeholder.Strip(App.Data.restaurant.name, "Cafe");
            UI.Text(footer, "Cafe", cafe.ToUpperInvariant(), T.Display, 34f, T.textPrimary, TextAlignmentOptions.Center).characterSpacing = 10f;
            UI.Text(footer, "Tagline", "AR Menu Experience", T.Body, 24f, T.textMuted, TextAlignmentOptions.Center);
            var dev = App.Data.developer;
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                UI.Spacer(footer, 10);
                UI.Eyebrow(footer, "Done by", T.accent, 18f).alignment = TextAlignmentOptions.Center;
                UI.Text(footer, "Developer", dev.DoneBy, T.Display, 40f, T.textPrimary, TextAlignmentOptions.Center);
            }
            UI.Spacer(footer, 14);
            var holder = UI.Rect("CreditsHolder", footer);
            UI.HStack(holder.gameObject, 0, null, TextAnchor.MiddleCenter);
            CreditsButton = UI.PillButton(holder, "CreditsButton", "Credits & Acknowledgements", UISprites.Info,
                T.surfaceHigh, T.accentDeep, 88, App.OpenCredits, -1f, false, 26f);
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                var year = string.IsNullOrWhiteSpace(dev.year) ? "" : dev.year + " ";
                UI.Text(footer, "Copyright", "\u00A9 " + year + dev.DoneBy + ". All rights reserved.", T.Body, 20f, T.textMuted,
                    TextAlignmentOptions.Center);
            }
        }

        void BuildDock(Transform safe)
        {
            var fade = UI.Image(Root, "DockFade", UISprites.FadeUp, T.background);
            fade.rectTransform.Strip(false, 360);
            fade.rectTransform.localScale = new Vector3(1, -1, 1);
            fade.rectTransform.pivot = new Vector2(0.5f, 1f); // flipped: keep it anchored to the bottom edge
            fade.transform.SetSiblingIndex(safe.GetSiblingIndex());

            var dock = UI.Rect("Dock", safe).Strip(false, 112, 36, 48);
            UI.HStack(dock.gameObject, 20, null, TextAnchor.MiddleCenter).childForceExpandHeight = true;

            var scan = UI.PillButton(dock, "DockScanButton", "Scan Menu Card", UISprites.Scan, T.accent, T.onAccent, 112, () => App.OpenScan(null));
            UI.Size(scan.gameObject, -1, -1, 1f);

            mDockCart = UI.PillButton(dock, "DockCartButton", "Order", UISprites.Cart, T.surfaceHigh, T.textPrimary, 112, App.OpenCart);
            var outline = UI.Rounded(mDockCart.transform, "Stroke", UITheme.WithAlpha(T.accent, 0.6f), 56, false, true);
            outline.rectTransform.Stretch();
            outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            mDockCartLabel = mDockCart.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }

        // ------------------------------------------------------------------ behaviour

        void RefreshDock()
        {
            var count = App.Orders.TotalUnits;
            var show = count > 0;
            if (mDockCart.gameObject.activeSelf != show)
            {
                mDockCart.gameObject.SetActive(show);
                if (show) Tween.Punch(mDockCart.transform, 0.12f, 0.3f);
            }
            mDockCartLabel.text = "Order (" + count + ")";
        }

        public void SelectCategory(string id, bool animate)
        {
            Category = id;
            foreach (var chip in mChips) chip.SetSelected(chip.Id == id);
            mSectionTitle.text = id == AllCategory ? "Our Menu" : id == FavouritesCategory ? "Favourites" : id;
            ApplyFilter(animate);
            if (animate && id == FavouritesCategory) ScrollToMenu();
        }

        void ApplyFilter(bool animate)
        {
            var visible = 0;
            foreach (var card in mCards)
            {
                var item = card.Item;
                var show = Category == AllCategory ||
                           (Category == FavouritesCategory ? App.Orders.IsFavourite(item.id) : item.category == Category);
                card.Root.gameObject.SetActive(show);
                if (show)
                {
                    if (animate) card.AnimateIn(visible * 0.05f);
                    visible++;
                }
            }
            VisibleCardCount = visible;

            mEmpty.gameObject.SetActive(visible == 0);
            if (visible == 0)
            {
                if (Category == FavouritesCategory)
                {
                    mEmptyTitle.text = "No favourites yet";
                    mEmptyBody.text = "Tap the heart on any dish to save it here.";
                }
                else
                {
                    mEmptyTitle.text = "No dishes here yet";
                    mEmptyBody.text = "Dishes in this category will appear here once they are added to the menu.";
                }
            }
            mCountLabel.text = visible == 1 ? "1 dish" : visible + " dishes";
        }

        void ScrollToMenu()
        {
            Canvas.ForceUpdateCanvases();
            var content = mScroll.content;
            var viewportHeight = ((RectTransform)mScroll.viewport).rect.height;
            var maxScroll = Mathf.Max(1f, content.rect.height - viewportHeight);
            var target = Mathf.Clamp(-mSectionTitle.transform.parent.parent.localPosition.y - 40f, 0f, maxScroll);
            var from = content.anchoredPosition.y;
            Tween.To((content, "scroll"), 0.45f,
                t => content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Lerp(from, target, t)), Ease.OutCubic);
        }

        protected override void OnShow()
        {
            foreach (var c in mCards) c.RefreshFavourite();
        }
    }

    /// <summary>Dish card used in the menu list.</summary>
    public sealed class FoodCardView
    {
        public readonly MenuItemData Item;
        public readonly RectTransform Root;
        readonly RectTransform mBody;
        readonly CanvasGroup mGroup;
        readonly DishImage mImage;
        readonly Image mFavIcon;
        readonly ARMenuApp mApp;
        public Button OpenButton { get; }
        public Button AddButton { get; }
        public Button FavouriteButton { get; }

        public FoodCardView(Transform parent, ARMenuApp app, MenuItemData item)
        {
            mApp = app;
            Item = item;
            var t = app.Theme;

            Root = UI.Rect("Card " + item.name, parent);
            UI.Size(Root.gameObject, -1, 300);
            mGroup = Root.gameObject.AddComponent<CanvasGroup>();
            UI.Shadow(Root, 30, -12, 1f);

            var body = UI.Rounded(Root, "Body", t.surface, 32, true);
            mBody = body.rectTransform.Stretch();
            OpenButton = UI.Button(body, () => app.OpenDetail(item));
            var outline = UI.Rounded(body.transform, "Stroke", t.stroke, 32, false, true);
            outline.rectTransform.Stretch();
            outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var row = UI.HStack(body.gameObject, 28, new RectOffset(24, 20, 24, 24));
            row.childForceExpandHeight = true;

            mImage = new DishImage(body.transform, t, 24);
            UI.Size(mImage.Root.gameObject, 252, 252);

            var info = UI.Rect("Info", body.transform);
            UI.VStack(info.gameObject, 6);
            UI.Size(info.gameObject, -1, -1, 1f);

            var badges = UI.Rect("Badges", info);
            UI.HStack(badges.gameObject, 10);
            if (item.isChefSpecial) FoodBadges.ChefPill(badges, t, 40);
            if (item.isPopular) FoodBadges.PopularPill(badges, t, 40);
            badges.gameObject.SetActive(item.isChefSpecial || item.isPopular);

            var name = UI.Text(info, "Name", item.name, t.Display, 42f, t.textPrimary);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;

            var tagline = UI.Text(info, "Tagline", "", t.Body, 24f, t.textSecondary);
            tagline.overflowMode = TextOverflowModes.Ellipsis;
            UI.SetValue(tagline, item.tagline, "Add a tagline");
            UI.Size(tagline.gameObject, -1, 62);

            UI.Spacer(info).gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;

            var bottom = UI.Rect("Bottom", info);
            UI.HStack(bottom.gameObject, 12, null, TextAnchor.MiddleLeft);
            FoodBadges.DietMark(bottom, t, item.DietType, 30);
            var spice = FoodBadges.SpiceMeter(bottom, t, 26, out var flames);
            FoodBadges.SetSpice(spice, flames, t, item.spiceLevel, true);
            var price = UI.Text(bottom, "Price", "", t.Strong, 34f, t.accent);
            price.textWrappingMode = TextWrappingModes.NoWrap;
            app.ApplyPrice(price, item);

            var actions = UI.Rect("Actions", body.transform);
            UI.VStack(actions.gameObject, 0, null, TextAnchor.UpperCenter, false);
            UI.Size(actions.gameObject, 80);
            FavouriteButton = UI.CircleButton(actions, "Favourite", UISprites.HeartOutline, new Color(0, 0, 0, 0), t.textSecondary, 76,
                () => { app.ToggleFavourite(item); Tween.Punch(mFavIcon.transform, 0.35f, 0.35f); }, 0.5f);
            mFavIcon = FavouriteButton.transform.Find("Icon").GetComponent<Image>();
            UI.Spacer(actions).gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
            AddButton = UI.CircleButton(actions, "QuickAdd", UISprites.Plus, t.accent, t.onAccent, 76, () => app.AddToCart(item, 1), 0.44f);

            RefreshImage();
            RefreshFavourite();
        }

        public void RefreshImage() => mImage.Set(mApp.GetImage(Item));

        public void RefreshFavourite()
        {
            var fav = mApp.Orders.IsFavourite(Item.id);
            mFavIcon.sprite = fav ? UISprites.Heart : UISprites.HeartOutline;
            mFavIcon.color = fav ? mApp.Theme.accent : mApp.Theme.textSecondary;
        }

        public void AnimateIn(float delay)
        {
            mGroup.alpha = 0f;
            mBody.anchoredPosition = new Vector2(0, -36);
            Tween.Fade(mGroup, 1f, 0.35f, null, Ease.OutCubic, delay);
            Tween.Move(mBody, Vector2.zero, 0.45f, Ease.OutQuint, null, delay);
        }
    }
}
