namespace Winjay.Core.Mind
{
    /// <summary>
    /// Every visible change comes from the event log — the foundation for
    /// deterministic replay and the rewind slider. Events carry the full
    /// payload needed to re-apply them.
    /// </summary>
    public enum MindEventType
    {
        NodeAdded,
        LinkCreated,
        StrengthChanged,
        NodePruned,
        LinkDeactivated,
        LinkRewired,
        NodeLocked,
        NodeUnlocked,
        HumanInput,
        GhostSuggested,
        GhostAccepted,
        GhostRejected,
        NodeMerged,
        PhaseAssigned
    }

    public sealed class MindEvent
    {
        public MindEvent(long sequence, MindEventType type, string actor)
        {
            Sequence = sequence;
            Type = type;
            Actor = actor;
        }

        public long Sequence { get; internal set; }
        public MindEventType Type { get; internal set; }
        public string Actor { get; internal set; }

        // node payload
        public string NodeId { get; internal set; }
        public string Label { get; internal set; }
        public MindNodeKind? Kind { get; internal set; }
        public MindContributor? Contributor { get; internal set; }
        public string ParentId { get; internal set; }
        public MindVec3? Position { get; internal set; }
        public bool? Ghost { get; internal set; }
        public string Phase { get; internal set; }
        public string MergedIntoId { get; internal set; }

        // link payload
        public string LinkId { get; internal set; }
        public string FromId { get; internal set; }
        public string ToId { get; internal set; }
        public MindLinkType? LinkType { get; internal set; }
        public MindContributor? LinkOriginator { get; internal set; }
        public double? Weight { get; internal set; }
        public string Detail { get; internal set; }

        // strength payload
        public double? OldStrength { get; internal set; }
        public double? NewStrength { get; internal set; }
    }
}
