namespace Winjay.Core.Proposals
{
    public enum ProposalKind
    {
        CreateEvidence,
        CreateHypothesis,
        StartInvestigation,
        Support,
        Contradict,
        StartFalsification,
        Fail,
        Resolve,
        ProposeDecision
    }

    /// <summary>
    /// The only way an agent can affect the world. Agents propose; the state
    /// engine validates and commits. Agents never mutate entities directly.
    /// </summary>
    public abstract class AgentProposal
    {
        protected AgentProposal(ProposalKind kind, string actor)
        {
            Kind = kind;
            Actor = actor;
        }

        public ProposalKind Kind { get; }
        public string Actor { get; }

        /// <summary>Entity targeted by this proposal (mutation proposals only).</summary>
        public string TargetId { get; protected set; }

        /// <summary>
        /// Version the proposing agent last observed. -1 means the agent did
        /// not pin a version and the check is skipped.
        /// </summary>
        public int ExpectedVersion { get; protected set; } = -1;
    }

    public sealed class CreateEvidenceProposal : AgentProposal
    {
        public CreateEvidenceProposal(string actor, string evidenceId, string content, string source, double confidence)
            : base(ProposalKind.CreateEvidence, actor)
        {
            EvidenceId = evidenceId;
            Content = content;
            Source = source;
            Confidence = confidence;
        }

        public string EvidenceId { get; }
        public string Content { get; }
        public string Source { get; }
        public double Confidence { get; }
    }

    public sealed class CreateHypothesisProposal : AgentProposal
    {
        public CreateHypothesisProposal(string actor, string hypothesisId, string statement, double confidence, params string[] basedOnEvidence)
            : base(ProposalKind.CreateHypothesis, actor)
        {
            HypothesisId = hypothesisId;
            Statement = statement;
            Confidence = confidence;
            BasedOnEvidence = basedOnEvidence ?? new string[0];
        }

        public string HypothesisId { get; }
        public string Statement { get; }
        public double Confidence { get; }
        public string[] BasedOnEvidence { get; }
    }

    public sealed class StartInvestigationProposal : AgentProposal
    {
        public StartInvestigationProposal(string actor, string hypothesisId, int expectedVersion = -1)
            : base(ProposalKind.StartInvestigation, actor)
        {
            HypothesisId = hypothesisId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
    }

    public sealed class SupportProposal : AgentProposal
    {
        public SupportProposal(string actor, string hypothesisId, string evidenceId, int expectedVersion = -1)
            : base(ProposalKind.Support, actor)
        {
            HypothesisId = hypothesisId;
            EvidenceId = evidenceId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
        public string EvidenceId { get; }
    }

    public sealed class ContradictProposal : AgentProposal
    {
        public ContradictProposal(string actor, string hypothesisId, string evidenceId, int expectedVersion = -1)
            : base(ProposalKind.Contradict, actor)
        {
            HypothesisId = hypothesisId;
            EvidenceId = evidenceId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
        public string EvidenceId { get; }
    }

    public sealed class StartFalsificationProposal : AgentProposal
    {
        public StartFalsificationProposal(string actor, string hypothesisId, int expectedVersion = -1)
            : base(ProposalKind.StartFalsification, actor)
        {
            HypothesisId = hypothesisId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
    }

    public sealed class FailHypothesisProposal : AgentProposal
    {
        public FailHypothesisProposal(string actor, string hypothesisId, int expectedVersion = -1)
            : base(ProposalKind.Fail, actor)
        {
            HypothesisId = hypothesisId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
    }

    public sealed class ResolveHypothesisProposal : AgentProposal
    {
        public ResolveHypothesisProposal(string actor, string hypothesisId, int expectedVersion = -1)
            : base(ProposalKind.Resolve, actor)
        {
            HypothesisId = hypothesisId;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
    }

    public sealed class ProposeDecisionProposal : AgentProposal
    {
        public ProposeDecisionProposal(string actor, string hypothesisId, string verdict, string reason, double confidence, int expectedVersion = -1)
            : base(ProposalKind.ProposeDecision, actor)
        {
            HypothesisId = hypothesisId;
            Verdict = verdict;
            Reason = reason;
            Confidence = confidence;
            TargetId = hypothesisId;
            ExpectedVersion = expectedVersion;
        }

        public string HypothesisId { get; }
        public string Verdict { get; }
        public string Reason { get; }
        public double Confidence { get; }
    }
}
