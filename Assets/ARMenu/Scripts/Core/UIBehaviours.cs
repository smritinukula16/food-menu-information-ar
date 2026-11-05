using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARMenu
{
    /// <summary>Subtle scale-down on press, springy release. Does not block ScrollRect drags.</summary>
    public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.95f;
        bool mPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            var sel = GetComponent<Selectable>();
            if (sel && !sel.IsInteractable()) return;
            mPressed = true;
            Tween.Scale(transform, new Vector3(pressedScale, pressedScale, 1f), 0.08f, Ease.OutCubic);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        void Release()
        {
            if (!mPressed) return;
            mPressed = false;
            Tween.Scale(transform, Vector3.one, 0.28f, Ease.OutBack);
        }

        void OnDisable()
        {
            mPressed = false;
            Tween.Kill((transform, "scale"));
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>Fits a RectTransform to Screen.safeArea (notches, home indicator). Edges can be toggled.</summary>
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Set by editor preview tools to ignore the editor's safe area.</summary>
        public static bool Bypass;

        public bool top = true, bottom = true, left = true, right = true;
        Rect mLastArea;
        Vector2Int mLastScreen;

        void OnEnable() => Apply(true);
        void Update() => Apply(false);

        void Apply(bool force)
        {
            var rt = transform as RectTransform;
            if (rt == null) return;
            var area = Bypass || !Application.isPlaying ? new Rect(0, 0, Screen.width, Screen.height) : Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == mLastArea && screen == mLastScreen) return;
            mLastArea = area;
            mLastScreen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;

            var min = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            var max = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            rt.anchorMin = new Vector2(left ? min.x : 0f, bottom ? min.y : 0f);
            rt.anchorMax = new Vector2(right ? max.x : 1f, top ? max.y : 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Horizontal ScrollRect that hands mostly-vertical drags to its parent ScrollRect,
    /// so the page still scrolls when a drag starts on the category chips.
    /// </summary>
    public class NestedScrollRect : ScrollRect
    {
        ScrollRect mParent;
        bool mRouteToParent;

        protected override void Start()
        {
            base.Start();
            if (transform.parent) mParent = transform.parent.GetComponentInParent<ScrollRect>();
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (mParent) mParent.OnInitializePotentialDrag(eventData);
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            mRouteToParent = mParent && Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x);
            if (mRouteToParent) mParent.OnBeginDrag(eventData);
            else base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (mRouteToParent) mParent.OnDrag(eventData);
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (mRouteToParent) mParent.OnEndDrag(eventData);
            else base.OnEndDrag(eventData);
            mRouteToParent = false;
        }

        public override void OnScroll(PointerEventData data)
        {
            if (mParent && Mathf.Abs(data.scrollDelta.y) > Mathf.Abs(data.scrollDelta.x)) mParent.OnScroll(data);
            else base.OnScroll(data);
        }
    }

    /// <summary>Continuously rotating loading indicator.</summary>
    public class SpinnerRotation : MonoBehaviour
    {
        public float degreesPerSecond = -300f;
        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }

    /// <summary>Gentle alpha/scale pulse used for the scanning frame and hints.</summary>
    public class Pulse : MonoBehaviour
    {
        public float speed = 2.2f;
        public float scaleAmount = 0.025f;
        public float minAlpha = 0.55f;
        Graphic[] mGraphics;
        float[] mBaseAlpha;

        void OnEnable()
        {
            mGraphics = GetComponentsInChildren<Graphic>(true);
            mBaseAlpha = new float[mGraphics.Length];
            for (var i = 0; i < mGraphics.Length; i++) mBaseAlpha[i] = mGraphics[i].color.a;
        }

        void OnDisable()
        {
            transform.localScale = Vector3.one;
            if (mGraphics == null) return;
            for (var i = 0; i < mGraphics.Length; i++)
                if (mGraphics[i]) { var c = mGraphics[i].color; c.a = mBaseAlpha[i]; mGraphics[i].color = c; }
        }

        void Update()
        {
            var k = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
            var s = 1f + scaleAmount * k;
            transform.localScale = new Vector3(s, s, 1f);
            for (var i = 0; i < mGraphics.Length; i++)
            {
                if (!mGraphics[i]) continue;
                var c = mGraphics[i].color;
                c.a = mBaseAlpha[i] * Mathf.Lerp(minAlpha, 1f, k);
                mGraphics[i].color = c;
            }
        }
    }
}
