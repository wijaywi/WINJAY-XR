using System.Linq;
using Winjay.Agents;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class ScenarioTests
    {
        private static (WinjayEventBus Bus, WinjayStateEngine Engine) NewConflictWorld(out ScenarioRunner runner)
        {
            var (bus, engine) = TestHarness.NewEngine();
            runner = new ScenarioRunner(engine, bus,
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81),
                    new EvidenceScriptItem("E002", "Evidence B", 0.70)),
                new MockAnalyzer("MockAnalyzer", "H001", false, "E001", "E002"),
                new MockFalsifier("MockFalsifier", "H001",
                    new EvidenceScriptItem("E003", "Contradicting observation", 0.90)),
                new MockDecision("MockDecision", "H001"));
            return (bus, engine);
        }

        private static (WinjayEventBus Bus, WinjayStateEngine Engine) NewResolveWorld(out ScenarioRunner runner)
        {
            var (bus, engine) = TestHarness.NewEngine();
            runner = new ScenarioRunner(engine, bus,
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81)),
                new MockAnalyzer("MockAnalyzer", "H001", true, "E001"),
                new MockFalsifier("MockFalsifier", "H001", contradiction: null),
                new MockDecision("MockDecision", "H001"));
            return (bus, engine);
        }

        [Fact]
        public void DemoScenario01_Conflict_EndsFailedWithRejectDecision()
        {
            var (bus, engine) = NewConflictWorld(out var runner);

            var result = runner.Run();

            Assert.True(result.Completed, "scenario must reach fixpoint before MaxTicks");
            Assert.True(result.Steps.Count > 0);

            var h = engine.Get<Hypothesis>("H001");
            Assert.NotNull(h);
            Assert.Equal(HypothesisState.Failed, h.State);

            var decision = engine.Entities.Values.OfType<Decision>().Single();
            Assert.Equal("REJECT_HYPOTHESIS", decision.Verdict);

            Assert.NotNull(engine.Get<Evidence>("E001"));
            Assert.NotNull(engine.Get<Evidence>("E002"));
            Assert.NotNull(engine.Get<Evidence>("E003"));

            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.ConflictDetected);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.FalsificationStarted);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.DecisionCommitted);
        }

        [Fact]
        public void DemoScenario01_NoProposalIsRejected()
        {
            var (_, _) = NewConflictWorld(out var runner);

            var result = runner.Run();

            Assert.All(result.Steps, s => Assert.True(s.Result.Ok,
                $"proposal {s.Proposal.Kind} from {s.Agent} at tick {s.Tick} was rejected: {s.Result.ErrorCode} — {s.Result.Error}"));
        }

        [Fact]
        public void DemoScenario01_ContradictingEvidence_TrackedOnHypothesis()
        {
            var (_, engine) = NewConflictWorld(out var runner);
            runner.Run();

            var h = engine.Get<Hypothesis>("H001");
            Assert.Equal(new[] { "E003" }, h.ContradictingEvidence);

            var conflict = engine.Entities.Values.OfType<Conflict>().Single();
            Assert.Equal("H001", conflict.HypothesisId);
            Assert.Equal("E003", conflict.EvidenceId);
        }

        [Fact]
        public void ScenarioResolve_EndsResolvedWithAcceptDecision()
        {
            var (bus, engine) = NewResolveWorld(out var runner);

            var result = runner.Run();

            Assert.True(result.Completed);
            Assert.Equal(HypothesisState.Resolved, engine.Get<Hypothesis>("H001").State);

            var decision = engine.Entities.Values.OfType<Decision>().Single();
            Assert.Equal("ACCEPT_HYPOTHESIS", decision.Verdict);

            Assert.DoesNotContain(bus.Journal, e => e.Type == WinjayEventType.ConflictDetected);
        }

        [Fact]
        public void Runner_ReachesFixpoint_NoPendingProposals()
        {
            var (_, _) = NewConflictWorld(out var runner);
            runner.Run();

            Assert.False(runner.Tick(), "after the run, another tick must produce no proposals");
        }

        [Fact]
        public void Scenario_IsDeterministic_TwoRunsProduceSameSteps()
        {
            var (busA, engineA) = NewConflictWorld(out var runnerA);
            var (busB, engineB) = NewConflictWorld(out var runnerB);

            var resultA = runnerA.Run();
            var resultB = runnerB.Run();

            Assert.Equal(resultA.Ticks, resultB.Ticks);
            Assert.Equal(resultA.Steps.Count, resultB.Steps.Count);

            for (var i = 0; i < resultA.Steps.Count; i++)
            {
                Assert.Equal(resultA.Steps[i].Agent, resultB.Steps[i].Agent);
                Assert.Equal(resultA.Steps[i].Proposal.Kind, resultB.Steps[i].Proposal.Kind);
            }

            var hA = engineA.Get<Hypothesis>("H001");
            var hB = engineB.Get<Hypothesis>("H001");
            Assert.Equal(hA.State, hB.State);
            Assert.Equal(hA.Confidence, hB.Confidence, 12);
        }

        [Fact]
        public void Scenario_WorldViewHasNoMutationSurface()
        {
            // Agents receive only WorldView; it must expose read-only state.
            // Properties without setters, methods limited to reads.
            foreach (var property in typeof(WorldView).GetProperties())
            {
                Assert.Null(property.GetSetMethod());
            }
        }
    }
}
