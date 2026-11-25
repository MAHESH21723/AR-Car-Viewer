using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

namespace CarViewer
{
    public enum PlacementState { Scanning, Ready, Placed }

    /// <summary>
    /// Plane detection, the placement reticle and touch gestures:
    /// tap = place / move, pinch = scale, one-finger horizontal drag = rotate.
    /// </summary>
    public class ARPlacementController : MonoBehaviour
    {
        public ARRaycastManager raycastManager;
        public ARPlaneManager planeManager;
        public Camera arCamera;
        public Transform reticle;
        public CarModel car;

        public event Action<PlacementState> StateChanged;
        public PlacementState State { get; private set; } = PlacementState.Scanning;
        public bool PlanesVisible { get; private set; } = true;

        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        bool reticleHasHit;
        Pose reticlePose;

        // Gesture state
        int trackedTouchId = -1;
        Vector2 touchStart;
        float touchStartTime;
        bool dragged, touchBlocked, pinching;
        float lastPinchDist;

        void OnEnable()
        {
            EnhancedTouch.EnhancedTouchSupport.Enable();
            if (planeManager != null)
            {
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            }
            SetReticleVisible(false);
        }

        void OnDisable()
        {
            if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            EnhancedTouch.EnhancedTouchSupport.Disable();
            SetReticleVisible(false);
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var p in args.added) p.gameObject.SetActive(PlanesVisible);
            if (State == PlacementState.Scanning && planeManager.trackables.count > 0)
                SetState(PlacementState.Ready);
        }

        void SetState(PlacementState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        public void SetPlanesVisible(bool visible)
        {
            PlanesVisible = visible;
            if (planeManager == null) return;
            foreach (var p in planeManager.trackables) p.gameObject.SetActive(visible);
        }

        /// <summary>Removes the car and returns to scanning for a surface.</summary>
        public void ResetPlacement()
        {
            car.ResetAll();
            SetState(planeManager != null && planeManager.trackables.count > 0
                ? PlacementState.Ready : PlacementState.Scanning);
        }

        void Update()
        {
            UpdateReticle();
            HandleTouches();
        }

        void UpdateReticle()
        {
            reticleHasHit = false;
            if (State != PlacementState.Placed && raycastManager != null)
            {
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (raycastManager.Raycast(centre, hits, TrackableType.PlaneWithinPolygon))
                {
                    reticleHasHit = true;
                    reticlePose = hits[0].pose;
                    if (State == PlacementState.Scanning) SetState(PlacementState.Ready);
                }
            }
            SetReticleVisible(reticleHasHit);
            if (reticleHasHit && reticle != null)
                reticle.SetPositionAndRotation(reticlePose.position + Vector3.up * 0.005f,
                    Quaternion.Euler(0f, reticlePose.rotation.eulerAngles.y, 0f));
        }

        void SetReticleVisible(bool v)
        {
            if (reticle != null && reticle.gameObject.activeSelf != v) reticle.gameObject.SetActive(v);
        }

        // ---- gestures -------------------------------------------------------

        void HandleTouches()
        {
            var touches = EnhancedTouch.Touch.activeTouches;
            if (touches.Count == 0) { trackedTouchId = -1; pinching = false; return; }

            if (touches.Count >= 2)
            {
                // Pinch to scale.
                if (State == PlacementState.Placed && !IsOverUI(touches[0].screenPosition) &&
                    !IsOverUI(touches[1].screenPosition))
                {
                    float d = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                    if (pinching && lastPinchDist > 1f) car.ScaleFactor *= d / lastPinchDist;
                    pinching = true;
                    lastPinchDist = d;
                }
                trackedTouchId = -1;
                dragged = true;
                return;
            }
            pinching = false;

            var t = touches[0];
            if (t.touchId != trackedTouchId)
            {
                trackedTouchId = t.touchId;
                touchStart = t.screenPosition;
                touchStartTime = Time.unscaledTime;
                dragged = false;
                touchBlocked = IsOverUI(t.screenPosition);
            }
            if (touchBlocked) return;

            if (!dragged && Vector2.Distance(t.screenPosition, touchStart) > 24f) dragged = true;
            if (dragged && State == PlacementState.Placed && t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                car.RotateBy(-t.delta.x * 0.25f);

            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                if (!dragged && Time.unscaledTime - touchStartTime < 0.5f) OnTap(t.screenPosition);
                trackedTouchId = -1;
            }
        }

        void OnTap(Vector2 screenPos)
        {
            if (State != PlacementState.Placed)
            {
                // Prefer the reticle under the screen centre, otherwise raycast the tap point.
                if (reticleHasHit) PlaceCar(reticlePose.position);
                else if (RaycastPlane(screenPos, out var pose)) PlaceCar(pose.position);
            }
            else if (RaycastPlane(screenPos, out var pose))
            {
                car.MoveTo(pose.position);
            }
        }

        bool RaycastPlane(Vector2 screenPos, out Pose pose)
        {
            pose = default;
            if (raycastManager == null || !raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
                return false;
            pose = hits[0].pose;
            return true;
        }

        void PlaceCar(Vector3 position)
        {
            car.PlaceAt(position, arCamera.transform.position);
            SetState(PlacementState.Placed);
        }

        static bool IsOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            EventSystem.current.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }
    }
}
