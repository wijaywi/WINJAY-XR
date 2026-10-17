using System.Collections.Generic;
using Winjay.Core.Proposals;

namespace Winjay.Agents
{
    /// <summary>
    /// Contract shared by mock agents and, later, real LLM-backed agents:
    /// observe the world, emit proposals. The interface will not change when
    /// the mock implementation is replaced.
    /// </summary>
    public interface IWinjayAgent
    {
        string Name { get; }

        IEnumerable<AgentProposal> Step(WorldView world);
    }
}
