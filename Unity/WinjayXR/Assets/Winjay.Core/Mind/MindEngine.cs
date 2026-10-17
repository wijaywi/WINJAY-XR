using System;
using System.Collections.Generic;
using System.Linq;

namespace Winjay.Core.Mind
{
    /// <summary>
    /// Result of applying an action through the MindEngine — always
    /// PROPOSE → VALIDATE → COMMIT. Rejected actions never mutate state.
    /// </summary>
    public sealed class MindResult
    {
        public static class Codes
        {
            public const string Ok = "OK";
            public const string DuplicateId = "DUPLICATE_ID";
            public const string UnknownNode = "UNKNOWN_NODE";
            public const string UnknownParent = "UNKNOWN_PARENT";
            public const string AlreadyPruned = "ALREADY_PRUNED";
            public const string Locked = "LOCKED";
            public const string RootProtected = "ROOT_PROTECTED";
            public const string NotGhost = "NOT_GHOST";
            public const string SameNode = "SAME_NODE";
            public const string EmptyLabel = "EMPTY_LABEL";
        }

        public bool Ok { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string Message { get; internal set; }

        public static MindResult Success()
        {
            return new MindResult { Ok = true, ErrorCode = Codes.Ok };
        }

        public static MindResult Fail(string code, string message)
        {
            return new MindResult { Ok = false, ErrorCode = code, Message = message };
        }
    }

    /// <summary>
    /// Everything an agent or the human curator can ask the engine to do.
    /// The engine validates and commits; nobody mutates the map directly.
    /// </summary>
    public interface IMindAction
    {
        string Actor { get; }
    }

    public sealed class AddMindNode : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
        public string Label { get; set; }
        public MindNodeKind Kind { get; set; } = MindNodeKind.Idea;
        public MindContributor Contributor { get; set; } = MindContributor.Explorer;
        public string ParentId { get; set; }
        public MindVec3 Position { get; set; }
        public bool IsGhost { get; set; }
    }

    public sealed class AddMindLink : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }
        public MindLinkType Type { get; set; }
        public MindContributor Originator { get; set; } = MindContributor.Connector;
        public double Weight { get; set; } = 1.0;
        public string Detail { get; set; }
    }

    public sealed class PruneMindNode : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
    }

    public sealed class LockMindNode : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
    }

    public sealed class UnlockMindNode : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
    }

    /// <summary>The curator speaks — the sentence becomes a node with human weight.</summary>
    public sealed class MindHumanInput : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
        public string Label { get; set; }
        public string ParentId { get; set; }
        public MindVec3 Position { get; set; }
    }

    public sealed class MergeMindNodes : IMindAction
    {
        public string Actor { get; set; }
        /// <summary>Node that gets absorbed and pruned.</summary>
        public string FromId { get; set; }
        /// <summary>Node that survives and re-points incoming links.</summary>
        public string IntoId { get; set; }
    }

    public sealed class AcceptMindGhost : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
    }

    public sealed class RejectMindGhost : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
    }

    public sealed class AssignMindPhase : IMindAction
    {
        public string Actor { get; set; }
        public string Id { get; set; }
        public string Phase { get; set; }
    }

    /// <summary>
    /// The deterministic truth layer of the living mind map.
    ///
    /// Strength is a pure function of the map: every active link contributes
    /// (Support +, Conflict −, Bridge slightly +), human-authored nodes start
    /// higher and their links weigh more, locked nodes freeze their strength,
    /// pruned nodes drop out. After every commit the engine recomputes the
    /// affected strengths — ripples are real arithmetic, never animation.
    ///
    /// The journal records every commit with full payload, so the whole map
    /// can be rebuilt from the log (rewind / replay / determinism tests).
    /// </summary>
    public sealed class MindEngine
    {
        public const double BaseStrength = 0.50;
        public const double HumanBaseStrength = 0.70;
        public const double SupportWeight = 0.15;
        public const double ConflictWeight = -0.20;
        public const double BridgeWeight = 0.05;
        public const double HumanLinkMultiplier = 1.5;
        public const double GhostStrength = 0.25;

        private readonly Dictionary<string, MindNode> _nodes = new Dictionary<string, MindNode>();
        private readonly Dictionary<string, MindLink> _links = new Dictionary<string, MindLink>();
        private readonly List<MindEvent> _journal = new List<MindEvent>();
        private long _sequence;

        public IReadOnlyDictionary<string, MindNode> Nodes => _nodes;
        public IReadOnlyDictionary<string, MindLink> Links => _links;
        public IReadOnlyList<MindEvent> Journal => _journal;

        public MindNode Root { get; private set; }

        // ------------------------------------------------------------- actions

        public MindResult Apply(IMindAction action)
        {
            if (action == null) return MindResult.Fail("NULL_ACTION", "Action is null.");

            switch (action)
            {
                case AddMindNode a: return AddNode(a);
                case AddMindLink a: return AddLink(a);
                case PruneMindNode a: return PruneNode(a);
                case LockMindNode a: return LockNode(a.Id, a.Actor, true);
                case UnlockMindNode a: return LockNode(a.Id, a.Actor, false);
                case MindHumanInput a: return HumanInput(a);
                case MergeMindNodes a: return MergeNodes(a);
                case AcceptMindGhost a: return ResolveGhost(a, true);
                case RejectMindGhost a: return ResolveGhost(a, false);
                case AssignMindPhase a: return AssignPhase(a);
                default: return MindResult.Fail("UNKNOWN_ACTION", "Unknown action " + action.GetType().Name);
            }
        }

        private MindResult AddNode(AddMindNode a)
        {
            if (string.IsNullOrWhiteSpace(a.Id)) return MindResult.Fail(MindResult.Codes.EmptyLabel, "Node id missing.");
            if (string.IsNullOrWhiteSpace(a.Label)) return MindResult.Fail(MindResult.Codes.EmptyLabel, "Node label missing.");
            if (_nodes.ContainsKey(a.Id)) return MindResult.Fail(MindResult.Codes.DuplicateId, "Node '" + a.Id + "' already exists.");
            if (a.ParentId != null && !_nodes.ContainsKey(a.ParentId))
                return MindResult.Fail(MindResult.Codes.UnknownParent, "Parent '" + a.ParentId + "' does not exist.");

            var node = new MindNode
            {
                Id = a.Id,
                Label = a.Label,
                Kind = a.Kind,
                Contributor = a.Contributor,
                ParentId = a.ParentId,
                Position = a.Position,
                Ghost = a.IsGhost,
                Locked = false,
                Pruned = false,
                Strength = a.IsGhost ? GhostStrength : (a.Contributor == MindContributor.Human ? HumanBaseStrength : BaseStrength)
            };
            node.Record(a.Actor + ":" + a.Kind);
            _nodes[a.Id] = node;
            if (Root == null && a.Kind == MindNodeKind.Root) Root = node;

            var evt = NewEvent(a.IsGhost ? MindEventType.GhostSuggested : MindEventType.NodeAdded, a.Actor);
            evt.NodeId = a.Id; evt.Label = a.Label; evt.Kind = a.Kind;
            evt.Contributor = a.Contributor; evt.ParentId = a.ParentId;
            evt.Position = a.Position; evt.Ghost = a.IsGhost;

            if (a.IsGhost) return MindResult.Success();
            return Ripple(node);
        }

        private MindResult AddLink(AddMindLink a)
        {
            if (string.IsNullOrWhiteSpace(a.Id)) return MindResult.Fail(MindResult.Codes.EmptyLabel, "Link id missing.");
            if (_links.ContainsKey(a.Id)) return MindResult.Fail(MindResult.Codes.DuplicateId, "Link '" + a.Id + "' already exists.");
            if (a.FromId == a.ToId) return MindResult.Fail(MindResult.Codes.SameNode, "A link needs two different nodes.");
            if (!_nodes.TryGetValue(a.FromId, out var from) || !_nodes.TryGetValue(a.ToId, out var to))
                return MindResult.Fail(MindResult.Codes.UnknownNode, "Link endpoint does not exist.");
            if (from.Pruned || to.Pruned) return MindResult.Fail(MindResult.Codes.UnknownNode, "Cannot link a pruned node.");
            if (from.Ghost || to.Ghost) return MindResult.Fail(MindResult.Codes.NotGhost, "Cannot link a ghost node before it is accepted.");

            var link = new MindLink
            {
                Id = a.Id,
                FromId = a.FromId,
                ToId = a.ToId,
                Type = a.Type,
                Originator = a.Originator,
                Weight = a.Weight,
                Detail = a.Detail,
                Active = true
            };
            _links[a.Id] = link;

            var evt = NewEvent(MindEventType.LinkCreated, a.Actor);
            evt.LinkId = a.Id; evt.FromId = a.FromId; evt.ToId = a.ToId;
            evt.LinkType = a.Type; evt.LinkOriginator = a.Originator;
            evt.Weight = a.Weight; evt.Detail = a.Detail;

            return Ripple(from, to);
        }

        private MindResult PruneNode(PruneMindNode a)
        {
            if (!_nodes.TryGetValue(a.Id, out var node)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + a.Id + "' does not exist.");
            if (node.Pruned) return MindResult.Fail(MindResult.Codes.AlreadyPruned, "Node '" + a.Id + "' is already pruned.");
            if (node.Locked) return MindResult.Fail(MindResult.Codes.Locked, "Locked node '" + a.Id + "' cannot be pruned. Unlock first.");
            if (node == Root) return MindResult.Fail(MindResult.Codes.RootProtected, "The root node cannot be pruned.");

            node.Pruned = true;
            node.Record(a.Actor + ":pruned");
            var evt = NewEvent(MindEventType.NodePruned, a.Actor);
            evt.NodeId = a.Id;

            // Deactivate every link touching the pruned node; the survivors ripple.
            var affected = new HashSet<MindNode> { node };
            foreach (var link in _links.Values.Where(l => l.Active && (l.FromId == a.Id || l.ToId == a.Id)).ToList())
            {
                link.Active = false;
                var linkEvt = NewEvent(MindEventType.LinkDeactivated, a.Actor);
                linkEvt.LinkId = link.Id; linkEvt.FromId = link.FromId; linkEvt.ToId = link.ToId;
                if (_nodes.TryGetValue(link.OtherEnd(a.Id), out var other)) affected.Add(other);
            }
            return Ripple(affected);
        }

        private MindResult LockNode(string id, string actor, bool locked)
        {
            if (!_nodes.TryGetValue(id, out var node)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + id + "' does not exist.");
            if (node.Pruned) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + id + "' is pruned.");
            if (node.Locked == locked) return MindResult.Fail(locked ? MindResult.Codes.Locked : "NOT_LOCKED", locked ? "Already locked." : "Already unlocked.");

            node.Locked = locked;
            node.Record(actor + (locked ? ":locked" : ":unlocked"));
            var evt = NewEvent(locked ? MindEventType.NodeLocked : MindEventType.NodeUnlocked, actor);
            evt.NodeId = id;
            // Unlocking rejoins the arithmetic: refresh the frozen value.
            return locked ? MindResult.Success() : Ripple(node);
        }

        private MindResult HumanInput(MindHumanInput a)
        {
            if (string.IsNullOrWhiteSpace(a.Label)) return MindResult.Fail(MindResult.Codes.EmptyLabel, "Spoken text missing.");
            if (string.IsNullOrWhiteSpace(a.Id)) return MindResult.Fail(MindResult.Codes.EmptyLabel, "Node id missing.");
            if (_nodes.ContainsKey(a.Id)) return MindResult.Fail(MindResult.Codes.DuplicateId, "Node '" + a.Id + "' already exists.");
            if (a.ParentId != null && !_nodes.ContainsKey(a.ParentId))
                return MindResult.Fail(MindResult.Codes.UnknownParent, "Parent '" + a.ParentId + "' does not exist.");

            var node = new MindNode
            {
                Id = a.Id,
                Label = a.Label,
                Kind = MindNodeKind.Human,
                Contributor = MindContributor.Human,
                ParentId = a.ParentId,
                Position = a.Position,
                Ghost = false,
                Locked = false,
                Pruned = false,
                Strength = HumanBaseStrength
            };
            node.Record(a.Actor + ":human");
            _nodes[a.Id] = node;

            var evt = NewEvent(MindEventType.HumanInput, a.Actor);
            evt.NodeId = a.Id; evt.Label = a.Label; evt.Kind = node.Kind; evt.Contributor = node.Contributor;
            evt.ParentId = a.ParentId; evt.Position = a.Position;
            return Ripple(node);
        }

        private MindResult MergeNodes(MergeMindNodes a)
        {
            if (!_nodes.TryGetValue(a.FromId, out var from)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + a.FromId + "' does not exist.");
            if (!_nodes.TryGetValue(a.IntoId, out var into)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + a.IntoId + "' does not exist.");
            if (a.FromId == a.IntoId) return MindResult.Fail(MindResult.Codes.SameNode, "Cannot merge a node into itself.");
            if (from.Locked || into.Locked) return MindResult.Fail(MindResult.Codes.Locked, "Locked nodes cannot be merged.");
            if (from.Pruned || into.Pruned) return MindResult.Fail(MindResult.Codes.UnknownNode, "Pruned nodes cannot be merged.");
            if (from == Root || into == Root) return MindResult.Fail(MindResult.Codes.RootProtected, "The root node cannot be merged.");

            from.Pruned = true;
            from.Record(a.Actor + ":merged into " + into.Id);
            var evt = NewEvent(MindEventType.NodeMerged, a.Actor);
            evt.NodeId = a.FromId; evt.MergedIntoId = a.IntoId;

            var affected = new HashSet<MindNode> { from, into };
            // Re-point every active link so the survivor inherits the relations.
            foreach (var link in _links.Values.Where(l => l.Active && (l.FromId == a.FromId || l.ToId == a.FromId)).ToList())
            {
                if (link.FromId == a.FromId) link.FromId = a.IntoId;
                if (link.ToId == a.FromId) link.ToId = a.IntoId;
                var rewire = NewEvent(MindEventType.LinkRewired, a.Actor);
                rewire.LinkId = link.Id; rewire.FromId = link.FromId; rewire.ToId = link.ToId;
                rewire.LinkType = link.Type; rewire.LinkOriginator = link.Originator; rewire.Weight = link.Weight;
                rewire.Detail = "rewired: merged " + a.FromId + " into " + a.IntoId;
            }
            return Ripple(affected);
        }

        private MindResult ResolveGhost(IMindAction a, bool accept)
        {
            var id = (a is AcceptMindGhost acc) ? acc.Id : ((RejectMindGhost)a).Id;
            if (!_nodes.TryGetValue(id, out var node)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + id + "' does not exist.");
            if (!node.Ghost) return MindResult.Fail(MindResult.Codes.NotGhost, "Node '" + id + "' is not a ghost suggestion.");
            if (node.Pruned) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + id + "' is pruned.");

            var evt = NewEvent(accept ? MindEventType.GhostAccepted : MindEventType.GhostRejected, a.Actor);
            evt.NodeId = id;
            if (accept)
            {
                node.Ghost = false;
                node.Record(a.Actor + ":accepted");
                return Ripple(node);
            }
            node.Pruned = true;
            node.Record(a.Actor + ":rejected");
            return Ripple(node);
        }

        private MindResult AssignPhase(AssignMindPhase a)
        {
            if (!_nodes.TryGetValue(a.Id, out var node)) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + a.Id + "' does not exist.");
            if (node.Pruned) return MindResult.Fail(MindResult.Codes.UnknownNode, "Node '" + a.Id + "' is pruned.");
            node.Phase = a.Phase;
            var evt = NewEvent(MindEventType.PhaseAssigned, a.Actor);
            evt.NodeId = a.Id; evt.Phase = a.Phase;
            return MindResult.Success();
        }

        // ------------------------------------------------------------- strength

        /// <summary>
        /// Recomputes strengths of every affected node and everything linked
        /// to them (strength is a pure function of the neighbourhood, so a new
        /// link ripples outward). Locked nodes keep their frozen value.
        /// </summary>
        private MindResult Ripple(params MindNode[] seeds)
        {
            return Ripple(new HashSet<MindNode>(seeds));
        }

        private MindResult Ripple(HashSet<MindNode> seeds)
        {
            var affected = new HashSet<MindNode>(seeds);
            foreach (var node in seeds.ToList())
            {
                foreach (var link in _links.Values.Where(l => l.Active && (l.FromId == node.Id || l.ToId == node.Id)))
                {
                    if (_nodes.TryGetValue(link.OtherEnd(node.Id), out var other)) affected.Add(other);
                }
            }

            foreach (var node in affected.OrderBy(n => n.Id, StringComparer.Ordinal))
            {
                var oldStrength = node.Pruned ? 0.0 : node.Strength;
                if (node.Locked || node.Pruned || node.Ghost) continue;
                var newStrength = ComputeStrength(node);
                node.Strength = newStrength;
                if (Math.Abs(oldStrength - newStrength) > 0.0001)
                {
                    var evt = NewEvent(MindEventType.StrengthChanged, "engine");
                    evt.NodeId = node.Id; evt.OldStrength = oldStrength; evt.NewStrength = newStrength;
                }
            }
            return MindResult.Success();
        }

        /// <summary>Pure function of the node's active links — deterministic by construction.</summary>
        public double ComputeStrength(MindNode node)
        {
            if (node.Pruned || node.Ghost) return node.Strength;
            if (node.Locked) return node.Strength;
            var strength = node.Contributor == MindContributor.Human ? HumanBaseStrength : BaseStrength;
            foreach (var link in _links.Values.Where(l => l.Active && (l.FromId == node.Id || l.ToId == node.Id)))
            {
                var weight = link.Weight;
                if (_nodes.TryGetValue(link.OtherEnd(node.Id), out var other) && other.Contributor == MindContributor.Human)
                    weight *= HumanLinkMultiplier;
                switch (link.Type)
                {
                    case MindLinkType.Support: strength += SupportWeight * weight; break;
                    case MindLinkType.Conflict: strength += ConflictWeight * weight; break;
                    case MindLinkType.Bridge: strength += BridgeWeight * weight; break;
                }
            }
            if (strength < 0.05) strength = 0.05;
            if (strength > 1.0) strength = 1.0;
            return strength;
        }

        // ------------------------------------------------------------- replay

        /// <summary>
        /// Rebuilds a fully identical engine from a journal — the basis for
        /// the rewind slider and for determinism guarantees.
        /// </summary>
        public static MindEngine FromJournal(IEnumerable<MindEvent> journal)
        {
            var engine = new MindEngine();
            foreach (var evt in journal)
            {
                var result = engine.Replay(evt);
                if (!result.Ok)
                    throw new InvalidOperationException("Replay failed at #" + evt.Sequence + " " + evt.Type + ": " + result.Message);
            }
            return engine;
        }

        private MindResult Replay(MindEvent evt)
        {
            switch (evt.Type)
            {
                case MindEventType.NodeAdded:
                case MindEventType.GhostSuggested:
                    return AddNode(new AddMindNode
                    {
                        Actor = evt.Actor, Id = evt.NodeId, Label = evt.Label,
                        Kind = evt.Kind ?? MindNodeKind.Idea, Contributor = evt.Contributor ?? MindContributor.Explorer,
                        ParentId = evt.ParentId, Position = evt.Position ?? default(MindVec3),
                        IsGhost = evt.Type == MindEventType.GhostSuggested
                    });
                case MindEventType.HumanInput:
                    return HumanInput(new MindHumanInput
                    {
                        Actor = evt.Actor, Id = evt.NodeId, Label = evt.Label,
                        ParentId = evt.ParentId, Position = evt.Position ?? default(MindVec3)
                    });
                case MindEventType.LinkCreated:
                    return AddLink(new AddMindLink
                    {
                        Actor = evt.Actor, Id = evt.LinkId, FromId = evt.FromId, ToId = evt.ToId,
                        Type = evt.LinkType ?? MindLinkType.Branch, Weight = evt.Weight ?? 1.0,
                        Originator = evt.LinkOriginator ?? MindContributor.Connector, Detail = evt.Detail
                    });
                case MindEventType.NodePruned:
                    return PruneNode(new PruneMindNode { Actor = evt.Actor, Id = evt.NodeId });
                case MindEventType.NodeLocked:
                    return LockNode(evt.NodeId, evt.Actor, true);
                case MindEventType.NodeUnlocked:
                    return LockNode(evt.NodeId, evt.Actor, false);
                case MindEventType.GhostAccepted:
                    return ResolveGhost(new AcceptMindGhost { Actor = evt.Actor, Id = evt.NodeId }, true);
                case MindEventType.GhostRejected:
                    return ResolveGhost(new RejectMindGhost { Actor = evt.Actor, Id = evt.NodeId }, false);
                case MindEventType.NodeMerged:
                    return MergeNodes(new MergeMindNodes { Actor = evt.Actor, FromId = evt.NodeId, IntoId = evt.MergedIntoId });
                case MindEventType.PhaseAssigned:
                    return AssignPhase(new AssignMindPhase { Actor = evt.Actor, Id = evt.NodeId, Phase = evt.Phase });
                case MindEventType.StrengthChanged:
                case MindEventType.LinkDeactivated:
                case MindEventType.LinkRewired:
                    // Derived events — recomputed during replay.
                    return MindResult.Success();
                default:
                    return MindResult.Fail("UNKNOWN_EVENT", "Unknown event " + evt.Type);
            }
        }

        // ------------------------------------------------------------- helpers

        public MindNode GetNode(string id)
        {
            _nodes.TryGetValue(id, out var node);
            return node;
        }

        public MindLink GetLink(string id)
        {
            _links.TryGetValue(id, out var link);
            return link;
        }

        private MindEvent NewEvent(MindEventType type, string actor)
        {
            var evt = new MindEvent(++_sequence, type, actor);
            _journal.Add(evt);
            return evt;
        }

        /// <summary>Deterministic snapshot used by tests and by renderers.</summary>
        public string Snapshot()
        {
            var nodeLines = _nodes.Values
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => n.Id + "|" + n.Label + "|" + n.Kind + "|" + n.Contributor + "|" + n.ParentId + "|" +
                             n.Position + "|" + n.Strength.ToString("0.0000") + "|" + (n.Locked ? "L" : "") +
                             (n.Pruned ? "P" : "") + (n.Ghost ? "G" : "") + "|" + (n.Phase ?? "-"));
            var linkLines = _links.Values
                .OrderBy(l => l.Id, StringComparer.Ordinal)
                .Select(l => l.Id + "|" + l.FromId + ">" + l.ToId + "|" + l.Type + "|" + l.Originator + "|" +
                             l.Weight.ToString("0.00") + "|" + (l.Active ? "on" : "off"));
            return string.Join("\n", nodeLines) + "\n---\n" + string.Join("\n", linkLines);
        }
    }
}
