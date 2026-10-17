using System.Collections.Generic;
using UnityEngine;

namespace WinjayXR
{
    /// <summary>
    /// Semantic-to-spatial palette: same colors and layout as the web
    /// visualizer, so every renderer tells the identical story.
    /// </summary>
    public static class WinjayPalette
    {
        public static readonly Dictionary<string, Color> States = new Dictionary<string, Color>
        {
            { "Created", Hex("#9E9E9E") },
            { "Investigating", Hex("#42A5F5") },
            { "Supported", Hex("#66BB6A") },
            { "Resolved", Hex("#FFCA28") },
            { "Conflicted", Hex("#FF7043") },
            { "Falsifying", Hex("#AB47BC") },
            { "Failed", Hex("#EF5350") },
        };

        public static readonly Color ResearcherEvidence = Hex("#4FC3F7");
        public static readonly Color FalsifierEvidence = Hex("#FF8A65");
        public static readonly Color DefaultEvidence = Hex("#B0BEC5");
        public static readonly Color SupportEdge = Hex("#66BB6A");
        public static readonly Color ContradictEdge = Hex("#EF5350");
        public static readonly Color DecisionAccept = Hex("#66BB6A");
        public static readonly Color DecisionReject = Hex("#EF5350");
        public static readonly Color Ground = Hex("#131A28");
        public static readonly Color PanelBg = Hex("#0B0E14");

        // mind map — same hex values as the web viewer (mind.js)
        public static readonly Color MindExplorer = Hex("#4FC3F7");
        public static readonly Color MindConnector = Hex("#66BB6A");
        public static readonly Color MindChallenger = Hex("#FF8A65");
        public static readonly Color MindHuman = Hex("#FFD54F");
        public static readonly Color MindRoot = Hex("#ECEFF1");
        public static readonly Color MindEdgeBranch = Hex("#78909C");
        public static readonly Color MindEdgeSupport = Hex("#66BB6A");
        public static readonly Color MindEdgeConflict = Hex("#EF5350");
        public static readonly Color MindEdgeBridge = Hex("#26A69A");
        public static readonly Color MindLock = Hex("#FFD54F");

        public static readonly Dictionary<string, Color> MindDays = new Dictionary<string, Color>
        {
            { "Day 1", Hex("#81D4FA") },
            { "Day 2", Hex("#A5D6A7") },
            { "Day 3", Hex("#FFE082") },
        };

        public static readonly Vector3[] AgentPositions =
        {
            new Vector3(-5.5f, 1.7f, 0f),   // MockResearcher
            new Vector3(0f, 1.7f, -4.8f),   // MockAnalyzer
            new Vector3(5.5f, 1.7f, 0f),    // MockFalsifier
            new Vector3(0f, 5.4f, 0f),      // MockDecision
        };

        public static readonly Color[] AgentColors =
        {
            Hex("#4FC3F7"), Hex("#81C784"), Hex("#FF8A65"), Hex("#CE93D8"),
        };

        public static readonly Dictionary<string, Vector3> Slots = new Dictionary<string, Vector3>
        {
            { "E001", new Vector3(-2.6f, 1.3f, 1.4f) },
            { "E002", new Vector3(-2.6f, 1.3f, -1.4f) },
            { "E003", new Vector3(2.8f, 1.3f, 1.2f) },
            { "E004", new Vector3(2.8f, 1.3f, -1.2f) },
            { "H001", new Vector3(0f, 2.0f, 0f) },
            { "CF001", new Vector3(1.7f, 1.35f, 0.5f) },
            { "DC001", new Vector3(0f, 4.1f, 0f) },
        };

        public static Vector3 SlotFor(string id)
        {
            if (Slots.TryGetValue(id, out var slot)) return slot;
            var n = id.Length + id[id.Length - 1];
            return new Vector3(((n % 5) - 2) * 1.6f, 1.3f, ((n % 3) - 1) * 1.6f);
        }

        public static Color EvidenceColorFor(string source)
        {
            var s = (source ?? string.Empty).ToLowerInvariant();
            if (s.Contains("falsifier")) return FalsifierEvidence;
            if (s.Contains("researcher")) return ResearcherEvidence;
            return DefaultEvidence;
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }

        public static Material CreateLit(Color color, float emission = 0.45f)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
            return mat;
        }

        public static Material CreateUnlit(Color color)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            return mat;
        }

        public static Material CreateUnlitTransparent(Color color, float alpha)
        {
            var shader = Shader.Find("Sprites/Default");
            var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(color.r, color.g, color.b, alpha);
            return mat;
        }
    }
}
