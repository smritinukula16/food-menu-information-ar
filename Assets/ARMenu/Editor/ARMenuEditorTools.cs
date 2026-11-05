using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ARMenu.EditorTools
{
    /// <summary>
    /// Editor helpers for the AR menu:
    ///  - Install: adds the "AR Menu App" object to the open scene and wires it to the existing
    ///    FoodMenuManager / Vuforia ImageTarget (idempotent).
    ///  - Validate: lists placeholder ("TODO") fields left in menu_data.json.
    ///  - Smoke test: builds the UI in edit mode, presses every button through the real onClick
    ///    handlers, checks the resulting state and saves PNG previews of each screen.
    /// Batch usage:
    ///   Unity -batchmode -projectPath . -executeMethod ARMenu.EditorTools.ARMenuEditorTools.InstallBatch -quit
    ///   Unity -batchmode -projectPath . -executeMethod ARMenu.EditorTools.ARMenuEditorTools.SmokeTestBatch -armenuOut UIPreviews -quit
    /// </summary>
    public static class ARMenuEditorTools
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string MarkerImagePath = "Assets/ARMenu/Marker/menu_card_2616.png";
        const string MarkerTrackableName = "menu_card_2616";
        /// <summary>Printed width of the card in metres (A5 = 148 mm). Must match the real print for correct AR scale.</summary>
        const float MarkerPrintedWidth = 0.148f;

        // ------------------------------------------------------------------ install

        [MenuItem("Tools/AR Menu/Install Into Open Scene")]
        public static void InstallMenu()
        {
            var app = Install();
            if (app) Selection.activeObject = app.gameObject;
        }

        public static void InstallBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var app = Install();
            if (!app) { EditorApplication.Exit(1); return; }
            if (!ApplyMenuCardTarget()) { EditorApplication.Exit(1); return; }
            ApplyAppIcon();
            RemoveSampleScenesFromBuild();
            EditorSceneManager.SaveScene(app.gameObject.scene);
            Debug.Log("[ARMenu] Installed into " + ScenePath);
        }

        static ARMenuApp Install()
        {
            var manager = Object.FindFirstObjectByType<FoodMenuManager>(FindObjectsInactive.Include);
            var marker = Object.FindFirstObjectByType<DefaultObserverEventHandler>(FindObjectsInactive.Include);
            if (!manager) Debug.LogWarning("[ARMenu] No FoodMenuManager found in the open scene.");
            if (!marker) Debug.LogWarning("[ARMenu] No Vuforia DefaultObserverEventHandler (ImageTarget) found in the open scene.");

            var app = Object.FindFirstObjectByType<ARMenuApp>(FindObjectsInactive.Include);
            if (!app)
            {
                var go = new GameObject("AR Menu App");
                Undo.RegisterCreatedObjectUndo(go, "Install AR Menu");
                app = go.AddComponent<ARMenuApp>();
            }

            GameObject legacy = null;
            if (manager && manager.foodNameText)
            {
                var c = manager.foodNameText.GetComponentInParent<Canvas>(true);
                if (c) legacy = c.rootCanvas ? c.rootCanvas.gameObject : c.gameObject;
            }

            var so = new SerializedObject(app);
            so.FindProperty("foodMenuManager").objectReferenceValue = manager;
            so.FindProperty("markerEvents").objectReferenceValue = marker;
            so.FindProperty("legacyCanvas").objectReferenceValue = legacy;
            so.ApplyModifiedProperties();
            ResetTheme(app);
            EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
            Debug.Log($"[ARMenu] AR Menu App wired: FoodMenuManager={(manager ? manager.name : "none")}, " +
                      $"ImageTarget={(marker ? marker.name : "none")}, legacy canvas={(legacy ? legacy.name : "none")}");
            return app;
        }

        [MenuItem("Tools/AR Menu/Reset Theme To Default (Lavender)")]
        public static void ResetThemeMenu()
        {
            var app = Object.FindFirstObjectByType<ARMenuApp>(FindObjectsInactive.Include);
            if (!app) { Debug.LogWarning("[ARMenu] No ARMenuApp in the open scene."); return; }
            ResetTheme(app);
            EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
        }

        /// <summary>Replaces the colours stored on the component with the code defaults (keeps font overrides).</summary>
        static void ResetTheme(ARMenuApp app)
        {
            var field = typeof(ARMenuApp).GetField("theme", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null) return;
            Undo.RecordObject(app, "Reset AR Menu theme");
            var old = field.GetValue(app) as UITheme;
            var fresh = new UITheme();
            if (old != null)
            {
                fresh.displayFont = old.displayFont;
                fresh.bodyFont = old.bodyFont;
                fresh.strongFont = old.strongFont;
            }
            field.SetValue(app, fresh);
            EditorUtility.SetDirty(app);
        }

        // ------------------------------------------------------------------ AR marker (menu card)

        [MenuItem("Tools/AR Menu/Use 2616 Menu Card As Image Target")]
        public static void ApplyMenuCardTargetMenu()
        {
            if (ApplyMenuCardTarget()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Points the existing Vuforia ImageTarget ("From Image" type) at the 2616 menu card texture.
        /// Only the target image, its name and its printed size change – tracking, events, models and
        /// their anchoring under the ImageTarget stay exactly as they were.
        /// </summary>
        public static bool ApplyMenuCardTarget()
        {
            // Same import settings as the previous marker texture: readable for Vuforia, uncompressed, max 2048.
            var importer = AssetImporter.GetAtPath(MarkerImagePath) as TextureImporter;
            if (importer == null) { Debug.LogError("[ARMenu] Marker texture missing: " + MarkerImagePath); return false; }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.isReadable = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkerImagePath);

            var handler = Object.FindFirstObjectByType<DefaultObserverEventHandler>(FindObjectsInactive.Include);
            if (!handler) { Debug.LogError("[ARMenu] No ImageTarget with DefaultObserverEventHandler in the scene."); return false; }
            var target = handler.GetComponent<Vuforia.ImageTargetBehaviour>();
            if (!target) { Debug.LogError("[ARMenu] The marker object has no ImageTargetBehaviour."); return false; }

            var aspect = (float)texture.height / texture.width; // Vuforia stores height / width
            var so = new SerializedObject(target);
            void Set(string prop, System.Action<SerializedProperty> apply)
            {
                var p = so.FindProperty(prop);
                if (p == null) Debug.LogWarning("[ARMenu] ImageTargetBehaviour has no field " + prop);
                else apply(p);
            }
            Set("mRuntimeTexture", p => p.objectReferenceValue = texture);
            Set("mTrackableName", p => p.stringValue = MarkerTrackableName);
            Set("mAspectRatio", p => p.floatValue = aspect);
            Set("mWidth", p => p.floatValue = MarkerPrintedWidth);
            Set("mHeight", p => p.floatValue = MarkerPrintedWidth * aspect);
            so.ApplyModifiedProperties();

            var preview = handler.GetComponent("ImageTargetPreview");
            if (preview)
            {
                var pso = new SerializedObject(preview);
                var name = pso.FindProperty("mTargetName");
                if (name != null) { name.stringValue = MarkerTrackableName; pso.ApplyModifiedProperties(); }
            }
            EditorUtility.SetDirty(target);
            Debug.Log($"[ARMenu] Image target -> {MarkerImagePath} ({texture.width}x{texture.height}), " +
                      $"printed size {MarkerPrintedWidth * 1000f:0} x {MarkerPrintedWidth * aspect * 1000f:0} mm");
            return true;
        }

        // ------------------------------------------------------------------ app icon + build list

        const string IconPath = "Assets/ARMenu/Branding/app_icon.png";
        const string IconForegroundPath = "Assets/ARMenu/Branding/app_icon_foreground.png";
        const string IconBackgroundPath = "Assets/ARMenu/Branding/app_icon_background.png";

        [MenuItem("Tools/AR Menu/Apply 2616 Cafe App Icon")]
        public static void ApplyAppIcon()
        {
            Texture2D Prepare(string path)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { Debug.LogError("[ARMenu] Missing icon " + path); return null; }
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var icon = Prepare(IconPath);
            var fg = Prepare(IconForegroundPath);
            var bg = Prepare(IconBackgroundPath);
            if (!icon) return;

            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.iOS })
            {
                try
                {
                    foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
                    {
                        var icons = PlayerSettings.GetPlatformIcons(target, kind);
                        foreach (var pi in icons)
                        {
                            if (pi.maxLayerCount >= 2 && fg && bg) pi.SetTextures(bg, fg); // Android adaptive: background, foreground
                            else pi.SetTexture(icon);
                        }
                        PlayerSettings.SetPlatformIcons(target, kind, icons);
                    }
                }
                catch (Exception e) { Debug.LogWarning("[ARMenu] Could not set " + target.TargetName + " icons: " + e.Message); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ARMenu] App icon set to the 2616 Cafe logo.");
        }

        /// <summary>Removes Vuforia's sample scene (0-Main) from the build list; only SampleScene ships.</summary>
        public static void RemoveSampleScenesFromBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => !s.path.StartsWith("Assets/SamplesResources/")).ToArray();
            if (scenes.Length != EditorBuildSettings.scenes.Length)
            {
                EditorBuildSettings.scenes = scenes;
                Debug.Log("[ARMenu] Removed Vuforia sample scenes from Build Settings.");
            }
        }

        // ------------------------------------------------------------------ validate

        [MenuItem("Tools/AR Menu/Validate Menu Data")]
        public static void ValidateMenu()
        {
            var db = MenuLoader.Load(MenuLoader.DefaultResource);
            var issues = CollectPlaceholders(db);
            if (issues.Count == 0) Debug.Log("[ARMenu] menu_data.json: no placeholders left.");
            else Debug.LogWarning("[ARMenu] menu_data.json placeholders to fill in (" + issues.Count + "):\n- " + string.Join("\n- ", issues));
        }

        public static List<string> CollectPlaceholders(MenuDatabase db)
        {
            var list = new List<string>();
            if (Placeholder.Is(db.restaurant.name)) list.Add("restaurant.name");
            if (Placeholder.Is(db.restaurant.description)) list.Add("restaurant.description");
            if (string.IsNullOrEmpty(db.currencySymbol)) list.Add("currencySymbol (empty)");
            foreach (var i in db.items)
            {
                void Check(bool missing, string field) { if (missing) list.Add(i.id + "." + field); }
                Check(!i.HasPrice, "price");
                Check(Placeholder.Is(i.tagline), "tagline");
                Check(Placeholder.Is(i.description), "description");
                Check(Placeholder.IsEmptyList(i.ingredients), "ingredients");
                Check(Placeholder.Is(i.preparationStyle), "preparationStyle");
                Check(Placeholder.Is(i.tasteProfile), "tasteProfile");
                Check(i.spiceLevel < 0, "spiceLevel");
                Check(Placeholder.Is(i.portionSize), "portionSize");
                Check(i.DietType == Diet.Unspecified, "diet");
                Check(!i.allergensVerified, "allergens (not verified)");
            }
            return list;
        }

        // ------------------------------------------------------------------ smoke test

        [MenuItem("Tools/AR Menu/Run UI Smoke Test + Capture Previews")]
        public static void SmokeTestMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARMenu] Exit play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UIPreviews");
            var ok = RunSmokeTest(dir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorUtility.RevealInFinder(dir);
            Debug.Log(ok ? "[ARMenu] Smoke test PASSED" : "[ARMenu] Smoke test FAILED – see Console");
        }

        public static void SmokeTestBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, "-armenuOut");
            var dir = idx >= 0 && idx + 1 < args.Length
                ? args[idx + 1]
                : Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UIPreviews");
            var ok = RunSmokeTest(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // discard, never save
            EditorApplication.Exit(ok ? 0 : 2);
        }

        sealed class Report
        {
            public readonly List<string> Lines = new List<string>();
            public int Failures;

            public void Check(bool condition, string what)
            {
                Lines.Add((condition ? "PASS  " : "FAIL  ") + what);
                if (!condition) Failures++;
            }
        }

        static void Click(Report r, Button b, string what)
        {
            var ok = b != null && b.gameObject.activeInHierarchy && b.IsInteractable();
            r.Check(ok, "button usable: " + what);
            if (ok) b.onClick.Invoke();
        }

        static bool RunSmokeTest(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var r = new Report();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var app = Object.FindFirstObjectByType<ARMenuApp>(FindObjectsInactive.Include);
            r.Check(app != null, "ARMenuApp present in " + ScenePath);
            if (app == null) return Finish(r, outDir);

            var manager = Object.FindFirstObjectByType<FoodMenuManager>(FindObjectsInactive.Include);
            var markerEvents = Object.FindFirstObjectByType<DefaultObserverEventHandler>(FindObjectsInactive.Include);
            r.Check(manager != null, "FoodMenuManager present");
            r.Check(markerEvents != null, "Vuforia DefaultObserverEventHandler present on the ImageTarget");
            if (markerEvents)
            {
                var it = new SerializedObject(markerEvents.GetComponent<Vuforia.ImageTargetBehaviour>());
                var tex = it.FindProperty("mRuntimeTexture").objectReferenceValue as Texture2D;
                r.Check(tex != null && AssetDatabase.GetAssetPath(tex) == MarkerImagePath, "ImageTarget uses the 2616 menu card");
                r.Check(it.FindProperty("mTrackableName").stringValue == MarkerTrackableName, "trackable name updated");
                r.Check(Mathf.Approximately(it.FindProperty("mWidth").floatValue, MarkerPrintedWidth), "target width = A5 (0.148 m)");
                r.Check(tex != null && tex.isReadable, "marker texture is readable for Vuforia");
            }
            var foundListenersBefore = markerEvents ? markerEvents.OnTargetFound.GetPersistentEventCount() : 0;

            SafeAreaFitter.Bypass = true;
            app.Initialize(false);
            app.RenderModelImages();

            using var capture = new PreviewCapture(app, 1080, 2340);
            var items = app.Data.items;
            r.Check(items.Length == 3, "menu data has 3 dishes (pizza, burger, fries)");
            r.Check(items.All(i => app.Presenter.HasModel(i.modelKey)), "every dish maps to an existing 3D model");
            r.Check(items.All(i => !string.IsNullOrEmpty(i.calories)), "nutrition merged from FoodMenuManager scene data");
            r.Check(items.All(i => app.GetImage(i) != null), "studio images rendered from the 3D models");
            r.Check(app.CurrentBase == app.Menu && app.Menu.IsVisible, "app opens on the menu");
            r.Check(app.Menu.VisibleCardCount == 3, "menu shows 3 dish cards");
            capture.Save("01_menu", outDir, scrollMenuTo: 1f);
            capture.Save("01b_menu_dishes", outDir, scrollMenuTo: 0f);
            capture.Save("01c_menu_footer", outDir, scrollMenuTo: -2f);

            app.Splash.Show(false);
            capture.Save("00_splash", outDir);
            Click(r, app.Splash.SkipButton, "splash tap to skip");
            r.Check(!app.Splash.IsVisible, "splash dismissed");

            Click(r, app.Menu.CreditsButton, "footer credits");
            r.Check(app.TopOverlay == app.Credits && app.Credits.EntryCount >= 8, "credits panel lists assets, SDKs and fonts");
            capture.Save("11_credits", outDir);
            capture.Save("11b_credits_more", outDir, scrollCreditsTo: 0f);
            Click(r, app.Credits.CloseButton, "credits close");
            r.Check(app.OverlayCount == 0, "credits closed");

            // Categories
            Chip ChipFor(string id) => app.Menu.Chips.First(c => c.Id == id);
            r.Check(CollectPlaceholders(app.Data).Count == 0, "menu data has no placeholders left");
            foreach (var cat in app.Data.categories)
            {
                Click(r, ChipFor(cat).Button, "category " + cat);
                r.Check(app.Menu.VisibleCardCount == 1, cat + " shows 1 dish");
            }
            Click(r, ChipFor(MenuScreen.FavouritesCategory).Button, "category Favourites (none yet)");
            r.Check(app.Menu.VisibleCardCount == 0, "Favourites shows empty state");
            capture.Save("02_menu_empty_favourites", outDir, scrollMenuTo: 0f);
            Click(r, ChipFor(MenuScreen.AllCategory).Button, "category All");
            r.Check(app.Menu.VisibleCardCount == 3, "All shows 3 dishes");

            // Quick add + favourite from the card
            var pizzaCard = app.Menu.Cards.First(c => c.Item.id == "pizza");
            Click(r, pizzaCard.AddButton, "card quick-add (+)");
            r.Check(app.Orders.TotalUnits == 1, "cart has 1 item after quick add");
            r.Check(app.Toast.LastMessage != null && app.Toast.LastMessage.StartsWith("Added 1"), "toast confirms add");
            r.Check(app.Menu.HeaderCart.BadgeText == "1", "header cart badge shows 1");
            Click(r, pizzaCard.FavouriteButton, "card favourite");
            r.Check(app.Orders.IsFavourite("pizza"), "pizza is a favourite");
            Click(r, app.Menu.FavouritesButton, "header favourites");
            r.Check(app.Menu.Category == MenuScreen.FavouritesCategory && app.Menu.VisibleCardCount == 1, "favourites filter shows 1 dish");
            Click(r, ChipFor(MenuScreen.AllCategory).Button, "category All again");

            // Detail sheet
            Click(r, pizzaCard.OpenButton, "open pizza card");
            r.Check(app.TopOverlay == app.Detail && app.Detail.Item?.id == "pizza", "detail sheet shows pizza");
            capture.Save("03_detail", outDir);
            Click(r, app.Detail.Stepper.PlusButton, "quantity +");
            Click(r, app.Detail.Stepper.PlusButton, "quantity + (again)");
            r.Check(app.Detail.Stepper.Value == 3, "quantity is 3");
            Click(r, app.Detail.Stepper.MinusButton, "quantity -");
            r.Check(app.Detail.Stepper.Value == 2, "quantity is 2");
            Click(r, app.Detail.AddButton, "add to order");
            r.Check(app.Orders.QuantityOf("pizza") == 3, "pizza quantity in cart is 3");
            r.Check(app.Detail.Stepper.Value == 1, "stepper resets after adding");
            Click(r, app.Detail.MoreInfoButton, "more information");
            r.Check(app.Detail.MoreInfoExpanded, "more information expanded");
            capture.Save("04_detail_more_info", outDir, scrollDetailTo: 0f);
            Click(r, app.Detail.FavouriteButton, "detail favourite toggle");
            r.Check(!app.Orders.IsFavourite("pizza"), "favourite toggled off from detail");
            Click(r, app.Detail.BackButton, "detail back");
            r.Check(app.OverlayCount == 0 && app.CurrentBase == app.Menu, "back returns to menu");

            // Detail -> View 3D -> AR
            Click(r, app.Menu.Cards.First(c => c.Item.id == "burger").OpenButton, "open burger card");
            var burgerBase = manager.burgerModel.transform.localScale; // scene baseline, before the AR view hides it
            Click(r, app.Detail.View3DButton, "view 3D model");
            r.Check(app.IsInAR && app.OverlayCount == 0, "AR view open");
            r.Check(app.SelectedItem?.id == "burger" && app.Presenter.ActiveKey == "burger", "burger selected for AR");
            r.Check(manager.burgerModel.activeSelf && !manager.pizzaModel.activeSelf && !manager.friesModel.activeSelf,
                "FoodMenuManager shows only the burger model");
            r.Check(app.Scan.State == ScanState.Searching, "scanner is searching for the marker");
            capture.Save("05_scan_searching", outDir, cameraFeed: true);

            // Marker found -> viewing
            app.Marker.Simulate(true);
            r.Check(app.Scan.State == ScanState.Viewing, "marker found -> model shown");
            r.Check(app.Presenter.IsPresenting, "presenter is presenting");
            r.Check(manager.burgerModel.transform.localScale == burgerBase, "model scale restored after reveal");
            r.Check(manager.burgerModel.transform.parent == markerEvents.transform, "model still parented to the ImageTarget");
            capture.Save("06_scan_viewing", outDir, cameraFeed: true);

            var friesChip = app.Scan.DishChips.First(c => c.Id == "fries");
            Click(r, friesChip.Button, "AR dish switcher: fries");
            r.Check(app.Presenter.ActiveKey == "fries" && manager.friesModel.activeSelf && !manager.burgerModel.activeSelf,
                "switcher swaps the model via FoodMenuManager");
            Click(r, app.Scan.AutoRotateButton, "auto-rotate toggle");
            r.Check(!app.Presenter.AutoRotate, "auto-rotate off");
            Click(r, app.Scan.AutoRotateButton, "auto-rotate toggle again");
            r.Check(app.Presenter.AutoRotate, "auto-rotate on");
            var friesBase = manager.friesModel.transform.localPosition;
            manager.friesModel.transform.localPosition += Vector3.right * 0.05f; // simulate a Lean drag
            Click(r, app.Scan.ResetButton, "reset view");
            r.Check(Vector3.Distance(manager.friesModel.transform.localPosition, friesBase) < 0.0001f, "reset view undoes the drag");
            Click(r, app.Scan.AddButton, "AR add to order");
            r.Check(app.Orders.QuantityOf("fries") == 1, "fries added from AR");
            Click(r, app.Scan.DetailsButton, "AR details");
            r.Check(app.TopOverlay == app.Detail && app.Detail.OpenedFromAR && app.Detail.Item.id == "fries", "detail opened over AR");
            Click(r, app.Detail.View3DInlineButton, "back to the AR view");
            r.Check(app.IsInAR && app.OverlayCount == 0 && app.Scan.State == ScanState.Viewing, "returned to AR viewing");

            Click(r, app.Scan.RescanButton, "rescan");
            r.Check(app.Scan.State == ScanState.Viewing && app.Presenter.IsPresenting,
                "rescan re-detects the marker still in view");

            app.Marker.Simulate(false);
            r.Check(app.Scan.State == ScanState.Searching && app.Scan.StatusTitle == "Menu card lost", "marker lost feedback");
            capture.Save("07_scan_marker_lost", outDir, cameraFeed: true);

            // Cart
            Click(r, app.Scan.Cart.Button, "AR cart button");
            r.Check(app.TopOverlay == app.Cart && app.Cart.RowCount == 2, "cart shows 2 lines");
            capture.Save("08_cart", outDir, cameraFeed: true);
            var pizzaRow = app.Cart.Rows["pizza"];
            Click(r, pizzaRow.Plus, "cart row +");
            r.Check(app.Orders.QuantityOf("pizza") == 4, "cart row + updates quantity");
            Click(r, app.Cart.Rows["fries"].Minus, "cart row - (removes fries)");
            r.Check(app.Orders.QuantityOf("fries") == 0 && app.Cart.RowCount == 1, "row removed at quantity 0");
            Click(r, app.Cart.ConfirmButton, "confirm order");
            r.Check(app.Cart.ShowingConfirmation, "order confirmation shown");
            capture.Save("09_order_confirmed", outDir, cameraFeed: true);
            Click(r, app.Cart.DoneButton, "done");
            r.Check(app.Orders.TotalUnits == 0 && app.CurrentBase == app.Menu && app.OverlayCount == 0, "order cleared, back on menu");
            r.Check(!app.Presenter.IsPresenting, "presenter concealed after leaving AR");

            Click(r, app.Menu.HeaderCart.Button, "header cart (empty)");
            capture.Save("10_cart_empty", outDir);
            Click(r, app.Cart.BrowseButton, "browse menu");
            r.Check(app.OverlayCount == 0 && app.CurrentBase == app.Menu, "browse closes the cart");
            Click(r, app.Menu.ScanButton, "menu scan button");
            r.Check(app.IsInAR, "scan button opens AR");
            app.Back();
            r.Check(app.CurrentBase == app.Menu, "system back leaves AR");

            r.Check(markerEvents.OnTargetFound.GetPersistentEventCount() == foundListenersBefore,
                "no persistent listeners added to the Vuforia handler");
            return Finish(r, outDir);
        }

        static bool Finish(Report r, string outDir)
        {
            SafeAreaFitter.Bypass = false;
            var text = string.Join("\n", r.Lines) + $"\n\n{r.Lines.Count - r.Failures}/{r.Lines.Count} checks passed";
            File.WriteAllText(Path.Combine(outDir, "smoke_test_report.txt"), text);
            if (r.Failures == 0) Debug.Log("[ARMenu] SMOKE TEST PASSED\n" + text);
            else Debug.LogError("[ARMenu] SMOKE TEST FAILED (" + r.Failures + ")\n" + text);
            return r.Failures == 0;
        }

        /// <summary>Renders the overlay UI into PNGs by temporarily switching it to a camera canvas.</summary>
        sealed class PreviewCapture : IDisposable
        {
            readonly ARMenuApp mApp;
            readonly Camera mCamera;
            readonly RenderTexture mTarget;
            readonly GameObject mFeedCanvas;
            readonly int mWidth, mHeight;

            public PreviewCapture(ARMenuApp app, int width, int height)
            {
                mApp = app;
                mWidth = width;
                mHeight = height;
                mTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

                var camGo = new GameObject("ARMenu Preview Camera") { hideFlags = HideFlags.DontSave };
                mCamera = camGo.AddComponent<Camera>();
                mCamera.enabled = false;
                mCamera.clearFlags = CameraClearFlags.SolidColor;
                mCamera.backgroundColor = Color.black;
                mCamera.cullingMask = 1 << UI.UILayer;
                mCamera.orthographic = true;
                mCamera.targetTexture = mTarget;
                camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                camGo.transform.position = new Vector3(0, 0, -10000);

                var canvas = app.Canvas;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mCamera;
                canvas.planeDistance = 10f;
                app.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                app.Scaler.scaleFactor = width / 1080f;

                // Simulated camera feed: the printed menu marker photo behind the AR overlay.
                mFeedCanvas = new GameObject("ARMenu Preview Feed", typeof(RectTransform)) { layer = UI.UILayer, hideFlags = HideFlags.DontSave };
                var feedCanvas = mFeedCanvas.AddComponent<Canvas>();
                feedCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                feedCanvas.worldCamera = mCamera;
                feedCanvas.planeDistance = 20f;
                feedCanvas.sortingOrder = -100;
                var feedScaler = mFeedCanvas.AddComponent<CanvasScaler>();
                feedScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                feedScaler.scaleFactor = width / 1080f;
                var raw = UI.Rect("Feed", mFeedCanvas.transform).Stretch().gameObject.AddComponent<RawImage>();
                raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkerImagePath);
                raw.color = new Color(0.8f, 0.78f, 0.74f);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = raw.texture ? (float)raw.texture.width / raw.texture.height : 1f;
                mFeedCanvas.SetActive(false);
            }

            public void Save(string name, string dir, bool cameraFeed = false, float scrollMenuTo = -1f, float scrollDetailTo = -1f,
                float scrollCreditsTo = -1f)
            {
                mFeedCanvas.SetActive(cameraFeed);
                for (var i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)mApp.Canvas.transform);
                    if (scrollMenuTo >= 0f) SetScroll(mApp.Menu.Root, scrollMenuTo);
                    if (scrollMenuTo < -1f) ScrollToBottom(mApp.Menu.Root);
                    if (scrollCreditsTo >= 0f) SetScroll(mApp.Credits.Root, scrollCreditsTo);
                    if (scrollDetailTo >= 0f) SetScroll(mApp.Detail.Root, scrollDetailTo);
                }
                Canvas.ForceUpdateCanvases();

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = mTarget };
                if (RenderPipeline.SupportsRenderRequest(mCamera, request)) RenderPipeline.SubmitRenderRequest(mCamera, request);
                else mCamera.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = mTarget;
                var tex = new Texture2D(mWidth, mHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, mWidth, mHeight), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            static void ScrollToBottom(RectTransform screenRoot)
            {
                var scroll = screenRoot.GetComponentInChildren<ScrollRect>();
                if (!scroll) return;
                var viewport = ((RectTransform)scroll.viewport).rect.height;
                scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, scroll.content.rect.height - viewport));
            }

            static void SetScroll(RectTransform screenRoot, float normalized)
            {
                var scroll = screenRoot.GetComponentInChildren<ScrollRect>();
                if (scroll) scroll.verticalNormalizedPosition = normalized;
            }

            public void Dispose()
            {
                if (mApp && mApp.Canvas)
                {
                    mApp.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    mApp.Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                }
                if (mCamera) Object.DestroyImmediate(mCamera.gameObject);
                if (mFeedCanvas) Object.DestroyImmediate(mFeedCanvas);
                if (mTarget) { mTarget.Release(); Object.DestroyImmediate(mTarget); }
            }
        }
    }
}
