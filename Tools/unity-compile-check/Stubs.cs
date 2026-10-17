// Compile-time stubs for package APIs used by the Winjay scripts.
// These exist ONLY for the offline compile check (Tools/unity-compile-check/
// check.sh) so the scripts can be validated against the real Unity reference
// assemblies without downloading the InputSystem / Meta XR / OpenXR packages.
// They are never part of the Unity project — do not copy into Assets/.
using System;
using UnityEngine;

namespace UnityEngine.InputSystem
{
    public class Vector2Control { public Vector2 ReadValue() => Vector2.zero; }
    public class ButtonControl { public bool isPressed => false; public bool wasPressedThisFrame => false; }
    public class Mouse
    {
        public static Mouse current => null;
        public ButtonControl leftButton => null;
        public ButtonControl rightButton => null;
        public Vector2Control position => null;
        public Vector2Control delta => null;
        public Vector2Control scroll => null;
    }
}

public class OVRManager : MonoBehaviour
{
    public static OVRManager instance => null;
    public bool isInsightPassthroughEnabled { get; set; }
}

public class OVRPassthroughLayer : MonoBehaviour
{
    public enum ProjectionSurfaceType { UserDefined, Reconstructed, Mesh }
    public ProjectionSurfaceType projectionSurfaceType { get; set; }
}

public static class OVRPlugin
{
    public static bool GetHandTrackingEnabled() => false;
}

public class OVRHand : MonoBehaviour
{
    public enum Hand { None = -1, HandLeft = 0, HandRight = 1 }
    public enum HandFinger { Thumb, Index, Middle, Ring, Pinky, Max }
    public bool IsTracked => false;
    public bool GetFingerIsPinching(HandFinger finger) => false;
    // SDK 71 exposes PointerPose as a Transform (mirrored exactly here).
    public Transform PointerPose => null;
}

public class OVRSkeleton : MonoBehaviour
{
    public enum SkeletonType { None, Body, HandLeft, HandRight, Max }
}

public class OVRMesh : MonoBehaviour { }

public class OVRMeshRenderer : MonoBehaviour { }

namespace UnityEngine.XR.OpenXR
{
    public class OpenXRSettings : UnityEngine.ScriptableObject
    {
        public static OpenXRSettings GetSettingsForBuildTargetGroup(UnityEditor.BuildTargetGroup group) => null;
        public Features.OpenXRFeature GetFeature(Type type) => null;
    }
}

namespace UnityEngine.XR.OpenXR.Features
{
    public class OpenXRFeature : UnityEngine.ScriptableObject { public bool enabled { get; set; } }
}

namespace UnityEngine.XR.Management
{
    public class XRManagerSettings : UnityEngine.ScriptableObject { }

    // Mirrors the real XRManagement 4.5 runtime class.
    public class XRGeneralSettings : UnityEngine.ScriptableObject
    {
        public static string k_SettingsKey = "com.unity.xr.management.loader_settings";
        public XRManagerSettings Manager { get; set; }
        public bool InitManagerOnStart { get; set; }
    }
}

namespace UnityEditor.XR.Management
{
    // Mirrors the real XRManagement 4.5 editor class (public API only).
    public class XRGeneralSettingsPerBuildTarget : UnityEngine.ScriptableObject
    {
        public static UnityEngine.XR.Management.XRGeneralSettings XRGeneralSettingsForBuildTarget(UnityEditor.BuildTargetGroup group) => null;
        public UnityEngine.XR.Management.XRGeneralSettings SettingsForBuildTarget(UnityEditor.BuildTargetGroup group) => null;
        public void CreateDefaultSettingsForBuildTarget(UnityEditor.BuildTargetGroup group) { }
        public void CreateDefaultManagerSettingsForBuildTarget(UnityEditor.BuildTargetGroup group) { }
    }
}

namespace UnityEditor.XR.Management.Metadata
{
    public static class XRPackageMetadataStore
    {
        public static bool AssignLoader(UnityEngine.XR.Management.XRManagerSettings settings, string loaderTypeName, UnityEditor.BuildTargetGroup group) => false;
    }
}
