using System.Collections.Generic;

namespace Winjay.Core.Model
{
    public sealed class Hypothesis : WinjayEntity
    {
        public Hypothesis(string id, string statement, double confidence)
            : base(id, EntityType.Hypothesis, confidence)
        {
            Statement = statement;
        }

        public string Statement { get; }
        public HypothesisState State { get; internal set; }
        public IReadOnlyList<string> SupportingEvidence => _supporting;
        public IReadOnlyList<string> ContradictingEvidence => _contradicting;

        private readonly List<string> _supporting = new List<string>();
        private readonly List<string> _contradicting = new List<string>();

        internal void AddSupport(string evidenceId)
        {
            _supporting.Add(evidenceId);
        }

        internal void AddContradiction(string evidenceId)
        {
            _contradicting.Add(evidenceId);
        }
    }
}
