using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ARMenu
{
    /// <summary>
    /// Entry point of the AR restaurant menu UI.
    ///
    /// Builds a screen-space UI (menu, food details, AR scanning, order) at runtime and connects it to the
    /// EXISTING project pieces without replacing them:
    ///  - FoodMenuManager: model switching (ShowPizza/ShowBurger/ShowFries) and nutrition data (FoodInfo),
    ///  - Vuforia DefaultObserverEventHandler on the ImageTarget: marker found / lost events,
    ///  - the food models under the ImageTarget (still anchored by Vuforia, still driven by Lean Touch).
    /// </summary>
    [DisallowMultipleComponent]
    public class ARMenuApp : MonoBehaviour
    {
        [Header("Existing scene references (auto-detected when empty)")]
        [SerializeField] FoodMenuManager foodMenuManager;
        [SerializeField] DefaultObserverEventHandler markerEvents;
        [Tooltip("The original marker-only info canvas ('Card'). Hidden at runtime because this UI replaces it.")]
        [SerializeField] GameObject legacyCanvas;
        [SerializeField] bool hideLegacyCanvas = true;

        [Header("Menu data")]
        [Tooltip("Resources path of the menu JSON (without extension).")]
        [SerializeField] string menuDataResource = MenuLoader.DefaultResource;

        [Header("Look & feel")]
        [SerializeField] UITheme theme = new UITheme();
        [SerializeField] Vector2 referenceResolution = new Vector2(1080, 1920);
        [SerializeField] int canvasSortingOrder = 10;

        [Header("3D presentation")]
        [SerializeField] bool presentationStand = true;
        [SerializeField] bool presentationFillLight = true;
        [SerializeField] bool autoRotate = true;
        [SerializeField] float rotationSpeed = 18f;
        [Tooltip("Size of the studio renders of the 3D models used as dish images.")]
        [SerializeField] int thumbnailSize = 512;

        [Tooltip("Show the branded splash (cafe logo + creator credit) when the app starts.")]
        [SerializeField] bool showSplash = true;

        [Header("Testing")]
        [Tooltip("Keep cart and favourites between sessions (PlayerPrefs).")]
        [SerializeField] bool persistOrder = true;
        [Tooltip("Editor only: F = simulate marker found, G = simulate marker lost (UI state only).")]
        [SerializeField] bool editorMarkerShortcuts = true;

        public UITheme Theme => theme;
        public MenuDatabase Data { get; private set; }
        public OrderService Orders { get; private set; }
        public ModelPresenter Presenter { get; private set; }
        public MarkerTrackingBridge Marker { get; private set; }
        public MenuItemData SelectedItem { get; private set; }
        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }

        public MenuScreen Menu { get; private set; }
        public ScanScreen Scan { get; private set; }
        public FoodDetailSheet Detail { get; private set; }
        public CartSheet Cart { get; private set; }
        public Toast Toast { get; private set; }
        public CreditsSheet Credits { get; private set; }
        public SplashScreen Splash { get; private set; }

        public event Action ImagesChanged;

        public bool IsInAR => Scan != null && mBase == Scan;
        public UIScreen CurrentBase => mBase;
        public UIScreen TopOverlay => mOverlays.Count > 0 ? mOverlays[mOverlays.Count - 1] : null;
        public int OverlayCount => mOverlays.Count;

        readonly List<UIScreen> mOverlays = new List<UIScreen>();
        readonly Dictionary<string, Texture> mImages = new Dictionary<string, Texture>();
        readonly List<Texture> mOwnedTextures = new List<Texture>();
        UIScreen mBase;
        bool mInitialized;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            if (Application.isPlaying) Initialize(persistOrder);
        }

        IEnumerator Start()
        {
            // Wait one rendered frame so the render pipeline is fully up before the studio renders.
            yield return null;
            RenderModelImages();
            // FoodMenuManager.Start() selects pizza by default; re-apply the dish selected in this UI.
            if (SelectedItem != null && SelectedItem.HasModel) Presenter?.Select(SelectedItem.modelKey);
        }

        /// <summary>Builds everything. Called from Awake in play mode; editor tools call it directly.</summary>
        public void Initialize(bool persist)
        {
            if (mInitialized) return;
            mInitialized = true;

            AutoResolveReferences();
            Tween.SetHost(this);
            UISprites.Init();
            theme.ResolveFonts();
            UI.Theme = theme;

            Data = MenuLoader.Load(menuDataResource);
            MenuLoader.MergeSceneNutrition(Data, foodMenuManager);
            Orders = new OrderService(persist, id => Data.Find(id) != null);

            Marker = new MarkerTrackingBridge(markerEvents);
            Presenter = new ModelPresenter(foodMenuManager, Marker.TargetRoot, theme, presentationStand && Marker.HasMarker,
                presentationFillLight && Application.isPlaying)
            {
                AutoRotate = autoRotate,
                RotationSpeed = rotationSpeed
            };
            Marker.Found += OnMarkerFound;
            Marker.Lost += OnMarkerLost;

            LoadPhotos();
            SelectedItem = FirstWithModel() ?? (Data.items.Length > 0 ? Data.items[0] : null);

            if (legacyCanvas && hideLegacyCanvas && Application.isPlaying) legacyCanvas.SetActive(false);

            EnsureEventSystem();
            BuildCanvas();
            SetBase(Menu, false);
            if (showSplash && Application.isPlaying) Splash.Show();
        }

        void AutoResolveReferences()
        {
            if (!foodMenuManager) foodMenuManager = FindFirstObjectByType<FoodMenuManager>(FindObjectsInactive.Include);
            if (!markerEvents) markerEvents = FindFirstObjectByType<DefaultObserverEventHandler>(FindObjectsInactive.Include);
            if (!legacyCanvas && foodMenuManager && foodMenuManager.foodNameText)
            {
                var c = foodMenuManager.foodNameText.GetComponentInParent<Canvas>(true);
                if (c) legacyCanvas = c.rootCanvas ? c.rootCanvas.gameObject : c.gameObject;
            }
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildCanvas()
        {
            var go = new GameObject("AR Menu UI", typeof(RectTransform)) { layer = UI.UILayer };
            go.transform.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = canvasSortingOrder;
            Canvas.pixelPerfect = false;
            Canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                                               AdditionalCanvasShaderChannels.Normal |
                                               AdditionalCanvasShaderChannels.Tangent;
            Scaler = go.AddComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.referenceResolution = referenceResolution;
            Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Scaler.matchWidthOrHeight = 0f; // portrait: keep the 1080 logical width on every phone
            Scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)go.transform;
            Menu = new MenuScreen(this, root);
            Scan = new ScanScreen(this, root);
            Detail = new FoodDetailSheet(this, root);
            Cart = new CartSheet(this, root);
            Credits = new CreditsSheet(this, root);
            Toast = new Toast(root, theme);
            Splash = new SplashScreen(this, root);
        }

        void Update()
        {
            if (!mInitialized) return;
            if (IsInAR)
            {
                Presenter?.Tick(Time.deltaTime);
                Scan.Tick();
            }
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                // Android's system back button arrives as Escape.
                if (kb.escapeKey.wasPressedThisFrame) Back();
#if UNITY_EDITOR
                if (editorMarkerShortcuts && Marker != null)
                {
                    if (kb.fKey.wasPressedThisFrame) Marker.Simulate(true);
                    if (kb.gKey.wasPressedThisFrame) Marker.Simulate(false);
                }
#endif
            }
#endif
        }

        void OnDestroy()
        {
            if (Marker != null)
            {
                Marker.Found -= OnMarkerFound;
                Marker.Lost -= OnMarkerLost;
                Marker.Dispose();
            }
            Presenter?.Dispose();
            foreach (var t in mOwnedTextures)
                if (t)
                {
                    if (Application.isPlaying) Destroy(t);
                    else DestroyImmediate(t);
                }
            mOwnedTextures.Clear();
        }

        // ------------------------------------------------------------------ navigation

        void SetBase(UIScreen screen, bool animate = true)
        {
            if (mBase == screen)
            {
                screen.Show(animate);
                screen.Root.SetSiblingIndex(0);
                return;
            }
            var previous = mBase;
            mBase = screen;
            previous?.Hide(animate);
            screen.Show(animate);
            screen.Root.SetSiblingIndex(0);
        }

        void PushOverlay(UIScreen screen)
        {
            mOverlays.Remove(screen);
            mOverlays.Add(screen);
            screen.Show();
        }

        void CloseAllOverlays()
        {
            for (var i = mOverlays.Count - 1; i >= 0; i--) mOverlays[i].Hide();
            mOverlays.Clear();
        }

        public void CloseTop()
        {
            if (mOverlays.Count == 0) return;
            var top = mOverlays[mOverlays.Count - 1];
            mOverlays.RemoveAt(mOverlays.Count - 1);
            top.Hide();
        }

        /// <summary>Back button / Android back: close the top sheet, else leave AR, else stay on the menu.</summary>
        public void Back()
        {
            if (Splash != null && Splash.IsVisible) Splash.Dismiss();
            else if (mOverlays.Count > 0) CloseTop();
            else if (mBase == Scan) GoHome();
            else if (Menu != null && Menu.Category != MenuScreen.AllCategory) Menu.SelectCategory(MenuScreen.AllCategory, true);
        }

        public void GoHome()
        {
            CloseAllOverlays();
            SetBase(Menu);
        }

        public void OpenDetail(MenuItemData item)
        {
            if (item == null) return;
            SelectItem(item);
            Detail.Bind(item, IsInAR);
            PushOverlay(Detail);
        }

        public void OpenCredits()
        {
            if (TopOverlay == Credits) return;
            PushOverlay(Credits);
        }

        public void OpenCart()
        {
            if (Cart.IsVisible && TopOverlay == Cart) return;
            PushOverlay(Cart);
        }

        /// <summary>Opens the AR view. With a dish: that dish is shown on the marker.</summary>
        public void OpenScan(MenuItemData item)
        {
            if (item != null && item.HasModel && Presenter.HasModel(item.modelKey)) SelectItem(item);
            else if (SelectedItem == null || !Presenter.HasModel(SelectedItem.modelKey))
            {
                var first = FirstWithModel();
                if (first != null) SelectItem(first);
            }
            CloseAllOverlays();
            if (SelectedItem != null) Presenter.Select(SelectedItem.modelKey);
            if (mBase == Scan) Scan.SwitchTo(SelectedItem);
            else SetBase(Scan);
        }

        public void SelectItem(MenuItemData item)
        {
            if (item != null) SelectedItem = item;
        }

        // ------------------------------------------------------------------ actions

        public void AddToCart(MenuItemData item, int quantity)
        {
            if (item == null || quantity <= 0) return;
            var before = Orders.QuantityOf(item.id);
            Orders.Add(item.id, quantity);
            var added = Orders.QuantityOf(item.id) - before;
            if (added <= 0) ShowToast("Maximum quantity reached for " + item.name, UISprites.Info);
            else ShowToast("Added " + added + " x " + item.name + " to your order", UISprites.Check);
        }

        public bool ToggleFavourite(MenuItemData item)
        {
            if (item == null) return false;
            var fav = Orders.ToggleFavourite(item.id);
            ShowToast(fav ? item.name + " saved to favourites" : item.name + " removed from favourites",
                fav ? UISprites.Heart : UISprites.HeartOutline);
            return fav;
        }

        public void ShowToast(string message, Sprite icon) => Toast?.Show(message, icon);

        // ------------------------------------------------------------------ formatting

        public string FormatAmount(float value)
        {
            var number = Mathf.Approximately(value % 1f, 0f)
                ? value.ToString("N0", CultureInfo.InvariantCulture)
                : value.ToString("N2", CultureInfo.InvariantCulture);
            var symbol = Data?.currencySymbol ?? "";
            return string.IsNullOrEmpty(symbol) ? number : symbol + number;
        }

        /// <summary>Writes the price, or a muted "Price TBA" placeholder if none is set in the menu data.</summary>
        public void ApplyPrice(TextMeshProUGUI label, MenuItemData item, float size = 34f)
        {
            if (item != null && item.HasPrice)
            {
                label.text = FormatAmount(item.price);
                label.font = theme.Strong;
                label.fontSize = size;
                label.fontStyle = FontStyles.Normal;
                label.color = theme.accent;
            }
            else
            {
                label.text = "Price TBA";
                label.font = theme.Body;
                label.fontSize = size * 0.72f;
                label.fontStyle = FontStyles.Italic;
                label.color = theme.textMuted;
            }
        }

        // ------------------------------------------------------------------ images

        public Texture GetImage(MenuItemData item) =>
            item != null && mImages.TryGetValue(item.id, out var tex) ? tex : null;

        void LoadPhotos()
        {
            foreach (var item in Data.items)
            {
                if (string.IsNullOrEmpty(item.imageResource)) continue;
                var sprite = Resources.Load<Sprite>(item.imageResource);
                Texture tex = sprite ? sprite.texture : Resources.Load<Texture2D>(item.imageResource);
                if (tex) mImages[item.id] = tex;
                else Debug.LogWarning($"[ARMenu] Image '{item.imageResource}' for '{item.name}' not found in Resources.");
            }
        }

        /// <summary>Renders studio shots of the existing 3D models for dishes without a photo.</summary>
        public void RenderModelImages()
        {
            if (Presenter == null) return;
            var changed = false;
            foreach (var item in Data.items)
            {
                if (mImages.ContainsKey(item.id) || !item.HasModel) continue;
                var model = Presenter.GetModel(item.modelKey);
                if (!model) continue;
                var tex = ModelThumbnailRenderer.Render(model, thumbnailSize, theme.surfaceHigh);
                if (!tex) continue;
                mImages[item.id] = tex;
                mOwnedTextures.Add(tex);
                changed = true;
            }
            if (changed) ImagesChanged?.Invoke();
        }

        MenuItemData FirstWithModel()
        {
            foreach (var item in Data.items)
                if (item.HasModel && Presenter != null && Presenter.HasModel(item.modelKey)) return item;
            return null;
        }

        // ------------------------------------------------------------------ marker events

        void OnMarkerFound()
        {
            Presenter?.SetTracked(true);
            if (IsInAR) Scan.OnMarkerFound();
        }

        void OnMarkerLost()
        {
            Presenter?.SetTracked(false);
            if (IsInAR) Scan.OnMarkerLost();
        }
    }
}
