using System.Collections.Generic;
using System.Linq;
using Winjay.Core.Model;
using Winjay.Core.Proposals;

namespace Winjay.Agents
{
    /// <summary>
    /// Scripted stand-in for the LLM Analyzer: synthesizes one hypothesis from
    /// its evidence list, starts investigation, then applies supporting
    /// evidence while the transition is still legal. If the Falsifier takes
    /// the hypothesis into conflict, the analyzer stands down.
    /// </summary>
    public sealed class MockAnalyzer : IWinjayAgent
    {
        private readonly string _hypothesisId;
        private readonly string[] _supportingEvidence;
        private readonly bool _resolveWhenFullySupported;
        private int _supportCursor;

        public MockAnalyzer(string name, string hypothesisId, bool resolveWhenFullySupported, params string[] supportingEvidence)
        {
            Name = name ?? "MockAnalyzer";
            _hypothesisId = hypothesisId;
            _supportingEvidence = supportingEvidence ?? new string[0];
            _resolveWhenFullySupported = resolveWhenFullySupported;
        }

        public string Name { get; }

        public IEnumerable<AgentProposal> Step(WorldView world)
        {
            var h = world.Get<Hypothesis>(_hypothesisId);

            if (h == null)
            {
                foreach (var id in _supportingEvidence)
                {
                    if (!world.Exists(id)) yield break; // wait for the Researcher
                }
                yield return new CreateHypothesisProposal(Name, _hypothesisId, Statement(), 0.62, _supportingEvidence);
                yield break;
            }

            if (h.State == HypothesisState.Created)
            {
                yield return new StartInvestigationProposal(Name, _hypothesisId);
                yield break;
            }

            while (_supportCursor < _supportingEvidence.Length)
            {
                var id = _supportingEvidence[_supportCursor];
                if (!h.SupportingEvidence.Contains(id))
                {
                    if (h.State == HypothesisState.Investigating || h.State == HypothesisState.Supported)
                    {
                        _supportCursor++;
                        yield return new SupportProposal(Name, _hypothesisId, id);
                    }
                    yield break; // world moved on (falsifier); stand down
                }
                _supportCursor++;
            }

            if (_resolveWhenFullySupported && h.State == HypothesisState.Supported)
            {
                yield return new ResolveHypothesisProposal(Name, _hypothesisId);
            }
        }

        private string Statement()
        {
            return $"Evidence {string.Join(" and ", _supportingEvidence)} supports hypothesis {_hypothesisId}.";
        }
    }
}
