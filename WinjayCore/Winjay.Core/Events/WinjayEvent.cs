using Winjay.Core.Model;
using Winjay.Core.State;

namespace Winjay.Core.Events
{
    public enum WinjayEventType
    {
        EntityCreated,
        StateChanged,
        EvidenceAdded,
        HypothesisCreated,
        ConflictDetected,
        FalsificationStarted,
        DecisionProposed,
        DecisionCommitted,
        ProposalRejected
    }

    public sealed class WinjayEvent
    {
        public WinjayEvent(long sequence, WinjayEventType type, string actor,
            string entityId = null, EntityType? entityType = null, Delta delta = null, string detail = null)
        {
            Sequence = sequence;
            Type = type;
            Actor = actor;
            EntityId = entityId;
            EntityType = entityType;
            Delta = delta;
            Detail = detail;
        }

        public long Sequence { get; }
        public WinjayEventType Type { get; }
        public string Actor { get; }
        public string EntityId { get; }
        public EntityType? EntityType { get; }
        public Delta Delta { get; }
        public string Detail { get; }
    }
}
