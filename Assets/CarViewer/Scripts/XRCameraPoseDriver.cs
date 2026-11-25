using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CarViewer
{
    /// <summary>
    /// Drives the AR camera transform from the XR device pose (ARCore).
    /// Replaces TrackedPoseDriver, whose HandheldARInputDevice layout is not registered
    /// in Unity 6000.6, which would leave the camera stuck at the origin.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    public class XRCameraPoseDriver : MonoBehaviour
    {
        static readonly List<InputDevice> devices = new List<InputDevice>();
        InputDevice device;

        void OnEnable() => Application.onBeforeRender += OnBeforeRender;
        void OnDisable() => Application.onBeforeRender -= OnBeforeRender;
        void Update() => ApplyPose();

        [BeforeRenderOrder(-30000)]
        void OnBeforeRender() => ApplyPose();

        void ApplyPose()
        {
            if (!device.isValid)
            {
                InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, devices);
                if (devices.Count == 0) InputDevices.GetDevices(devices);
                device = default;
                foreach (var d in devices)
                {
                    if (d.TryGetFeatureValue(CommonUsages.devicePosition, out _) ||
                        d.TryGetFeatureValue(CommonUsages.centerEyePosition, out _))
                    {
                        device = d;
                        break;
                    }
                }
                if (!device.isValid) return;
            }

            bool hasPos = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 p) ||
                          device.TryGetFeatureValue(CommonUsages.devicePosition, out p);
            bool hasRot = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion r) ||
                          device.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
            if (hasPos) transform.localPosition = p;
            if (hasRot) transform.localRotation = r;
        }
    }
}
