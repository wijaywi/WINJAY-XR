using System;
using System.Linq;
using Winjay.Core.Mind;
using Xunit;

namespace Winjay.Tests
{
    public class MindEngineTests
    {
        private static MindResult AddNode(MindEngine e, string id, string label, string parent = null,
            MindNodeKind kind = MindNodeKind.Idea, MindContributor contributor = MindContributor.Explorer)
        {
            return e.Apply(new AddMindNode
            {
                Actor = contributor.ToString(), Id = id, Label = label,
                Kind = kind, Contributor = contributor, ParentId = parent
            });
        }

        private static MindResult Link(MindEngine e, string id, string from, string to, MindLinkType type,
            MindContributor originator, double weight = 1.0, string detail = null)
        {
            return e.Apply(new AddMindLink
            {
                Actor = originator.ToString(), Id = id, FromId = from, ToId = to,
                Type = type, Originator = originator, Weight = weight, Detail = detail
            });
        }

        /// <summary>Mirrors the demo script with direct actions — the determinism baseline.</summary>
        private static void RunFullScript(MindEngine e)
        {
            Assert.True(AddNode(e, "VAC", "3-Day Vacation", null, MindNodeKind.Root).Ok);
            Assert.True(AddNode(e, "TRN", "Transport", "VAC").Ok);
            Assert.True(AddNode(e, "BUD", "Budget", "VAC").Ok);
            Assert.True(AddNode(e, "ACT", "Activities", "VAC").Ok);
            Assert.True(AddNode(e, "ARR", "Late arrival Day 1", "TRN").Ok);
            Assert.True(AddNode(e, "TREK", "Sunrise trekking 4 AM", "ACT").Ok);
            Assert.True(AddNode(e, "STREET", "Street food stall", null).Ok);
            Assert.True(Link(e, "L01", "TRN", "ARR", MindLinkType.Branch, MindContributor.Explorer).Ok);
            Assert.True(Link(e, "L02", "ACT", "TREK", MindLinkType.Branch, MindContributor.Explorer).Ok);
            Assert.True(Link(e, "L03", "STREET", "BUD", MindLinkType.Support, MindContributor.Connector,
                detail: "local food keeps the budget alive").Ok);
            Assert.True(Link(e, "L04", "ARR", "TREK", MindLinkType.Conflict, MindContributor.Challenger,
                detail: "4 AM start collides with late arrival").Ok);
            Assert.True(Link(e, "L05", "BUD", "TREK", MindLinkType.Conflict, MindContributor.Challenger,
                detail: "trek fees exceed the limit").Ok);

            // Curator prunes the trek — the conflict disappears but the scenery branch loses support.
            Assert.True(e.Apply(new PruneMindNode { Actor = "Human", Id = "TREK" }).Ok);
            Assert.True(e.Apply(new AddMindNode
            {
                Actor = "Connector", Id = "SUNSET", Label = "Sunset at the cliff", Kind = MindNodeKind.Idea,
                Contributor = MindContributor.Connector, ParentId = "ACT", IsGhost = true
            }).Ok);
            Assert.True(e.Apply(new AcceptMindGhost { Actor = "Human", Id = "SUNSET" }).Ok);
            Assert.True(Link(e, "L06", "SUNSET", "ACT", MindLinkType.Branch, MindContributor.Connector).Ok);

            // The curator speaks — a human node with heavier links.
            Assert.True(e.Apply(new MindHumanInput
            {
                Actor = "Human", Id = "MOTION", Label = "I easily get motion sick", ParentId = "VAC"
            }).Ok);
            Assert.True(Link(e, "L07", "MOTION", "ARR", MindLinkType.Conflict, MindContributor.Human,
                detail: "long winding road").Ok);

            // Lock a plan the curator cares about, then settle phases.
            Assert.True(e.Apply(new LockMindNode { Actor = "Human", Id = "STREET" }).Ok);
            Assert.True(e.Apply(new AssignMindPhase { Actor = "Explorer", Id = "ARR", Phase = "Day 1" }).Ok);
            Assert.True(e.Apply(new AssignMindPhase { Actor = "Explorer", Id = "SUNSET", Phase = "Day 2" }).Ok);
            Assert.True(e.Apply(new AssignMindPhase { Actor = "Explorer", Id = "STREET", Phase = "Day 3" }).Ok);
        }

        [Fact]
        public void SameActionSequence_ProducesIdenticalSnapshot()
        {
            var a = new MindEngine();
            var b = new MindEngine();
            RunFullScript(a);
            RunFullScript(b);
            Assert.Equal(a.Snapshot(), b.Snapshot());
        }

        [Fact]
        public void FromJournal_RebuildsIdenticalState()
        {
            var engine = new MindEngine();
            RunFullScript(engine);

            var replayed = MindEngine.FromJournal(engine.Journal);
            Assert.Equal(engine.Snapshot(), replayed.Snapshot());
        }

        [Fact]
        public void FromJournal_RegeneratesIdenticalEventStream()
        {
            var engine = new MindEngine();
            RunFullScript(engine);

            var replayed = MindEngine.FromJournal(engine.Journal);
            var original = engine.Journal.Select(x => (x.Sequence, x.Type, x.Actor, x.NodeId, x.LinkId)).ToList();
            var rebuilt = replayed.Journal.Select(x => (x.Sequence, x.Type, x.Actor, x.NodeId, x.LinkId)).ToList();
            Assert.Equal(original, rebuilt);
        }

        [Fact]
        public void SupportRaisesConflictLowers_BranchNeutral()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "B", "Beta", "V");

            Assert.Equal(MindEngine.BaseStrength, e.GetNode("A").Strength, 12);

            Link(e, "LB", "V", "A", MindLinkType.Branch, MindContributor.Explorer);
            Assert.Equal(MindEngine.BaseStrength, e.GetNode("A").Strength, 12);

            Link(e, "LS", "A", "B", MindLinkType.Support, MindContributor.Connector);
            Assert.Equal(0.65, e.GetNode("A").Strength, 12);
            Assert.Equal(0.65, e.GetNode("B").Strength, 12);

            Link(e, "LC", "A", "V", MindLinkType.Conflict, MindContributor.Challenger);
            Assert.Equal(0.45, e.GetNode("A").Strength, 12);

            AddNode(e, "C", "Gamma", "V");
            Link(e, "LBR", "A", "C", MindLinkType.Bridge, MindContributor.Connector);
            Assert.Equal(0.50, e.GetNode("A").Strength, 12); // 0.45 + 0.05
            Assert.Equal(0.55, e.GetNode("C").Strength, 12); // 0.50 + 0.05
        }

        [Fact]
        public void HumanLinks_WeighOneAndAHalf()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "H", "Human idea", "V", MindNodeKind.Human, MindContributor.Human);
            AddNode(e, "A", "Agent idea", "V");

            Assert.Equal(MindEngine.HumanBaseStrength, e.GetNode("H").Strength, 12);

            Link(e, "LS", "H", "A", MindLinkType.Support, MindContributor.Human);
            // The human connection amplifies the machine idea: 0.50 + 0.15*1.5.
            Assert.Equal(0.725, e.GetNode("A").Strength, 12);
            // The human node itself keeps its elevated base; its link weighs normal: 0.70 + 0.15.
            Assert.Equal(0.85, e.GetNode("H").Strength, 12);
        }

        [Fact]
        public void LockedNode_FreezesStrength_UnlockRecomputes()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "B", "Beta", "V");
            Link(e, "LS", "A", "B", MindLinkType.Support, MindContributor.Connector);
            Assert.Equal(0.65, e.GetNode("A").Strength, 12);

            Assert.True(e.Apply(new LockMindNode { Actor = "Human", Id = "A" }).Ok);
            Assert.Equal("LOCKED", e.Apply(new LockMindNode { Actor = "Human", Id = "A" }).ErrorCode);

            Link(e, "LC", "A", "B", MindLinkType.Conflict, MindContributor.Challenger);
            Assert.Equal(0.65, e.GetNode("A").Strength, 12); // frozen
            Assert.Equal(0.45, e.GetNode("B").Strength, 12); // B still recomputed: 0.5+0.15-0.20

            Assert.True(e.Apply(new UnlockMindNode { Actor = "Human", Id = "A" }).Ok);
            Assert.Equal(0.45, e.GetNode("A").Strength, 12); // 0.5+0.15-0.20 on unlock
        }

        [Fact]
        public void Prune_RipplesNeighbors_AndDeactivatesLinks()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "B", "Beta", "V");
            Link(e, "LS", "A", "B", MindLinkType.Support, MindContributor.Connector);
            Assert.Equal(0.65, e.GetNode("A").Strength, 12);

            Assert.True(e.Apply(new PruneMindNode { Actor = "Human", Id = "B" }).Ok);

            Assert.True(e.GetNode("B").Pruned);
            Assert.False(e.GetLink("LS").Active);
            Assert.Equal(0.50, e.GetNode("A").Strength, 12); // ripple recomputed for real
            Assert.Contains(e.Journal, x => x.Type == MindEventType.LinkDeactivated && x.LinkId == "LS");
        }

        [Fact]
        public void PruneProtectedAndLockedNodes_AreRejected()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");

            Assert.Equal("ROOT_PROTECTED", e.Apply(new PruneMindNode { Actor = "Human", Id = "V" }).ErrorCode);

            e.Apply(new LockMindNode { Actor = "Human", Id = "A" });
            Assert.Equal("LOCKED", e.Apply(new PruneMindNode { Actor = "Human", Id = "A" }).ErrorCode);

            e.Apply(new UnlockMindNode { Actor = "Human", Id = "A" });
            Assert.True(e.Apply(new PruneMindNode { Actor = "Human", Id = "A" }).Ok);
            Assert.Equal("ALREADY_PRUNED", e.Apply(new PruneMindNode { Actor = "Human", Id = "A" }).ErrorCode);
        }

        [Fact]
        public void Merge_RewiresLinksAndReplaysIdentically()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "B", "Beta", "V");
            AddNode(e, "C", "Gamma", "V");
            Link(e, "LS", "A", "C", MindLinkType.Support, MindContributor.Connector);

            Assert.True(e.Apply(new MergeMindNodes { Actor = "Human", FromId = "A", IntoId = "B" }).Ok);

            Assert.True(e.GetNode("A").Pruned);
            var rewired = e.GetLink("LS");
            Assert.True(rewired.Active);
            Assert.Equal("B", rewired.FromId);
            Assert.Equal("C", rewired.ToId);
            Assert.Contains(e.Journal, x => x.Type == MindEventType.LinkRewired && x.LinkId == "LS");

            var replayed = MindEngine.FromJournal(e.Journal);
            Assert.Equal(e.Snapshot(), replayed.Snapshot());
        }

        [Fact]
        public void Merge_RejectsLockedEndpointsAndRoot()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "B", "Beta", "V");

            e.Apply(new LockMindNode { Actor = "Human", Id = "A" });
            Assert.Equal("LOCKED", e.Apply(new MergeMindNodes { Actor = "Human", FromId = "A", IntoId = "B" }).ErrorCode);
            e.Apply(new UnlockMindNode { Actor = "Human", Id = "A" });

            Assert.Equal("ROOT_PROTECTED", e.Apply(new MergeMindNodes { Actor = "Human", FromId = "V", IntoId = "B" }).ErrorCode);
            Assert.Equal("SAME_NODE", e.Apply(new MergeMindNodes { Actor = "Human", FromId = "A", IntoId = "A" }).ErrorCode);
        }

        [Fact]
        public void Ghost_MustBeAcceptedBeforeLinking_AndSuggestsAtLowStrength()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "ACT", "Activities", "V");

            Assert.True(e.Apply(new AddMindNode
            {
                Actor = "Connector", Id = "SUNSET", Label = "Sunset at the cliff",
                Contributor = MindContributor.Connector, ParentId = "ACT", IsGhost = true
            }).Ok);

            Assert.True(e.GetNode("SUNSET").Ghost);
            Assert.Equal(MindEngine.GhostStrength, e.GetNode("SUNSET").Strength, 12);
            Assert.Contains(e.Journal, x => x.Type == MindEventType.GhostSuggested);

            Assert.False(Link(e, "L1", "SUNSET", "ACT", MindLinkType.Branch, MindContributor.Connector).Ok);
            Assert.Equal("NOT_GHOST", Link(e, "L1", "SUNSET", "ACT", MindLinkType.Branch, MindContributor.Connector).ErrorCode);

            Assert.True(e.Apply(new AcceptMindGhost { Actor = "Human", Id = "SUNSET" }).Ok);
            Assert.False(e.GetNode("SUNSET").Ghost);
            Assert.Equal(MindEngine.BaseStrength, e.GetNode("SUNSET").Strength, 12); // ripple after accept
            Assert.True(Link(e, "L1", "SUNSET", "ACT", MindLinkType.Branch, MindContributor.Connector).Ok);

            // Reject path on a second ghost.
            Assert.True(e.Apply(new AddMindNode
            {
                Actor = "Connector", Id = "SPA", Label = "Spa morning", Contributor = MindContributor.Connector,
                ParentId = "ACT", IsGhost = true
            }).Ok);
            Assert.True(e.Apply(new RejectMindGhost { Actor = "Human", Id = "SPA" }).Ok);
            Assert.True(e.GetNode("SPA").Pruned);
        }

        [Fact]
        public void Validation_RejectsInvalidActions()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");

            Assert.Equal("DUPLICATE_ID", AddNode(e, "A", "Another").ErrorCode);
            Assert.Equal("UNKNOWN_PARENT", AddNode(e, "X", "X", "MISSING").ErrorCode);
            Assert.Equal("EMPTY_LABEL", AddNode(e, "Y", "  ").ErrorCode);
            Assert.Equal("UNKNOWN_NODE", Link(e, "L1", "A", "MISSING", MindLinkType.Support, MindContributor.Connector).ErrorCode);
            Assert.Equal("SAME_NODE", Link(e, "L1", "A", "A", MindLinkType.Support, MindContributor.Connector).ErrorCode);

            // Rejected actions never mutate state.
            var before = e.Snapshot();
            AddNode(e, "A", "Another");
            Link(e, "L1", "A", "MISSING", MindLinkType.Support, MindContributor.Connector);
            Assert.Equal(before, e.Snapshot());
        }

        [Fact]
        public void Strength_IsClampedToBounds()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            AddNode(e, "A", "Alpha", "V");
            AddNode(e, "H", "Human", "V", MindNodeKind.Human, MindContributor.Human);

            for (var i = 0; i < 6; i++)
            {
                AddNode(e, "N" + i, "Node " + i, "V");
                Assert.True(Link(e, "LS" + i, "H", "N" + i, MindLinkType.Support, MindContributor.Human).Ok);
            }
            Assert.Equal(0.725, e.GetNode("N0").Strength, 12);
            Assert.Equal(1.0, e.GetNode("H").Strength, 12); // ceiling clamp: 0.70 + 6*0.225

            for (var i = 0; i < 10; i++)
            {
                AddNode(e, "C" + i, "Conflict " + i, "V");
                Assert.True(Link(e, "LC" + i, "A", "C" + i, MindLinkType.Conflict, MindContributor.Challenger).Ok);
            }
            Assert.Equal(0.05, e.GetNode("A").Strength, 12); // clamped at floor
        }

        [Fact]
        public void HumanInput_CreatesWeightedNode_AndReplays()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);

            Assert.True(e.Apply(new MindHumanInput
            {
                Actor = "Human", Id = "MOTION", Label = "I easily get motion sick", ParentId = "V"
            }).Ok);

            var node = e.GetNode("MOTION");
            Assert.Equal(MindNodeKind.Human, node.Kind);
            Assert.Equal(MindContributor.Human, node.Contributor);
            Assert.Equal(MindEngine.HumanBaseStrength, node.Strength, 12);
            Assert.Single(e.Journal, x => x.Type == MindEventType.HumanInput && x.NodeId == "MOTION");

            var replayed = MindEngine.FromJournal(e.Journal);
            Assert.Equal(e.Snapshot(), replayed.Snapshot());
        }

        [Fact]
        public void AssignPhase_AppearsInSnapshot_AndReplays()
        {
            var e = new MindEngine();
            AddNode(e, "V", "Root", null, MindNodeKind.Root);
            Assert.True(e.Apply(new AssignMindPhase { Actor = "Explorer", Id = "V", Phase = "Day 2" }).Ok);
            Assert.Equal("Day 2", e.GetNode("V").Phase);

            var replayed = MindEngine.FromJournal(e.Journal);
            Assert.Equal(e.Snapshot(), replayed.Snapshot());
        }
    }
}
