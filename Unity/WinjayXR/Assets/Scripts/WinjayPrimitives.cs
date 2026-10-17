using UnityEngine;

namespace WinjayXR
{
    /// <summary>Procedural geometry used by the spatial scene (no external models).</summary>
    public static class WinjayPrimitives
    {
        private static readonly System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem> Displays =
            new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();

        /// <summary>XRDevice.isPresent is obsolete-as-error in Unity 6 — poll the display subsystem instead.</summary>
        public static bool XrDisplayActive()
        {
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            for (var i = 0; i < Displays.Count; i++)
            {
                if (Displays[i].running) return true;
            }
            return false;
        }

        public static Mesh Octahedron(float radius)
        {
            var v = new Vector3[]
            {
                new Vector3(0, radius, 0),
                new Vector3(radius, 0, 0),
                new Vector3(0, 0, radius),
                new Vector3(-radius, 0, 0),
                new Vector3(0, 0, -radius),
                new Vector3(0, -radius, 0),
            };
            var t = new int[]
            {
                0, 2, 1, 0, 1, 4, 0, 4, 3, 0, 3, 2,
                5, 1, 2, 5, 4, 1, 5, 3, 4, 5, 2, 3,
            };
            var mesh = new Mesh { name = "Octahedron" };
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh Ring(float radius, float thickness, int segments = 48)
        {
            var vertices = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                var ca = Mathf.Cos(a);
                var sa = Mathf.Sin(a);
                vertices[i * 2] = new Vector3(ca * (radius - thickness), sa * (radius - thickness), 0);
                vertices[i * 2 + 1] = new Vector3(ca * (radius + thickness), sa * (radius + thickness), 0);
                var next = (i + 1) % segments;
                tris[i * 6] = i * 2;
                tris[i * 6 + 1] = next * 2;
                tris[i * 6 + 2] = i * 2 + 1;
                tris[i * 6 + 3] = next * 2;
                tris[i * 6 + 4] = next * 2 + 1;
                tris[i * 6 + 5] = i * 2 + 1;
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh Quad(float w, float h)
        {
            var mesh = new Mesh { name = "Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-w / 2f, -h / 2f, 0),
                new Vector3(w / 2f, -h / 2f, 0),
                new Vector3(w / 2f, h / 2f, 0),
                new Vector3(-w / 2f, h / 2f, 0),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        public static TextMesh MakeLabel(string text, Color color, float characterSize = 0.14f, int fontSize = 48)
        {
            var go = new GameObject("Label");
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = characterSize;
            tm.fontSize = fontSize;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.richText = false;
            return tm;
        }

        /// <summary>
        /// Queues a measured fit: legacy TextMesh scales with fontSize as well
        /// as characterSize, and inherits non-uniform parent scales, so the only
        /// reliable size control is to measure the generated mesh and rescale.
        /// </summary>
        public static LabelAutoFit FitLabel(TextMesh label, float maxWidth, float maxHeight, bool counterParentScale = true)
        {
            var fit = label.gameObject.AddComponent<LabelAutoFit>();
            fit.maxWidth = maxWidth;
            fit.maxHeight = maxHeight;
            fit.counterParentScale = counterParentScale;
            return fit;
        }

        public static LineRenderer MakeLine(Color color, float width = 0.03f)
        {
            var go = new GameObject("Line");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = CreateUnlit(color);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.useWorldSpace = true;
            return lr;
        }

        public static Material CreateUnlit(Color color)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            return mat;
        }
    }

    /// <summary>
    /// Rescales a label's generated mesh so it fits its slot regardless of
    /// fontSize/characterSize semantics (see WinjayPrimitives.FitLabel).
    /// Fits whenever the text changes; measurement happens one frame after
    /// the mesh is created, before it is ever rendered.
    /// </summary>
    public sealed class LabelAutoFit : MonoBehaviour
    {
        public float maxWidth = 1f;
        public float maxHeight = 0.12f;
        public bool counterParentScale = true;

        private string _fittedText;
        private bool _counterScaled;

        private void LateUpdate()
        {
            var tm = GetComponent<TextMesh>();
            if (tm == null || tm.text == _fittedText) return;
            var filter = GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0) return; // retry next frame
            var size = mesh.bounds.size;
            if (size.x <= 0.0001f || size.y <= 0.0001f) return;

            if (counterParentScale && !_counterScaled)
            {
                _counterScaled = true;
                var parent = transform.parent;
                var ps = parent != null ? parent.lossyScale : Vector3.one;
                transform.localScale = new Vector3(
                    ps.x > 0.0001f ? 1f / ps.x : 1f,
                    ps.y > 0.0001f ? 1f / ps.y : 1f,
                    ps.z > 0.0001f ? 1f / ps.z : 1f);
            }

            var s = Mathf.Clamp(Mathf.Min(maxWidth / size.x, maxHeight / size.y), 0.02f, 1.6f);
            if (s < 0.98f || s > 1.02f) tm.characterSize *= s;
            _fittedText = tm.text;
        }
    }
}
