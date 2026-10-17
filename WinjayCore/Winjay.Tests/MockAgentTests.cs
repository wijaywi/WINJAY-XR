using System.Linq;
using Winjay.Agents;
using Winjay.Core.Model;
using Winjay.Core.Proposals;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class MockAgentTests
    {
        private static WorldView NewView(WinjayStateEngine engine, Winjay.Core.Events.WinjayEventBus bus)
        {
            return new WorldView(engine, bus);
        }

        [Fact]
        public void MockResearcher_EmitsScriptedEvidenceInOrder()
        {
            var (bus, engine) = TestHarness.NewEngine();
            var researcher = new MockResearcher("R",
                new EvidenceScriptItem("E001", "A", 0.8),
                new EvidenceScriptItem("E002", "B", 0.7));

            var first = researcher.Step(NewView(engine, bus)).ToList();
            Assert.Equal(2, first.Count); // researcher pours its whole script in one step
            Assert.Equal("E001", ((CreateEvidenceProposal)first[0]).EvidenceId);
            Assert.Equal("E002", ((CreateEvidenceProposal)first[1]).EvidenceId);

            Assert.Empty(researcher.Step(NewView(engine, bus)));
        }

        [Fact]
        public void MockResearcher_SkipsAlreadyExistingEvidence()
        {
            var (bus, engine) = TestHarness.NewEngine();
            Assert.True(engine.Apply(new CreateEvidenceProposal("Other", "E001", "x", "Other", 0.5)).Ok);

            var researcher = new MockResearcher("R",
                new EvidenceScriptItem("E001", "A", 0.8),
                new EvidenceScriptItem("E002", "B", 0.7));

            var proposals = researcher.Step(NewView(engine, bus)).ToList();

            Assert.Single(proposals); // E001 skipped, only E002 proposed
            Assert.Equal("E002", ((CreateEvidenceProposal)proposals[0]).EvidenceId);
        }

        [Fact]
        public void MockAnalyzer_WaitsForEvidence_ThenCreatesHypothesis()
        {
            var (bus, engine) = TestHarness.NewEngine();
            var analyzer = new MockAnalyzer("A", "H001", false, "E001", "E002");

            Assert.Empty(analyzer.Step(NewView(engine, bus))); // no evidence yet

            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            Assert.Empty(analyzer.Step(NewView(engine, bus))); // still missing E002

            engine.Apply(new CreateEvidenceProposal("R", "E002", "B", "R", 0.7));

            var proposals = analyzer.Step(NewView(engine, bus)).ToList();
            var create = Assert.IsType<CreateHypothesisProposal>(Assert.Single(proposals));
            Assert.Equal("H001", create.HypothesisId);
            Assert.Contains("E001", create.Statement);
            Assert.Contains("E002", create.Statement);
        }

        [Fact]
        public void MockAnalyzer_InvestigatesThenSupports()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            var analyzer = new MockAnalyzer("A", "H001", false, "E001");

            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // create H001
            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // investigate

            Assert.Equal(HypothesisState.Investigating, engine.Get<Hypothesis>("H001").State);

            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // support E001

            var h = engine.Get<Hypothesis>("H001");
            Assert.Equal(HypothesisState.Supported, h.State);
            Assert.Contains("E001", h.SupportingEvidence);
        }

        [Fact]
        public void MockAnalyzer_StandsDown_WhenFalsifierOwnsTheState()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new CreateEvidenceProposal("R", "E002", "B", "R", 0.7));
            var analyzer = new MockAnalyzer("A", "H001", false, "E001", "E002");

            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // create H001
            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // investigate
            engine.Apply(analyzer.Step(NewView(engine, bus)).Single()); // support E001 -> SUPPORTED

            // falsifier takes over: contradict -> CONFLICTED
            engine.Apply(new CreateEvidenceProposal("F", "E003", "C", "F", 0.9));
            engine.Apply(new ContradictProposal("F", "H001", "E003"));

            // analyzer would support E002 next, but the transition is no longer legal
            var proposals = analyzer.Step(NewView(engine, bus)).ToList();
            foreach (var p in proposals)
            {
                Assert.True(engine.Apply(p).Ok, "analyzer must only propose legal transitions");
            }
            Assert.Equal(HypothesisState.Conflicted, engine.Get<Hypothesis>("H001").State);
        }

        [Fact]
        public void MockFalsifier_DrivesConflictToFailure()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new CreateHypothesisProposal("A", "H001", "h", 0.62, "E001"));
            engine.Apply(new StartInvestigationProposal("A", "H001"));

            var falsifier = new MockFalsifier("F", "H001",
                new EvidenceScriptItem("E003", "Contradiction", 0.9));

            engine.Apply(falsifier.Step(NewView(engine, bus)).Single()); // create E003
            Assert.NotNull(engine.Get<Evidence>("E003"));

            engine.Apply(falsifier.Step(NewView(engine, bus)).Single()); // contradict
            Assert.Equal(HypothesisState.Conflicted, engine.Get<Hypothesis>("H001").State);

            engine.Apply(falsifier.Step(NewView(engine, bus)).Single()); // falsify
            Assert.Equal(HypothesisState.Falsifying, engine.Get<Hypothesis>("H001").State);

            engine.Apply(falsifier.Step(NewView(engine, bus)).Single()); // fail
            Assert.Equal(HypothesisState.Failed, engine.Get<Hypothesis>("H001").State);

            Assert.Empty(falsifier.Step(NewView(engine, bus)));
        }

        [Fact]
        public void MockFalsifier_WithoutScript_DoesNothing()
        {
            var (bus, engine) = TestHarness.NewEngine();
            var falsifier = new MockFalsifier("F", "H001", contradiction: null);

            Assert.Empty(falsifier.Step(NewView(engine, bus)));
        }

        [Fact]
        public void MockDecision_RejectsFailedHypothesis_Once()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new CreateHypothesisProposal("A", "H001", "h", 0.62, "E001"));
            engine.Apply(new StartInvestigationProposal("A", "H001"));
            engine.Apply(new CreateEvidenceProposal("F", "E003", "C", "F", 0.9));
            engine.Apply(new ContradictProposal("F", "H001", "E003"));
            engine.Apply(new StartFalsificationProposal("F", "H001"));
            engine.Apply(new FailHypothesisProposal("F", "H001"));

            var decision = new MockDecision("D", "H001");

            var first = decision.Step(NewView(engine, bus)).ToList();
            var proposal = Assert.IsType<ProposeDecisionProposal>(Assert.Single(first));
            Assert.Equal("REJECT_HYPOTHESIS", proposal.Verdict);
            Assert.True(engine.Apply(proposal).Ok);

            Assert.Empty(decision.Step(NewView(engine, bus))); // decided once
        }

        [Fact]
        public void MockDecision_AcceptsResolvedHypothesis()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new CreateHypothesisProposal("A", "H001", "h", 0.62, "E001"));
            engine.Apply(new StartInvestigationProposal("A", "H001"));
            engine.Apply(new SupportProposal("A", "H001", "E001"));
            engine.Apply(new ResolveHypothesisProposal("A", "H001"));

            var decision = new MockDecision("D", "H001");

            var proposal = Assert.IsType<ProposeDecisionProposal>(Assert.Single(decision.Step(NewView(engine, bus))));
            Assert.Equal("ACCEPT_HYPOTHESIS", proposal.Verdict);
        }
    }
}
