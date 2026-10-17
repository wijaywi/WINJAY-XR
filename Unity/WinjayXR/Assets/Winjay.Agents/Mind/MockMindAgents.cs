using System.Collections.Generic;

namespace Winjay.Agents.Mind
{
    /// <summary>Grows branches: creates nodes and branch edges in scripted order.</summary>
    public sealed class MockExplorer : ScriptedMindAgent
    {
        public const string Role = "Explorer";

        public MockExplorer(IReadOnlyList<MindScriptItem> script) : base(Role, script)
        {
        }
    }

    /// <summary>
    /// Finds distant connections: support links, cross-branch bridges, and
    /// ghost suggestions when a pruned node leaves a hole in the map.
    /// </summary>
    public sealed class MockConnector : ScriptedMindAgent
    {
        public const string Role = "Connector";

        public MockConnector(IReadOnlyList<MindScriptItem> script) : base(Role, script)
        {
        }
    }

    /// <summary>Marks weak and colliding ideas with conflict links.</summary>
    public sealed class MockChallenger : ScriptedMindAgent
    {
        public const string Role = "Challenger";

        public MockChallenger(IReadOnlyList<MindScriptItem> script) : base(Role, script)
        {
        }
    }
}
