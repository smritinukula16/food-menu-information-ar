using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARMenu
{
    /// <summary>
    /// Presentation layer on top of the existing AR setup:
    /// - model switching still goes through FoodMenuManager.ShowPizza/ShowBurger/ShowFries,
    /// - models stay children of the ImageTarget (anchoring untouched),
    /// - adds a reveal animation, a presentation stand, a warm fill light, optional idle rotation
    ///   and a "reset view" that undoes Lean Touch drags/pinches/twists.
    /// Model geometry and materials are not modified.
    /// </summary>
    public sealed class ModelPresenter : IDisposable
    {
        struct LocalPose
        {
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }

        readonly FoodMenuManager mMenu;
        readonly Transform mTarget;
        readonly Dictionary<string, GameObject> mModels = new Dictionary<string, GameObject>();
        readonly Dictionary<GameObject, LocalPose> mBaseline = new Dictionary<GameObject, LocalPose>();

        GameObject mStand;
        Renderer[] mStandRenderers;
        Vector3 mStandBasePosition;
        Light mFillLight;
        Material[] mMaterials = Array.Empty<Material>();
        bool mAnimating;
        float mLastTouchTime = -100f;

        public string ActiveKey { get; private set; }
        public bool IsPresenting { get; private set; }
        public bool AutoRotate = true;
        public float RotationSpeed = 18f;
        public bool ShowStand = true;
        public float IdleDelayAfterTouch = 2.5f;

        public GameObject ActiveModel => ActiveKey != null && mModels.TryGetValue(ActiveKey, out var m) ? m : null;

        public ModelPresenter(FoodMenuManager menu, Transform targetRoot, UITheme theme, bool createStand, bool createFillLight)
        {
            mMenu = menu;
            mTarget = targetRoot;
            if (mMenu != null)
            {
                foreach (var key in FoodMenuManager.ModelKeys)
                {
                    var model = mMenu.GetModel(key);
                    if (model == null) continue;
                    mModels[key] = model;
                    var t = model.transform;
                    mBaseline[model] = new LocalPose { Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale };
                }
            }
            if (createStand && mTarget != null) CreateStand(theme);
            if (createFillLight) CreateFillLight();
        }

        public bool HasModel(string key) => key != null && mModels.ContainsKey(key.ToLowerInvariant());
        public GameObject GetModel(string key) => key != null && mModels.TryGetValue(key.ToLowerInvariant(), out var m) ? m : null;

        /// <summary>Selects which dish should appear on the marker (uses the original FoodMenuManager methods).</summary>
        public void Select(string key)
        {
            ActiveKey = string.IsNullOrEmpty(key) ? null : key.ToLowerInvariant();
            if (mMenu != null && ActiveKey != null) mMenu.ShowByKey(ActiveKey);
        }

        /// <summary>Hides the model while the "loading" state is shown after detection.</summary>
        public void PrepareReveal()
        {
            IsPresenting = false;
            KillTweens();
            var model = ActiveModel;
            if (model) model.transform.localScale = Vector3.zero;
            if (mStand) mStand.transform.localScale = Vector3.zero;
            SetFillLight(false);
        }

        /// <summary>Shows the selected model on the marker with a short pop-in.</summary>
        public void Reveal(bool tracked)
        {
            Select(ActiveKey);
            var model = ActiveModel;
            if (model == null) return;

            RestoreBaseline(model);
            var targetScale = model.transform.localScale;
            FitStand(model);

            IsPresenting = true;
            mAnimating = true;
            model.transform.localScale = Vector3.zero;
            Tween.To((this, "model"), 0.6f, t => { if (model) model.transform.localScale = targetScale * t; },
                Ease.OutBack, () => mAnimating = false);

            if (mStand)
            {
                mStand.transform.localScale = Vector3.zero;
                Tween.To((this, "stand"), 0.45f, t => { if (mStand) mStand.transform.localScale = Vector3.one * t; }, Ease.OutCubic);
            }
            SetTracked(tracked);
        }

        /// <summary>Stops presenting (leaving the AR view). Restores the model's original transform.</summary>
        public void Conceal()
        {
            IsPresenting = false;
            KillTweens();
            mAnimating = false;
            var model = ActiveModel;
            if (model) RestoreBaseline(model);
            if (mStand) mStand.transform.localScale = Vector3.zero;
            SetFillLight(false);
        }

        /// <summary>Undo user drags / pinches / twists on the active model.</summary>
        public void ResetView()
        {
            var model = ActiveModel;
            if (model == null) return;
            KillTweens();
            RestoreBaseline(model);
            var target = model.transform.localScale;
            mAnimating = true;
            Tween.To((this, "model"), 0.4f, t => { if (model) model.transform.localScale = target * Mathf.Lerp(0.85f, 1f, t); },
                Ease.OutBack, () => mAnimating = false);
            if (mStand) mStand.transform.localScale = Vector3.one;
            FitStand(model);
        }

        /// <summary>Called whenever tracking changes so the stand/light follow the marker visibility.</summary>
        public void SetTracked(bool tracked)
        {
            var visible = tracked && IsPresenting;
            if (mStandRenderers != null)
                foreach (var r in mStandRenderers)
                    if (r) r.enabled = visible && ShowStand;
            SetFillLight(visible);
        }

        public void Tick(float deltaTime)
        {
            if (!IsPresenting) return;
            var model = ActiveModel;
            if (model == null) return;

            if (AnyPointerPressed()) mLastTouchTime = Time.unscaledTime;

            // Let the stand follow Lean Touch drags and pinches.
            if (mStand && !mAnimating && mBaseline.TryGetValue(model, out var basePose))
            {
                var offset = model.transform.localPosition - basePose.Position;
                mStand.transform.localPosition = mStandBasePosition + new Vector3(offset.x, 0f, offset.z);
                var ratio = basePose.Scale.x != 0f ? model.transform.localScale.x / basePose.Scale.x : 1f;
                mStand.transform.localScale = Vector3.one * Mathf.Clamp(ratio, 0.05f, 20f);
            }

            if (!AutoRotate || mAnimating || mTarget == null) return;
            if (Time.unscaledTime - mLastTouchTime < IdleDelayAfterTouch) return;
            model.transform.Rotate(mTarget.up, RotationSpeed * deltaTime, Space.World);
        }

        // ------------------------------------------------------------------ internals

        void RestoreBaseline(GameObject model)
        {
            if (!mBaseline.TryGetValue(model, out var pose)) return;
            var t = model.transform;
            t.localPosition = pose.Position;
            t.localRotation = pose.Rotation;
            t.localScale = pose.Scale;
        }

        void KillTweens()
        {
            Tween.Kill((this, "model"));
            Tween.Kill((this, "stand"));
        }

        static bool AnyPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null)
                foreach (var t in touch.touches)
                    if (t.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;
#endif
            return false;
        }

        void CreateStand(UITheme theme)
        {
            mStand = new GameObject("AR Presentation Stand");
            mStand.transform.SetParent(mTarget, false);
            mStand.transform.localScale = Vector3.zero;

            var top = CreateDisc("Top", ThemeColor(theme?.surfaceHigh, 0x2A2622), 0.25f, 0.8f);
            var rim = CreateDisc("Rim", ThemeColor(theme?.accent, 0xCFA75E), 0.9f, 0.7f);
            mStandRenderers = new[] { top.GetComponent<Renderer>(), rim.GetComponent<Renderer>() };
            foreach (var r in mStandRenderers) r.enabled = false;
        }

        static Color ThemeColor(Color? c, uint fallback) => c ?? UITheme.Hex(fallback);

        GameObject CreateDisc(string name, Color color, float metallic, float smoothness)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            var col = disc.GetComponent<Collider>();
            if (col) DestroySafe(col);
            disc.transform.SetParent(mStand.transform, false);
            var r = disc.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            var mat = CreateLitMaterial(color, metallic, smoothness);
            if (mat) r.sharedMaterial = mat;
            return disc;
        }

        Material CreateLitMaterial(Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = null;
            if (shader) mat = new Material(shader);
            else
            {
                // Fall back to a copy of an existing food material (always included in builds).
                foreach (var m in mModels.Values)
                {
                    var r = m.GetComponentInChildren<Renderer>(true);
                    if (r && r.sharedMaterial) { mat = new Material(r.sharedMaterial); break; }
                }
            }
            if (mat == null) return null;
            mat.name = "ARMenu Stand";
            mat.hideFlags = HideFlags.DontSave;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            var list = new List<Material>(mMaterials) { mat };
            mMaterials = list.ToArray();
            return mat;
        }

        void FitStand(GameObject model)
        {
            if (mStand == null) return;
            if (!TryGetLocalBounds(model, out var b))
                b = new Bounds(Vector3.zero, new Vector3(0.1f, 0.05f, 0.1f));

            var radius = Mathf.Max(0.03f, Mathf.Max(b.extents.x, b.extents.z) * 1.15f);
            var thickness = Mathf.Clamp(radius * 0.08f, 0.002f, 0.02f);
            mStandBasePosition = new Vector3(b.center.x, b.min.y, b.center.z);
            mStand.transform.localPosition = mStandBasePosition;
            mStand.transform.localRotation = Quaternion.identity;

            var top = mStand.transform.Find("Top");
            var rim = mStand.transform.Find("Rim");
            // Unity's cylinder is 2 units tall and 1 unit wide.
            if (top)
            {
                top.localPosition = new Vector3(0f, -thickness * 0.5f, 0f);
                top.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            }
            if (rim)
            {
                rim.localPosition = new Vector3(0f, -thickness * 0.75f, 0f);
                rim.localScale = new Vector3(radius * 2.08f, thickness * 0.45f, radius * 2.08f);
            }
        }

        bool TryGetLocalBounds(GameObject model, out Bounds bounds)
        {
            var result = default(Bounds);
            var has = false;
            var toLocal = mTarget.worldToLocalMatrix;

            void Encapsulate(Mesh mesh, Transform t)
            {
                if (mesh == null) return;
                var mb = mesh.bounds;
                var m = toLocal * t.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }

            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) Encapsulate(mf.sharedMesh, mf.transform);
            foreach (var sm in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Encapsulate(sm.sharedMesh, sm.transform);
            bounds = result;
            return has;
        }

        void CreateFillLight()
        {
            var go = new GameObject("AR Presentation Fill Light");
            go.transform.rotation = Quaternion.Euler(28f, 155f, 0f);
            mFillLight = go.AddComponent<Light>();
            mFillLight.type = LightType.Directional;
            mFillLight.color = new Color(1f, 0.9f, 0.76f);
            mFillLight.intensity = 0.75f;
            mFillLight.shadows = LightShadows.None;
            mFillLight.enabled = false;
        }

        void SetFillLight(bool on)
        {
            if (mFillLight) mFillLight.enabled = on;
        }

        static void DestroySafe(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public void Dispose()
        {
            KillTweens();
            if (mStand) DestroySafe(mStand);
            if (mFillLight) DestroySafe(mFillLight.gameObject);
            foreach (var m in mMaterials) DestroySafe(m);
            mMaterials = Array.Empty<Material>();
        }
    }
}
