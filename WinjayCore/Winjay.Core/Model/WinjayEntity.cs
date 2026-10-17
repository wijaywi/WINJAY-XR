using System.Collections.Generic;

namespace Winjay.Core.Model
{
    public abstract class WinjayEntity
    {
        protected WinjayEntity(string id, EntityType type, double confidence)
        {
            Id = id;
            Type = type;
            Confidence = confidence;
        }

        public string Id { get; }
        public EntityType Type { get; }
        public int Version { get; internal set; } = 1;
        public double Confidence { get; internal set; }
        public IReadOnlyList<ProvenanceEntry> Provenance => _provenance;

        private readonly List<ProvenanceEntry> _provenance = new List<ProvenanceEntry>();

        internal void Record(long sequence, string actor, string action)
        {
            _provenance.Add(new ProvenanceEntry(sequence, actor, action));
        }
    }
}
