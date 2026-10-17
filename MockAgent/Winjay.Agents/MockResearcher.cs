using System.Collections.Generic;
using Winjay.Core.Proposals;

namespace Winjay.Agents
{
    /// <summary>
    /// Scripted stand-in for the LLM Researcher: emits its evidence script in
    /// order, skipping items that already exist in the world.
    /// </summary>
    public sealed class MockResearcher : IWinjayAgent
    {
        private readonly EvidenceScriptItem[] _script;
        private int _cursor;

        public MockResearcher(string name, params EvidenceScriptItem[] script)
        {
            Name = name ?? "MockResearcher";
            _script = script ?? new EvidenceScriptItem[0];
        }

        public string Name { get; }

        public IEnumerable<AgentProposal> Step(WorldView world)
        {
            while (_cursor < _script.Length)
            {
                var item = _script[_cursor];
                if (world.Exists(item.Id))
                {
                    _cursor++;
                    continue;
                }
                _cursor++;
                yield return new CreateEvidenceProposal(Name, item.Id, item.Content, Name, item.Confidence);
            }
        }
    }
}
