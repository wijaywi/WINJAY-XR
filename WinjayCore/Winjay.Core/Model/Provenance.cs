using System;

namespace Winjay.Core.Model
{
    public sealed class ProvenanceEntry
    {
        public ProvenanceEntry(long sequence, string actor, string action)
        {
            Sequence = sequence;
            Actor = actor;
            Action = action;
            UtcTime = DateTime.UtcNow;
        }

        public long Sequence { get; }
        public string Actor { get; }
        public string Action { get; }
        public DateTime UtcTime { get; }
    }
}
