using System.Linq;
using Winjay.Agents.Mind;
using Winjay.Core.Mind;
using Xunit;

namespace Winjay.Tests
{
    public class MindScenarioTests
    {
        [Fact]
        public void Story_SettlesWithoutHittingTickLimit()
        {
            var story = DemoMindScenario.CreateStory();
            var result = story.RunUntilSettled();

            Assert.True(result.Settled);
            Assert.Equal("agents are at rest and no curator step is pending", result.SettledReason);
            Assert.True(result.Ticks < 200);
        }

        [Fact]
        public void AgentsAlone_RestWaitingForTheCurator()
        {
            var runner = DemoMindScenario.CreateRunner();
            var result = runner.Run();

            Assert.True(result.Settled);
            var engine = runner.Engine;

            // Growth stops at the curator step: no prune, no ghost, no voice, no settle.
            Assert.False(engine.GetNode("TREK").Pruned);
            Assert.Null(engine.GetNode("SUNSET"));
            Assert.Null(engine.GetNode("MOTION"));
            Assert.Null(engine.GetNode("TRAIN"));
            Assert.Null(engine.GetNode("ARR").Phase);

            // All five branches plus their leaves are on the map.
            Assert.Equal(15, engine.Nodes.Count);
        }

        [Fact]
        public void Story_ProducesEveryDemoMoment()
        {
            var story = DemoMindScenario.CreateStory();
            story.RunUntilSettled();
            var engine = story.Engine;

            // Prune: the trek is gone, its links deactivated.
            Assert.True(engine.GetNode("TREK").Pruned);
            Assert.False(engine.GetLink("C_TREK_SCN").Active);
            Assert.False(engine.GetLink("X_ARR_TREK").Active);

            // Ripple: the scenery idea dipped and recovered once the ghost was accepted.
            var scn = engine.GetNode("SCN");
            Assert.Equal(MindEngine.BaseStrength + MindEngine.SupportWeight, scn.Strength, 12);
            var sun = engine.GetNode("SUNSET");
            Assert.NotNull(sun);
            Assert.False(sun.Ghost);
            Assert.False(sun.Pruned);

            // Lock: the beach day is frozen gold.
            var beach = engine.GetNode("BEACH");
            Assert.True(beach.Locked);

            // Voice: the human constraint weighs 1.5x — the road trip drops hard.
            Assert.False(engine.GetNode("MOTION").Locked);
            Assert.Equal(MindContributor.Human, engine.GetNode("MOTION").Contributor);
            Assert.Equal(0.20, engine.GetNode("RIDE").Strength, 12); // 0.50 - 0.20*1.5
            Assert.Equal(0.50, engine.GetNode("MOTION").Strength, 12); // 0.70 - 0.20 (own link weighs normal)

            // Reaction: an alternative branch grew around the collision.
            var train = engine.GetNode("TRAIN");
            Assert.NotNull(train);
            Assert.True(engine.GetLink("E_TRAIN").Active);

            // Settlement: three light paths.
            Assert.Equal("Day 1", engine.GetNode("ARR").Phase);
            Assert.Equal("Day 1", train.Phase);
            Assert.Equal("Day 2", beach.Phase);
            Assert.Equal("Day 2", sun.Phase);
            Assert.Equal("Day 3", engine.GetNode("STREET").Phase);
            Assert.Equal("Day 3", engine.GetNode("HOMESTAY").Phase);
        }

        [Fact]
        public void Story_GhostAppearsOnlyAfterPrune_AndIsAcceptedAfter()
        {
            var story = DemoMindScenario.CreateStory();
            story.RunUntilSettled();

            var journal = story.Engine.Journal;
            var pruneIndex = journal.ToList().FindIndex(e => e.Type == MindEventType.NodePruned && e.NodeId == "TREK");
            var ghostIndex = journal.ToList().FindIndex(e => e.Type == MindEventType.GhostSuggested && e.NodeId == "SUNSET");
            var acceptIndex = journal.ToList().FindIndex(e => e.Type == MindEventType.GhostAccepted && e.NodeId == "SUNSET");

            Assert.True(pruneIndex >= 0, "prune must be in the journal");
            Assert.True(ghostIndex > pruneIndex, "the ghost suggestion is a reaction, it must come after the prune");
            Assert.True(acceptIndex > ghostIndex, "acceptance comes after the suggestion");
        }

        [Fact]
        public void Story_StrengthRipple_IsRealArithmetic()
        {
            var story = DemoMindScenario.CreateStory();
            story.RunUntilSettled();

            var changes = story.Engine.Journal
                .Where(e => e.Type == MindEventType.StrengthChanged && e.NodeId == "SCN")
                .ToList();

            // Dip when the trek dies, recovery when the sunset link lands.
            Assert.Contains(changes, c => c.OldStrength == 0.65 && c.NewStrength == 0.50);
            Assert.Contains(changes, c => c.OldStrength == 0.50 && c.NewStrength == 0.65);
        }

        [Fact]
        public void Story_IsDeterministic_AndReplaysFromJournal()
        {
            var a = DemoMindScenario.CreateStory();
            a.RunUntilSettled();
            var b = DemoMindScenario.CreateStory();
            b.RunUntilSettled();

            Assert.Equal(a.Engine.Snapshot(), b.Engine.Snapshot());

            var replayed = MindEngine.FromJournal(a.Engine.Journal);
            Assert.Equal(a.Engine.Snapshot(), replayed.Snapshot());
        }

        [Fact]
        public void Story_MapStaysWithinBudget()
        {
            var story = DemoMindScenario.CreateStory();
            story.RunUntilSettled();
            var engine = story.Engine;

            var alive = engine.Nodes.Values.Count(n => !n.Pruned);
            Assert.True(alive <= 25, "the map must stay readable: " + alive + " nodes alive");
        }

        [Fact]
        public void CuratorScript_EachStepFiresExactlyOnce()
        {
            var story = DemoMindScenario.CreateStory();
            story.RunUntilSettled();

            var humanSteps = story.Steps.Where(s => s.Actor == "Human").ToList();
            Assert.Equal(4, humanSteps.Count);
            Assert.All(humanSteps, s => Assert.True(s.Result.Ok, s.Note));
        }
    }
}
