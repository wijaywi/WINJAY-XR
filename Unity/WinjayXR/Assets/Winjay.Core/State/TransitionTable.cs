using System.Collections.Generic;
using Winjay.Core.Model;
using Winjay.Core.Proposals;

namespace Winjay.Core.State
{
    internal static class TransitionTable
    {
        // Six core transitions from the spec, plus two edges required by
        // DemoScenario01: late support (SUPPORTED -> SUPPORTED) and late
        // contradiction (SUPPORTED -> CONFLICTED).
        private static readonly Dictionary<(HypothesisState, ProposalKind), HypothesisState> Transitions =
            new Dictionary<(HypothesisState, ProposalKind), HypothesisState>
            {
                { (HypothesisState.Created, ProposalKind.StartInvestigation), HypothesisState.Investigating },
                { (HypothesisState.Investigating, ProposalKind.Support), HypothesisState.Supported },
                { (HypothesisState.Supported, ProposalKind.Support), HypothesisState.Supported },
                { (HypothesisState.Supported, ProposalKind.Resolve), HypothesisState.Resolved },
                { (HypothesisState.Investigating, ProposalKind.Contradict), HypothesisState.Conflicted },
                { (HypothesisState.Supported, ProposalKind.Contradict), HypothesisState.Conflicted },
                { (HypothesisState.Conflicted, ProposalKind.StartFalsification), HypothesisState.Falsifying },
                { (HypothesisState.Falsifying, ProposalKind.Fail), HypothesisState.Failed },
            };

        public static bool TryGetTransition(HypothesisState from, ProposalKind kind, out HypothesisState to)
        {
            return Transitions.TryGetValue((from, kind), out to);
        }
    }
}
