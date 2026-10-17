using System.Collections.Generic;

namespace Winjay.Core.Mind
{
    /// <summary>
    /// One idea in the living mind map. All mutations flow through MindEngine
    /// (PROPOSE → VALIDATE → COMMIT); agents and the human curator never
    /// touch nodes directly.
    /// </summary>
    public sealed class MindNode
    {
        public string Id { get; internal set; }
        public string Label { get; internal set; }
        public MindNodeKind Kind { get; internal set; }
        public MindContributor Contributor { get; internal set; }
        public string ParentId { get; internal set; }
        public MindVec3 Position { get; internal set; }
        public int Version { get; internal set; } = 1;

        /// <summary>Deterministic confidence of the idea, recomputed from its links.</summary>
        public double Strength { get; internal set; }

        /// <summary>Locked nodes are immune to every mutation (gold).</summary>
        public bool Locked { get; internal set; }

        /// <summary>Pruned nodes leave the map but stay in the journal for replay.</summary>
        public bool Pruned { get; internal set; }

        /// <summary>Ghost nodes are agent suggestions waiting for the curator (accept/reject).</summary>
        public bool Ghost { get; internal set; }

        /// <summary>Day assignment after the map "settles" (Day 1 / Day 2 / Day 3), if any.</summary>
        public string Phase { get; internal set; }

        public IReadOnlyList<string> Provenance => _provenance;

        private readonly List<string> _provenance = new List<string>();

        internal void Record(string entry)
        {
            _provenance.Add(entry);
        }
    }

    /// <summary>
    /// A meaningful edge between two ideas. The link type is the semantic:
    /// Branch = hierarchy, Support = green, Conflict = red tension,
    /// Bridge = unexpected cross-branch arc.
    /// </summary>
    public sealed class MindLink
    {
        public string Id { get; internal set; }
        public string FromId { get; internal set; }
        public string ToId { get; internal set; }
        public MindLinkType Type { get; internal set; }
        public MindContributor Originator { get; internal set; }
        public double Weight { get; internal set; } = 1.0;
        public string Detail { get; internal set; }
        public bool Active { get; internal set; } = true;

        public string OtherEnd(string nodeId)
        {
            return nodeId == FromId ? ToId : FromId;
        }
    }
}
