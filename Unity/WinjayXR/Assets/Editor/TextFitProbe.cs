using UnityEngine;

namespace WinjayXR.EditorTools
{
    /// <summary>
    /// Batchmode probe: measures real world-space size of TextMesh labels
    /// (characterSize / fontSize semantics and the effect of a scaled parent)
    /// so the button auto-fit uses measured numbers, not guesses.
    /// Run: Unity.exe -batchmode -quit -projectPath ... -executeMethod WinjayXR.EditorTools.TextFitProbe.Run
    /// </summary>
    public static class TextFitProbe
    {
        public static void Run()
        {
            var longText = "I easily get motion sick";
            var shortText = "RUN";

            var sizeA = MeasureLabel(longText, 0.09f, 52, Vector3.one, Vector3.one, "A bare charSize0.09");
            var sizeA2 = MeasureLabel(shortText, 0.09f, 52, Vector3.one, Vector3.one, "A2 bare RUN");
            var sizeB = MeasureLabel(longText, 0.09f, 52, new Vector3(1.06f, 0.16f, 1f), Vector3.one, "B under quad(1.06,0.16)");
            var sizeC = MeasureLabel(longText, 0.09f, 52, new Vector3(1.06f, 0.16f, 1f),
                new Vector3(1f / 1.06f, 1f / 0.16f, 1f), "C counter-scaled");
            var sizeD = MeasureLabel(shortText, 0.09f, 52, new Vector3(0.56f, 0.16f, 1f),
                new Vector3(1f / 0.56f, 1f / 0.16f, 1f), "D RUN counter-scaled 0.56");

            Debug.Log("[WINJAY-PROBE] long='" + longText + "' A=" + Fmt(sizeA) + " A2='" + shortText + "'=" + Fmt(sizeA2));
            Debug.Log("[WINJAY-PROBE] B=" + Fmt(sizeB) + " C=" + Fmt(sizeC) + " D=" + Fmt(sizeD));
            Debug.Log("[WINJAY-PROBE] per-char world width at charSize 0.09 (bare) = " +
                      (sizeA.x / longText.Length).ToString("0.0000") + " ; height = " + sizeA.y.ToString("0.0000"));
        }

        private static Vector3 MeasureLabel(string text, float charSize, int fontSize,
            Vector3 parentScale, Vector3 localScale, string tag)
        {
            var parent = new GameObject("ProbeParent_" + tag);
            parent.transform.localScale = parentScale;

            var label = WinjayPrimitives.MakeLabel(text, Color.white, charSize, fontSize);
            label.transform.SetParent(parent.transform, false);
            label.transform.localScale = localScale;

            var mf = label.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            var verts = mesh != null ? mesh.vertexCount : -1;

            var size = label.GetComponent<Renderer>().bounds.size;
            Object.DestroyImmediate(parent);
            if (verts <= 0) Debug.LogWarning("[WINJAY-PROBE] '" + tag + "' mesh verts=" + verts + " (mesh not built in edit mode?)");
            return size;
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ")";
        }
    }
}
