using UnityEngine;
using UnityEngine.XR;

namespace WinjayXR
{
    /// <summary>
    /// XR environment: head-tracked camera, Meta passthrough (MR) and hand
    /// tracking. Everything is best-effort — on a plain desktop without XR
    /// the world still renders with the editor camera.
    /// </summary>
    public sealed class XrEnvironmentSetup : MonoBehaviour
    {
        public WinjaySceneController Controller { get; set; }

        private OVRHand _leftHand;
        private OVRHand _rightHand;

        private void Start()
        {
            ConfigureCamera();
#if UNITY_EDITOR
            // Editor Play without a live XR session (no simulator/headset runtime)
            // must not create OVRManager: its Update loop retries
            // TrySetTrackingOriginMode against a non-running subsystem and logs
            // "Unable to set TrackingOrigin" every frame.
            if (!WinjayPrimitives.XrDisplayActive())
            {
                Debug.Log("[WinjayXR] no XR display in this Play session — desktop preview mode (passthrough and hand tracking are off)");
                return;
            }
            SetupPassthrough();
            SetupHands();
#else
            StartCoroutine(SetupXrWhenReady());
#endif
        }

#if !UNITY_EDITOR
        // On device XR normally starts before the scene loads; give it a few
        // seconds in case the runtime is slow, then proceed regardless.
        private System.Collections.IEnumerator SetupXrWhenReady()
        {
            var deadline = Time.realtimeSinceStartup + 4f;
            while (!WinjayPrimitives.XrDisplayActive() && Time.realtimeSinceStartup < deadline)
                yield return new WaitForSeconds(0.25f);
            if (!WinjayPrimitives.XrDisplayActive())
                Debug.LogWarning("[WinjayXR] XR display did not start — passthrough and hand tracking may not work");
            SetupPassthrough();
            SetupHands();
        }
#endif

        private void LateUpdate()
        {
            if (!WinjayPrimitives.XrDisplayActive()) return;
            try
            {
                var cam = Camera.main;
                if (cam == null) return;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (!head.isValid) return;
                Vector3 pos;
                if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out pos))
                    cam.transform.localPosition = pos;
                Quaternion rot;
                if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rot))
                    cam.transform.localRotation = rot;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WinjayXR] head tracking error: " + e.Message);
            }
        }

        private void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = WinjayPalette.PanelBg;
            cam.nearClipPlane = 0.05f;
        }

        private void SetupPassthrough()
        {
            try
            {
                var manager = OVRManager.instance;
                if (manager == null)
                {
                    var go = new GameObject("OVRManager");
                    manager = go.AddComponent<OVRManager>();
                }
                manager.isInsightPassthroughEnabled = true;

                var layerGo = new GameObject("WinjayPassthrough");
                layerGo.transform.SetParent(transform, false);
                var layer = layerGo.AddComponent<OVRPassthroughLayer>();
                layer.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
                Debug.Log("[WinjayXR] passthrough active — the Winjay world floats in your room");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WinjayXR] passthrough unavailable: " + e.Message);
            }
        }

        private void SetupHands()
        {
            try
            {
                var handTrackingEnabled = OVRPlugin.GetHandTrackingEnabled();
                if (!handTrackingEnabled)
                {
                    Debug.LogWarning("[WinjayXR] the XR runtime reports hand tracking disabled; pinch input is unavailable");
                }
                _leftHand = CreateHand(OVRHand.Hand.HandLeft);
                _rightHand = CreateHand(OVRHand.Hand.HandRight);
                Debug.Log("[WinjayXR] hand components initialized; live tracking still depends on valid runtime input");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WinjayXR] hand tracking error: " + e.Message);
            }
        }

        private OVRHand CreateHand(OVRHand.Hand handType)
        {
            var go = new GameObject("OVRHand_" + handType);
            go.transform.SetParent(transform, false);
            // Keep the GameObject inactive first: SDK 71 runs its data-provider
            // search inside Awake (OVRSkeleton looks for an OVRHand whose
            // GetSkeletonType() == _skeletonType). The internal [SerializeField]
            // fields must be set BEFORE any Awake runs.
            go.SetActive(false);

            var hand = go.AddComponent<OVRHand>();
            // OVRHand.HandType is an internal [SerializeField] in SDK 71 — the
            // only way to set it at runtime is reflection. Serialized fields are
            // preserved by IL2CPP, so Quest builds are safe (plus link.xml).
            SetInstanceField(hand, "HandType", handType);

            var skeleton = go.AddComponent<OVRSkeleton>();
            SetInstanceField(skeleton, "_skeletonType",
                handType == OVRHand.Hand.HandLeft
                    ? OVRSkeleton.SkeletonType.HandLeft
                    : OVRSkeleton.SkeletonType.HandRight);

            go.AddComponent<OVRMesh>();
            go.AddComponent<OVRMeshRenderer>();

            // Awake runs now, in order: OVRHand → OVRSkeleton (finds the OVRHand
            // provider) → OVRMesh/OVRMeshRenderer (auto-binds to the OVRHand).
            go.SetActive(true);
            return hand;
        }

        private static void SetInstanceField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning("[WinjayXR] field '" + fieldName + "' not found on "
                    + target.GetType().Name + " — SDK version may differ");
                return;
            }
            field.SetValue(target, value);
        }

        public OVRHand GetHand(OVRHand.Hand handType)
        {
            if (handType == OVRHand.Hand.HandLeft && _leftHand != null) return _leftHand;
            if (handType == OVRHand.Hand.HandRight && _rightHand != null) return _rightHand;
            return null;
        }
    }
}
