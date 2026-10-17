using UnityEngine;

namespace WinjayXR
{
    /// <summary>
    /// Runtime bootstrap: any scene (even an empty one) becomes a Winjay
    /// world — either the living mind map (the hero demo) or the state
    /// machine, switchable in place from the world's own panel.
    /// </summary>
    public static class WinjayBootstrap
    {
        /// <summary>True = living mind map; false = State machine demo.</summary>
        public static bool MindMode = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<WinjaySceneController>() != null) return;
            if (Object.FindFirstObjectByType<WinjayMindController>() != null) return;
            Build();
        }

        public static void Switch(bool toMind)
        {
            MindMode = toMind;
            var old = GameObject.Find("WINJAY-XR");
            if (old != null) Object.Destroy(old);
            Build();
        }

        private static void Build()
        {
            var root = new GameObject("WINJAY-XR");

            // the camera lives outside the world root so switching modes
            // never destroys it
            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            if (Camera.main == null)
            {
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            camGo.transform.SetParent(null, false);
            var orbit = camGo.GetComponent<EditorCameraOrbit>();
            if (orbit == null) orbit = camGo.AddComponent<EditorCameraOrbit>();
            orbit.target = MindMode ? new Vector3(0f, 0.6f, 0f) : new Vector3(0f, 2f, 0f);
            orbit.distance = MindMode ? 10.5f : 12f;

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.98f, 0.94f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var env = root.AddComponent<XrEnvironmentSetup>();

            if (MindMode)
            {
                var controller = root.AddComponent<WinjayMindController>();
                var interaction = root.AddComponent<WinjayMindInteraction>();
                interaction.Controller = controller;
            }
            else
            {
                var controller = root.AddComponent<WinjaySceneController>();
                env.Controller = controller;
                var interaction = root.AddComponent<WinjayInteraction>();
                interaction.Controller = controller;
            }
        }
    }
}
