namespace Winjay.Core.Model
{
    public sealed class Conflict : WinjayEntity
    {
        public Conflict(string id, string hypothesisId, string evidenceId, double confidence)
            : base(id, EntityType.Conflict, confidence)
        {
            HypothesisId = hypothesisId;
            EvidenceId = evidenceId;
        }

        public string HypothesisId { get; }
        public string EvidenceId { get; }
    }
}
