using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
#endif

namespace WinjayXR.EditorTools
{
    /// <summary>
    /// Best-effort project configuration: OpenXR loaders,
    /// Meta Quest Support feature, Android player settings and a Main scene.
    /// Every step is logged — anything that fails has a manual fallback in
    /// Docs/UNITY_SETUP.md.
    /// </summary>
    public static class WinjayProjectSetup
    {
        [MenuItem("WINJAY/Setup XR Settings")]
        public static void SetupAll()
        {
            SetupXrLoaders();
            EnableMetaOpenXrFeature();
            SetupAndroidPlayerSettings();
            SetupStandaloneSettings();
            ApplyBranding();
            Debug.Log("[WINJAY] Setup complete. See the Console for per-step details.");
        }

        private static void ApplyBranding()
        {
            try
            {
                PlayerSettings.productName = "WINJAY XR";
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/WinjayIcon.png");
                if (icon != null)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android),
                        new[] { icon }, IconKind.Application);
                    PlayerSettings.SetIcons(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone),
                        new[] { icon }, IconKind.Application);
                    Debug.Log("[WINJAY] App icon applied from Assets/Textures/WinjayIcon.png.");
                }
                else
                {
                    Debug.LogWarning("[WINJAY] WinjayIcon.png not found — default icon used.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WINJAY] Failed to set app name/icon: " + e.Message);
            }
        }

        private static void SetupXrLoaders()
        {
#if UNITY_2021_2_OR_NEWER
            var store = GetOrCreateXrSettingsStore();
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                try
                {
                    if (store == null)
                    {
                        Debug.LogWarning("[WINJAY] XR Plug-in Management store unavailable for " + group);
                        continue;
                    }
                    var settings = store.SettingsForBuildTarget(group);
                    if (settings == null)
                    {
                        store.CreateDefaultSettingsForBuildTarget(group);
                        settings = store.SettingsForBuildTarget(group);
                    }
                    if (settings.Manager == null)
                    {
                        store.CreateDefaultManagerSettingsForBuildTarget(group);
                        settings = store.SettingsForBuildTarget(group);
                    }
                    if (settings == null || settings.Manager == null)
                    {
                        Debug.LogWarning("[WINJAY] XR manager could not be created for " + group);
                        continue;
                    }
                    settings.InitManagerOnStart = true;
                    EditorUtility.SetDirty(settings);

                    var openxr = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore
                        .AssignLoader(settings.Manager, "Unity.XR.OpenXR.OpenXRLoader", group);
                    Debug.Log("[WINJAY] XR loader for " + group + ": OpenXR=" + openxr);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[WINJAY] Failed to set XR loader " + group + ": " + e.Message +
                        "\nManual fallback: Project Settings > XR Plug-in Management.");
                }
            }
#endif
        }

#if UNITY_2021_2_OR_NEWER
        /// <summary>
        /// Returns the XR Plug-in Management settings store, creating
        /// Assets/XR/XRGeneralSettingsPerBuildTarget.asset if the project has
        /// none yet (mirrors what the XR Plug-in Management UI does on first
        /// open, using only public APIs).
        /// </summary>
        private static XRGeneralSettingsPerBuildTarget GetOrCreateXrSettingsStore()
        {
            XRGeneralSettingsPerBuildTarget store;
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out store) && store != null)
                return store;

            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                store = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (store == null)
            {
                store = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                if (!AssetDatabase.IsValidFolder("Assets/XR"))
                    AssetDatabase.CreateFolder("Assets", "XR");
                AssetDatabase.CreateAsset(store, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, store, true);
            return store;
        }
#endif

        private static void EnableMetaOpenXrFeature()
        {
            try
            {
                var featureType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.Name == "MetaXRFeature" && typeof(UnityEngine.XR.OpenXR.Features.OpenXRFeature).IsAssignableFrom(t));
                if (featureType == null)
                {
                    Debug.LogWarning("[WINJAY] MetaXRFeature not found (Meta XR SDK not imported yet?).");
                    return;
                }

                var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (settings == null)
                {
                    Debug.LogWarning("[WINJAY] OpenXR settings for Android not available.");
                    return;
                }
                var feature = settings.GetFeature(featureType);
                if (feature == null)
                {
                    Debug.LogWarning("[WINJAY] MetaXRFeature not registered in OpenXR settings.");
                    return;
                }
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                Debug.Log("[WINJAY] MetaXRFeature enabled (hand tracking + passthrough via OpenXR).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WINJAY] Failed to enable MetaXRFeature: " + e.Message +
                    "\nManual fallback: Project Settings > XR Plug-in Management > OpenXR > Android > Meta Quest Support.");
            }
        }

        private static void SetupAndroidPlayerSettings()
        {
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android), "com.winjay.xr");
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android), ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android), ManagedStrippingLevel.Minimal);
                Debug.Log("[WINJAY] Android player settings applied (arm64, IL2CPP, minSdk 32).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WINJAY] Failed to set Android player settings: " + e.Message);
            }
        }

        private static void SetupStandaloneSettings()
        {
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone), "com.winjay.xr");
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                    new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
                // Both legacy and new input stay active (mouse fallback & Meta SDK).
                // No public API exists for this, so the raw property is intentional.
#pragma warning disable CS0618
                PlayerSettings.SetPropertyInt("activeInputHandler", 2, BuildTargetGroup.Standalone);
                PlayerSettings.SetPropertyInt("activeInputHandler", 2, BuildTargetGroup.Android);
#pragma warning restore CS0618
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WINJAY] Failed to set Standalone settings: " + e.Message);
            }
        }

        [MenuItem("WINJAY/Create Main Scene")]
        public static void CreateMainScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true),
            };
            Debug.Log("[WINJAY] Main scene created: Assets/Scenes/Main.unity (the runtime bootstrap fills in everything else).");
        }

        [MenuItem("WINJAY/Full Setup (loaders + scene)")]
        public static void SetupEverything()
        {
            CreateMainScene();
            SetupAll();
        }
    }
}
