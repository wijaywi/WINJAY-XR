namespace Winjay.Core.Model
{
    public sealed class Decision : WinjayEntity
    {
        public Decision(string id, string hypothesisId, string verdict, string reason, double confidence)
            : base(id, EntityType.Decision, confidence)
        {
            HypothesisId = hypothesisId;
            Verdict = verdict;
            Reason = reason;
        }

        public string HypothesisId { get; }
        public string Verdict { get; }
        public string Reason { get; }
    }
}
