using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ARMenu
{
    public enum Ease { Linear, InCubic, OutCubic, InOutCubic, OutBack, OutQuint }

    /// <summary>
    /// Tiny coroutine tween helper (no third-party dependency). A key identifies a tween so
    /// starting a new tween with the same key cancels the previous one. Uses unscaled time.
    /// Outside play mode (editor tools / previews) every tween completes instantly.
    /// </summary>
    public static class Tween
    {
        sealed class Handle { public Coroutine Routine; }

        static MonoBehaviour sHost;
        static readonly Dictionary<object, Handle> sRunning = new Dictionary<object, Handle>();

        public static void SetHost(MonoBehaviour host)
        {
            sHost = host;
            sRunning.Clear();
        }

        public static bool Instant => sHost == null || !Application.isPlaying || !sHost.isActiveAndEnabled;

        public static void To(object key, float duration, Action<float> step, Ease ease = Ease.OutCubic,
            Action onComplete = null, float delay = 0f)
        {
            Kill(key);
            if (Instant || (duration <= 0f && delay <= 0f))
            {
                step?.Invoke(1f);
                onComplete?.Invoke();
                return;
            }

            var handle = new Handle();
            if (key != null) sRunning[key] = handle;
            handle.Routine = sHost.StartCoroutine(Run(key, handle, duration, delay, step, ease, onComplete));
        }

        public static void Delay(object key, float seconds, Action action) =>
            To(key, 0f, null, Ease.Linear, action, seconds);

        public static void Kill(object key)
        {
            if (key == null) return;
            if (sRunning.TryGetValue(key, out var handle))
            {
                if (handle.Routine != null && sHost != null) sHost.StopCoroutine(handle.Routine);
                sRunning.Remove(key);
            }
        }

        static IEnumerator Run(object key, Handle handle, float duration, float delay, Action<float> step, Ease ease,
            Action onComplete)
        {
            if (delay > 0f)
            {
                var waited = 0f;
                while (waited < delay)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (duration > 0f)
            {
                var t = 0f;
                step?.Invoke(Evaluate(ease, 0f));
                while (t < duration)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    step?.Invoke(Evaluate(ease, Mathf.Clamp01(t / duration)));
                }
            }
            else
            {
                step?.Invoke(1f);
            }

            if (key != null && sRunning.TryGetValue(key, out var current) && current == handle)
                sRunning.Remove(key);
            onComplete?.Invoke();
        }

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                default: return t;
            }
        }

        // ----- Convenience wrappers -----

        public static void Fade(CanvasGroup group, float to, float duration, Action onComplete = null,
            Ease ease = Ease.OutCubic, float delay = 0f)
        {
            if (group == null) return;
            var from = group.alpha;
            To((group, "fade"), duration, t => group.alpha = Mathf.LerpUnclamped(from, to, t), ease, onComplete, delay);
        }

        public static void Scale(Transform target, Vector3 to, float duration, Ease ease = Ease.OutCubic,
            Action onComplete = null, float delay = 0f)
        {
            if (target == null) return;
            var from = target.localScale;
            To((target, "scale"), duration, t =>
            {
                if (target) target.localScale = Vector3.LerpUnclamped(from, to, t);
            }, ease, onComplete, delay);
        }

        public static void Move(RectTransform target, Vector2 to, float duration, Ease ease = Ease.OutCubic,
            Action onComplete = null, float delay = 0f)
        {
            if (target == null) return;
            var from = target.anchoredPosition;
            To((target, "move"), duration, t => target.anchoredPosition = Vector2.LerpUnclamped(from, to, t), ease,
                onComplete, delay);
        }

        /// <summary>Quick "bump" used for badges and confirmations.</summary>
        public static void Punch(Transform target, float amount = 0.25f, float duration = 0.35f)
        {
            if (target == null) return;
            To((target, "scale"), duration, t =>
            {
                var s = 1f + Mathf.Sin(t * Mathf.PI) * amount * (1f - t * 0.5f);
                target.localScale = new Vector3(s, s, 1f);
            }, Ease.Linear, () => target.localScale = Vector3.one);
        }
    }
}
