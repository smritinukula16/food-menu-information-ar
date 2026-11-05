using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>
    /// Full food information panel (bottom sheet). Scrollable; sticky action bar with quantity and Add to order.
    /// Every value comes from menu_data.json / scene data – missing values show as visible placeholders.
    /// </summary>
    public sealed class FoodDetailSheet : UIScreen
    {
        const float ActionBarHeight = 176f;

        readonly RectTransform mPanel;
        readonly Image mBackdrop;
        readonly ScrollRect mScroll;
        readonly DishImage mHero;
        readonly RectTransform mBadges;
        readonly TextMeshProUGUI mName, mTagline, mPrice, mInOrder, mDescription, mIngredients, mPreparation, mTaste,
            mAllergens, mAddLabel, mView3DLabel, mMoreLabel;
        readonly Image mAllergenIcon, mMoreChevron, mFavIcon;
        readonly RectTransform mMoreContent;
        readonly CanvasGroup mMoreGroup;
        readonly FactTile mSpice, mPortion, mCalories, mProtein, mOil, mDiet;
        readonly Image[] mSpiceFlames;
        readonly RectTransform mSpiceMeter;

        public MenuItemData Item { get; private set; }
        public bool OpenedFromAR { get; private set; }
        public bool MoreInfoExpanded { get; private set; }
        public QuantityStepper Stepper { get; }
        public Button BackButton { get; }
        public Button FavouriteButton { get; }
        public Button View3DButton { get; }
        public Button View3DInlineButton { get; }
        public Button MoreInfoButton { get; }
        public Button AddButton { get; }

        public FoodDetailSheet(ARMenuApp app, RectTransform parent) : base(app, "FoodDetailSheet", parent)
        {
            mBackdrop = UI.Image(Root, "Backdrop", UISprites.White, new Color(0, 0, 0, 0.72f), true);
            mBackdrop.rectTransform.Stretch();
            UI.Button(mBackdrop, App.Back, false);

            var panelImg = UI.Rounded(Root, "Panel", T.background, 44, true);
            mPanel = panelImg.rectTransform.Stretch(0, 0, 56, -48);
            var topSafe = UI.Rect("TopSafe", mPanel).Stretch(0, 0, 0, 48);
            var safeFit = topSafe.gameObject.AddComponent<SafeAreaFitter>();
            safeFit.bottom = false;
            var inner = UI.Rect("Inner", topSafe).Stretch(0, 0, 40, 0);
            var bottomSafe = UI.Rect("BottomSafe", inner).Stretch();
            bottomSafe.gameObject.AddComponent<SafeAreaFitter>().top = false;

            // ---- scroll content
            var content = UI.VerticalScroll(inner, "Scroll", out mScroll, new RectOffset(40, 40, 24, 80), 26);
            ((RectTransform)mScroll.transform).Stretch(0, 0, 0, ActionBarHeight);

            mHero = new DishImage(content, T, 40);
            UI.Size(mHero.Root.gameObject, -1, 620);
            var heroFade = UI.Image(mHero.Root, "Fade", UISprites.FadeUp, UITheme.WithAlpha(T.background, 0.85f));
            heroFade.rectTransform.Strip(false, 200);
            heroFade.rectTransform.pivot = new Vector2(0.5f, 1f);
            heroFade.rectTransform.localScale = new Vector3(1, -1, 1);

            mBadges = UI.Rect("Badges", content);
            UI.HStack(mBadges.gameObject, 12);

            mName = UI.Text(content, "Name", "", T.Display, 72f, T.textPrimary);
            mName.lineSpacing = -8f;
            mTagline = UI.Text(content, "Tagline", "", T.Body, 30f, T.textSecondary, TextAlignmentOptions.TopLeft, FontStyles.Italic);

            var priceRow = UI.Rect("PriceRow", content);
            UI.HStack(priceRow.gameObject, 16, null, TextAnchor.MiddleLeft);
            mPrice = UI.Text(priceRow, "Price", "", T.Strong, 54f, T.accent);
            mPrice.textWrappingMode = TextWrappingModes.NoWrap;
            UI.Spacer(priceRow, -1, 1f);
            mInOrder = UI.Text(priceRow, "InOrder", "", T.Body, 24f, T.accentDeep, TextAlignmentOptions.Right);
            mInOrder.textWrappingMode = TextWrappingModes.NoWrap;

            // ---- quick facts
            var grid = UI.Rect("Facts", content);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(320, 156);
            gl.spacing = new Vector2(20, 20);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            mSpice = new FactTile(grid, T, "Spice level");
            mSpiceMeter = FoodBadges.SpiceMeter(mSpice.ValueRow, T, 30, out mSpiceFlames);
            mSpiceMeter.SetAsFirstSibling();
            mPortion = new FactTile(grid, T, "Portion");
            mDiet = new FactTile(grid, T, "Diet");
            mCalories = new FactTile(grid, T, "Calories");
            mProtein = new FactTile(grid, T, "Protein");
            mOil = new FactTile(grid, T, "Oil level");

            View3DInlineButton = UI.PillButton(content, "View3DInline", "View in 3D on your table", UISprites.Cube,
                UITheme.WithAlpha(T.accentLight, 0.4f), T.accentDeep, 104, OnView3D);
            var stroke = UI.Rounded(View3DInlineButton.transform, "Stroke", UITheme.WithAlpha(T.accent, 0.7f), 52, false, true);
            stroke.rectTransform.Stretch();
            stroke.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            mView3DLabel = View3DInlineButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            mDescription = Section(content, "About this dish");
            mIngredients = Section(content, "Ingredients");

            // ---- "More information" expander
            var moreBg = UI.Rounded(content, "MoreInfoButton", T.surfaceHigh, 28, true);
            MoreInfoButton = UI.Button(moreBg, ToggleMoreInfo);
            UI.HStack(moreBg.gameObject, 18, new RectOffset(32, 32, 0, 0), TextAnchor.MiddleLeft);
            UI.Size(moreBg.gameObject, -1, 100);
            UI.Icon(moreBg.transform, UISprites.Info, T.accent, 40);
            mMoreLabel = UI.Text(moreBg.transform, "Label", "More information", T.Strong, 28f, T.textPrimary);
            UI.Size(mMoreLabel.gameObject, -1, -1, 1f);
            mMoreChevron = UI.Icon(moreBg.transform, UISprites.ChevronDown, T.textSecondary, 40);

            mMoreContent = UI.Rect("MoreInfo", content);
            UI.VStack(mMoreContent.gameObject, 26, new RectOffset(0, 0, 6, 0));
            mMoreGroup = mMoreContent.gameObject.AddComponent<CanvasGroup>();
            mPreparation = Section(mMoreContent, "Preparation style");
            mTaste = Section(mMoreContent, "Taste profile");

            var allergenBox = UI.Rounded(mMoreContent, "Allergens", T.surfaceHigh, 28);
            UI.VStack(allergenBox.gameObject, 12, new RectOffset(32, 32, 28, 28));
            var allergenHeader = UI.Rect("Header", allergenBox.transform);
            UI.HStack(allergenHeader.gameObject, 12);
            mAllergenIcon = UI.Icon(allergenHeader, UISprites.Info, T.warning, 32);
            UI.Eyebrow(allergenHeader, "Allergen information", T.accent);
            mAllergens = UI.Text(allergenBox.transform, "Text", "", T.Body, 27f, T.textSecondary);
            mAllergens.lineSpacing = 8f;

            var note = UI.Text(mMoreContent, "NutritionNote",
                "Calories, protein and oil level are taken from the menu data and may vary by portion.", T.Body, 22f,
                T.textMuted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            note.lineSpacing = 6f;

            var dev = App.Data.developer;
            if (!string.IsNullOrWhiteSpace(dev.name))
            {
                var mark = UI.Text(content, "Watermark",
                    Placeholder.Strip(App.Data.restaurant.name, "Cafe") + "  \u00B7  AR Menu by " + dev.name,
                    T.Body, 21f, T.textMuted, TextAlignmentOptions.Center);
                mark.margin = new Vector4(0, 24, 0, 0);
            }

            // ---- top bar (floats over the hero image)
            var topBar = UI.Rect("TopBar", inner).Strip(true, 128);
            UI.HStack(topBar.gameObject, 18, new RectOffset(64, 64, 40, 0), TextAnchor.UpperLeft);
            var barBg = UITheme.WithAlpha(T.background, 0.78f);
            BackButton = UI.CircleButton(topBar, "Back", UISprites.Back, barBg, T.textPrimary, 88, App.Back);
            UI.Spacer(topBar, -1, 1f);
            FavouriteButton = UI.CircleButton(topBar, "Favourite", UISprites.HeartOutline, barBg, T.textPrimary, 88, OnFavourite);
            mFavIcon = FavouriteButton.transform.Find("Icon").GetComponent<Image>();
            View3DButton = UI.CircleButton(topBar, "View3D", UISprites.Cube, T.accent, T.onAccent, 88, OnView3D);

            var grabber = UI.Image(inner, "Grabber", UISprites.Rounded, UITheme.WithAlpha(T.textMuted, 0.6f));
            UI.MakeRounded(grabber, 4);
            grabber.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(88, 8), new Vector2(0, 16));

            // ---- sticky action bar
            var bar = UI.Image(bottomSafe, "ActionBar", UISprites.White, T.backgroundRaised, true);
            bar.rectTransform.Strip(false, ActionBarHeight);
            var line = UI.Image(bar.transform, "TopLine", UISprites.White, T.stroke);
            line.rectTransform.Strip(true, 2);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // Fills the home-indicator area below the bar.
            var under = UI.Image(bar.transform, "Underlay", UISprites.White, T.backgroundRaised);
            under.rectTransform.Stretch(0, 0, ActionBarHeight, -400);
            under.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UI.HStack(bar.gameObject, 22, new RectOffset(40, 40, 30, 42), TextAnchor.MiddleLeft).childForceExpandHeight = true;
            Stepper = new QuantityStepper(bar.transform, T, 104, 270);
            Stepper.Changed += _ => RefreshAddLabel();
            AddButton = UI.PillButton(bar.transform, "AddToOrder", "Add to order", UISprites.Cart, T.accent, T.onAccent, 104, OnAdd);
            UI.Size(AddButton.gameObject, -1, -1, 1f);
            mAddLabel = AddButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();

            App.Orders.CartChanged += RefreshInOrder;
            App.Orders.FavouriteChanged += id => { if (Item != null && id == Item.id) RefreshFavourite(); };
            App.ImagesChanged += () => { if (Item != null) mHero.Set(App.GetImage(Item)); };
        }

        TextMeshProUGUI Section(Transform parent, string title)
        {
            var s = UI.Rect("Section " + title, parent);
            UI.VStack(s.gameObject, 12);
            UI.Eyebrow(s, title, T.accent);
            var body = UI.Text(s, "Body", "", T.Body, 30f, T.textPrimary);
            body.lineSpacing = 10f;
            return body;
        }

        // ------------------------------------------------------------------ binding

        public void Bind(MenuItemData item, bool fromAR)
        {
            Item = item;
            OpenedFromAR = fromAR;
            mHero.Set(App.GetImage(item));

            for (var i = mBadges.childCount - 1; i >= 0; i--)
            {
                var child = mBadges.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
            var diet = item.DietType;
            if (diet == Diet.Vegetarian)
                FoodBadges.Pill(mBadges, T, "Vegetarian", UISprites.VegMark, UITheme.WithAlpha(T.veg, 0.16f), T.veg);
            else if (diet == Diet.NonVegetarian)
                FoodBadges.Pill(mBadges, T, "Non-vegetarian", UISprites.NonVegMark, UITheme.WithAlpha(T.nonVeg, 0.18f), T.nonVeg);
            if (item.isChefSpecial) FoodBadges.ChefPill(mBadges, T);
            if (item.isPopular) FoodBadges.PopularPill(mBadges, T);
            if (!string.IsNullOrEmpty(item.category))
                FoodBadges.Pill(mBadges, T, item.category, null, T.surfaceHigh, T.accentDeep);

            mName.text = item.name;
            UI.SetValue(mTagline, item.tagline, "Add a tagline");
            if (!Placeholder.Is(item.tagline)) mTagline.fontStyle = FontStyles.Italic;
            App.ApplyPrice(mPrice, item, 54f);

            var spiceLabel = FoodBadges.SpiceLabel(item.spiceLevel);
            FoodBadges.SetSpice(mSpiceMeter, mSpiceFlames, T, item.spiceLevel, false);
            mSpiceMeter.gameObject.SetActive(item.spiceLevel > 0);
            mSpice.Set(spiceLabel, "Not set");
            mPortion.Set(Placeholder.Is(item.portionSize) ? null : item.portionSize, "Not set");
            mDiet.Set(FoodBadges.DietLabel(diet), "Not set");
            mCalories.Set(item.calories, "Not set");
            mProtein.Set(item.protein, "Not set");
            mOil.Set(Capitalise(item.oil), "Not set");

            UI.SetValue(mDescription, item.description, "Add a description of the dish");
            if (Placeholder.IsEmptyList(item.ingredients))
                UI.SetValue(mIngredients, item.ingredients.FirstOrDefault(), "Add ingredients");
            else
            {
                mIngredients.text = string.Join("   •   ", item.ingredients.Where(i => !Placeholder.Is(i)).Select(i => i.Trim()));
                mIngredients.color = T.textPrimary;
                mIngredients.fontStyle = FontStyles.Normal;
            }
            UI.SetValue(mPreparation, item.preparationStyle, "Add the preparation style");
            UI.SetValue(mTaste, item.tasteProfile, "Add the taste profile");

            if (item.allergensVerified)
            {
                var listed = item.allergens.Where(a => !Placeholder.Is(a)).Select(a => a.Trim()).ToArray();
                mAllergens.text = listed.Length > 0
                    ? "Contains: " + string.Join(", ", listed)
                    : "No major allergens in this recipe.";
                mAllergens.color = T.textPrimary;
                mAllergenIcon.color = T.success;
            }
            else
            {
                mAllergens.text = "Allergen information has not been verified for this dish yet. " +
                                  "If you have an allergy, please ask our staff before ordering.";
                mAllergens.color = T.textSecondary;
                mAllergenIcon.color = T.warning;
            }

            var hasModel = App.Presenter != null && App.Presenter.HasModel(item.modelKey);
            View3DButton.gameObject.SetActive(hasModel);
            View3DInlineButton.gameObject.SetActive(hasModel);
            mView3DLabel.text = fromAR ? "Back to the AR view" : "View in 3D on your table";

            SetMoreInfo(false, false);
            Stepper.SetValue(1, false);
            RefreshAddLabel();
            RefreshInOrder();
            RefreshFavourite();
            mScroll.verticalNormalizedPosition = 1f;
            mScroll.velocity = Vector2.zero;
        }

        static string Capitalise(string s) =>
            string.IsNullOrWhiteSpace(s) ? s : char.ToUpperInvariant(s.Trim()[0]) + s.Trim().Substring(1);

        void RefreshAddLabel()
        {
            if (Item == null) return;
            var qty = Stepper.Value;
            mAddLabel.text = Item.HasPrice ? "Add " + qty + "  ·  " + App.FormatAmount(Item.price * qty) : "Add " + qty + " to order";
        }

        void RefreshInOrder()
        {
            if (Item == null) return;
            var q = App.Orders.QuantityOf(Item.id);
            mInOrder.text = q > 0 ? q + " in your order" : "";
        }

        void RefreshFavourite()
        {
            var fav = Item != null && App.Orders.IsFavourite(Item.id);
            mFavIcon.sprite = fav ? UISprites.Heart : UISprites.HeartOutline;
            mFavIcon.color = fav ? T.accent : T.textPrimary;
        }

        // ------------------------------------------------------------------ actions

        void OnAdd()
        {
            if (Item == null) return;
            var qty = Stepper.Value;
            App.AddToCart(Item, qty);
            Stepper.SetValue(1, false);
            mAddLabel.text = "Added to order";
            Tween.Punch(AddButton.transform, 0.06f, 0.3f);
            Tween.Delay((this, "addLabel"), 1.4f, RefreshAddLabel);
        }

        void OnFavourite()
        {
            if (Item == null) return;
            App.ToggleFavourite(Item);
            Tween.Punch(mFavIcon.transform, 0.35f, 0.35f);
        }

        void OnView3D()
        {
            if (Item == null) return;
            App.OpenScan(Item);
        }

        void ToggleMoreInfo() => SetMoreInfo(!MoreInfoExpanded, true);

        void SetMoreInfo(bool expanded, bool animate)
        {
            MoreInfoExpanded = expanded;
            mMoreLabel.text = expanded ? "Less information" : "More information";
            mMoreContent.gameObject.SetActive(expanded);
            var targetRot = Quaternion.Euler(0, 0, expanded ? 180f : 0f);
            if (!animate)
            {
                mMoreChevron.transform.localRotation = targetRot;
                mMoreGroup.alpha = 1f;
                return;
            }
            var from = mMoreChevron.transform.localRotation;
            Tween.To((mMoreChevron, "rot"), 0.25f,
                t => mMoreChevron.transform.localRotation = Quaternion.Slerp(from, targetRot, t));
            if (expanded)
            {
                mMoreGroup.alpha = 0f;
                Tween.Fade(mMoreGroup, 1f, 0.3f);
                Tween.Delay((this, "scrollMore"), 0.05f, ScrollToMore);
            }
        }

        void ScrollToMore()
        {
            Canvas.ForceUpdateCanvases();
            var content = mScroll.content;
            var viewport = ((RectTransform)mScroll.viewport).rect.height;
            var max = Mathf.Max(0f, content.rect.height - viewport);
            var target = Mathf.Clamp(-MoreInfoButton.transform.localPosition.y - 140f, 0f, max);
            var from = content.anchoredPosition.y;
            if (target <= from) return;
            Tween.To((content, "scroll"), 0.4f,
                t => content.anchoredPosition = new Vector2(0, Mathf.Lerp(from, target, t)), Ease.OutCubic);
        }

        // ------------------------------------------------------------------ transitions

        protected override void AnimateIn()
        {
            Group.alpha = 1f;
            var c = mBackdrop.color;
            mBackdrop.color = new Color(c.r, c.g, c.b, 0f);
            Tween.To((mBackdrop, "fade"), 0.3f, t => mBackdrop.color = new Color(c.r, c.g, c.b, 0.72f * t));
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

        /// <summary>Compact tile in the "quick facts" grid.</summary>
        sealed class FactTile
        {
            public readonly RectTransform ValueRow;
            readonly TextMeshProUGUI mValue;
            readonly UITheme mTheme;

            public FactTile(Transform parent, UITheme t, string label)
            {
                mTheme = t;
                var bg = UI.Rounded(parent, "Fact " + label, t.surfaceHigh, 26);
                UI.VStack(bg.gameObject, 10, new RectOffset(24, 20, 24, 20));
                UI.Eyebrow(bg.transform, label, t.textMuted, 18f);
                ValueRow = UI.Rect("ValueRow", bg.transform);
                UI.HStack(ValueRow.gameObject, 10, null, TextAnchor.MiddleLeft);
                mValue = UI.Text(ValueRow, "Value", "", t.Display, 34f, t.textPrimary);
                mValue.textWrappingMode = TextWrappingModes.Normal;
                mValue.overflowMode = TextOverflowModes.Ellipsis;
                UI.Size(mValue.gameObject, -1, -1, 1f);
            }

            public void Set(string value, string missing)
            {
                if (string.IsNullOrWhiteSpace(value) || Placeholder.Is(value))
                {
                    mValue.text = missing;
                    mValue.font = mTheme.Body;
                    mValue.fontSize = 26f;
                    mValue.fontStyle = FontStyles.Italic;
                    mValue.color = mTheme.textMuted;
                }
                else
                {
                    mValue.text = value.Trim();
                    mValue.font = mTheme.Display;
                    mValue.fontSize = 34f;
                    mValue.fontStyle = FontStyles.Normal;
                    mValue.color = mTheme.textPrimary;
                }
            }
        }
    }
}
