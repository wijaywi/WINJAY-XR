using System.Collections.Generic;
using Winjay.Core.Mind;

namespace Winjay.Agents.Mind
{
    /// <summary>
    /// Read-only window onto the living mind map. Agents receive a fresh view
    /// on every step and can only propose — the engine validates and commits.
    /// </summary>
    public sealed class MindWorldView
    {
        private readonly MindEngine _engine;

        public MindWorldView(MindEngine engine)
        {
            _engine = engine;
        }

        public IReadOnlyDictionary<string, MindNode> Nodes => _engine.Nodes;
        public IReadOnlyDictionary<string, MindLink> Links => _engine.Links;
        public IReadOnlyList<MindEvent> Journal => _engine.Journal;
        public MindNode Root => _engine.Root;

        public MindNode GetNode(string id)
        {
            return _engine.GetNode(id);
        }

        public MindLink GetLink(string id)
        {
            return _engine.GetLink(id);
        }

        public bool Exists(string id)
        {
            return _engine.GetNode(id) != null;
        }

        /// <summary>True when the node exists and has not been pruned or rejected.</summary>
        public bool Alive(string id)
        {
            var node = _engine.GetNode(id);
            return node != null && !node.Pruned;
        }

        /// <summary>True when an active link of the given type connects the two nodes in either direction.</summary>
        public bool HasLink(string a, string b, MindLinkType? type = null)
        {
            foreach (var link in _engine.Links.Values)
            {
                if (!link.Active) continue;
                if (type.HasValue && link.Type != type.Value) continue;
                if ((link.FromId == a && link.ToId == b) || (link.FromId == b && link.ToId == a)) return true;
            }
            return false;
        }

        public int CountActiveLinks(string nodeId, MindLinkType? type = null)
        {
            var count = 0;
            foreach (var link in _engine.Links.Values)
            {
                if (!link.Active) continue;
                if (type.HasValue && link.Type != type.Value) continue;
                if (link.FromId == nodeId || link.ToId == nodeId) count++;
            }
            return count;
        }
    }
}
