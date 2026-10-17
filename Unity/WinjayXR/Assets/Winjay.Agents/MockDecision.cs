using System.Collections.Generic;
using Winjay.Core.Model;
using Winjay.Core.Proposals;

namespace Winjay.Agents
{
    /// <summary>
    /// Scripted stand-in for the LLM Decision agent: proposes a verdict once
    /// the hypothesis reaches a terminal state. The state engine still
    /// validates and commits.
    /// </summary>
    public sealed class MockDecision : IWinjayAgent
    {
        private readonly string _targetHypothesisId;
        private bool _decided;

        public MockDecision(string name, string targetHypothesisId)
        {
            Name = name ?? "MockDecision";
            _targetHypothesisId = targetHypothesisId;
        }

        public string Name { get; }

        public IEnumerable<AgentProposal> Step(WorldView world)
        {
            if (_decided) yield break;

            var h = world.Get<Hypothesis>(_targetHypothesisId);
            if (h == null) yield break;

            if (h.State == HypothesisState.Failed)
            {
                _decided = true;
                yield return new ProposeDecisionProposal(Name, _targetHypothesisId,
                    "REJECT_HYPOTHESIS", "Falsification evidence dominates", 0.87);
            }
            else if (h.State == HypothesisState.Resolved)
            {
                _decided = true;
                yield return new ProposeDecisionProposal(Name, _targetHypothesisId,
                    "ACCEPT_HYPOTHESIS", "Hypothesis survived falsification", 0.91);
            }
        }
    }
}
