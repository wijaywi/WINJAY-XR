using System.Collections.Generic;
using System.Linq;
using Winjay.Core.Model;
using Winjay.Core.Proposals;

namespace Winjay.Agents
{
    /// <summary>
    /// Scripted stand-in for the LLM Falsifier. It does not ask "is this
    /// hypothesis true?" — it asks "what would make it false?", then hunts
    /// for the contradicting evidence, and drives the conflict pipeline:
    /// CONTRADICT -> FALSIFY -> FAIL.
    /// </summary>
    public sealed class MockFalsifier : IWinjayAgent
    {
        private readonly string _targetHypothesisId;
        private readonly EvidenceScriptItem _contradiction;
        private bool _createdContradiction;

        public MockFalsifier(string name, string targetHypothesisId, EvidenceScriptItem contradiction = null)
        {
            Name = name ?? "MockFalsifier";
            _targetHypothesisId = targetHypothesisId;
            _contradiction = contradiction;
        }

        public string Name { get; }

        public IEnumerable<AgentProposal> Step(WorldView world)
        {
            if (_contradiction == null) yield break; // no attack scripted

            var h = world.Get<Hypothesis>(_targetHypothesisId);
            if (h == null) yield break;

            if (!_createdContradiction)
            {
                if (!world.Exists(_contradiction.Id))
                {
                    _createdContradiction = true;
                    yield return new CreateEvidenceProposal(Name, _contradiction.Id, _contradiction.Content, Name, _contradiction.Confidence);
                    yield break;
                }
            }

            if (world.Exists(_contradiction.Id)
                && !h.ContradictingEvidence.Contains(_contradiction.Id)
                && (h.State == HypothesisState.Investigating || h.State == HypothesisState.Supported))
            {
                yield return new ContradictProposal(Name, _targetHypothesisId, _contradiction.Id);
                yield break;
            }

            if (h.State == HypothesisState.Conflicted)
            {
                yield return new StartFalsificationProposal(Name, _targetHypothesisId);
                yield break;
            }

            if (h.State == HypothesisState.Falsifying)
            {
                yield return new FailHypothesisProposal(Name, _targetHypothesisId);
            }
        }
    }
}
