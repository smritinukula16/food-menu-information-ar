using System;
using UnityEngine;
using Vuforia;

namespace ARMenu
{
    /// <summary>
    /// Listens to the existing Vuforia DefaultObserverEventHandler (OnTargetFound / OnTargetLost)
    /// without replacing it, so the original renderer toggling and anchoring stay in charge.
    /// </summary>
    public sealed class MarkerTrackingBridge : IDisposable
    {
        readonly DefaultObserverEventHandler mHandler;

        public bool IsTracked { get; private set; }
        public bool HasMarker => mHandler != null;
        public Transform TargetRoot => mHandler ? mHandler.transform : null;

        public event Action Found;
        public event Action Lost;

        public MarkerTrackingBridge(DefaultObserverEventHandler handler)
        {
            mHandler = handler;
            if (mHandler == null)
            {
                Debug.LogWarning("[ARMenu] No DefaultObserverEventHandler assigned – AR marker events are unavailable.");
                return;
            }
            // RemoveListener first guards against duplicate subscriptions (e.g. domain reload in the editor).
            mHandler.OnTargetFound.RemoveListener(HandleFound);
            mHandler.OnTargetLost.RemoveListener(HandleLost);
            mHandler.OnTargetFound.AddListener(HandleFound);
            mHandler.OnTargetLost.AddListener(HandleLost);
            IsTracked = ReadCurrentStatus();
        }

        bool ReadCurrentStatus()
        {
            var observer = mHandler.GetComponent<ObserverBehaviour>();
            if (observer == null) return false;
            var status = observer.TargetStatus.Status;
            switch (mHandler.StatusFilter)
            {
                case DefaultObserverEventHandler.TrackingStatusFilter.Tracked:
                    return status == Status.TRACKED;
                case DefaultObserverEventHandler.TrackingStatusFilter.Tracked_ExtendedTracked:
                    return status == Status.TRACKED || status == Status.EXTENDED_TRACKED;
                default:
                    return status == Status.TRACKED || status == Status.EXTENDED_TRACKED || status == Status.LIMITED;
            }
        }

        void HandleFound()
        {
            if (IsTracked) return;
            IsTracked = true;
            Found?.Invoke();
        }

        void HandleLost()
        {
            if (!IsTracked) return;
            IsTracked = false;
            Lost?.Invoke();
        }

        /// <summary>Editor/testing helper: simulate the marker being found or lost (UI state only).</summary>
        public void Simulate(bool found)
        {
            if (found) HandleFound();
            else HandleLost();
        }

        public void Dispose()
        {
            if (mHandler == null) return;
            mHandler.OnTargetFound.RemoveListener(HandleFound);
            mHandler.OnTargetLost.RemoveListener(HandleLost);
        }
    }
}
