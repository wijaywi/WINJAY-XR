using Winjay.Core.Model;
using Winjay.Core.Proposals;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class StateMachineTests
    {
        private static Result ApplyAndAssertOk(WinjayStateEngine engine, AgentProposal proposal)
        {
            var result = engine.Apply(proposal);
            Assert.True(result.Ok, $"Proposal {proposal.Kind} failed: {result.ErrorCode} — {result.Error}");
            return result;
        }

        [Fact]
        public void ConflictPath_DocPhase2Sequence_EndsFailed()
        {
            // Exact sequence from the spec, PHASE 2:
            // Create H001 -> Investigate -> Add E001 -> Support H001 ->
            // Add E004 -> Conflict -> Falsify -> Fail
            var (bus, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));

            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "Evidence E001 and E002 support Hypothesis H001.", 0.62, "E001"));

            Assert.Equal(HypothesisState.Created, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            Assert.Equal(HypothesisState.Investigating, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));
            Assert.Equal(HypothesisState.Supported, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockFalsifier", "E004",
                "Contradicting observation", "MockFalsifier", 0.90));

            ApplyAndAssertOk(engine, new ContradictProposal("MockFalsifier", "H001", "E004"));
            Assert.Equal(HypothesisState.Conflicted, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new StartFalsificationProposal("MockFalsifier", "H001"));
            Assert.Equal(HypothesisState.Falsifying, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new FailHypothesisProposal("MockFalsifier", "H001"));
            Assert.Equal(HypothesisState.Failed, engine.Get<Hypothesis>("H001").State);

            // Conflict entity must exist and point at H001 + E004.
            var conflict = engine.Get<Conflict>("CF001");
            Assert.NotNull(conflict);
            Assert.Equal("H001", conflict.HypothesisId);
            Assert.Equal("E004", conflict.EvidenceId);

            Assert.True(bus.Journal.Count > 0, "event journal should not be empty");
        }

        [Fact]
        public void ResolvePath_CreatedToResolved()
        {
            var (_, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A clean hypothesis.", 0.62, "E001"));
            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));

            Assert.Equal(HypothesisState.Supported, engine.Get<Hypothesis>("H001").State);

            ApplyAndAssertOk(engine, new ResolveHypothesisProposal("MockAnalyzer", "H001"));
            Assert.Equal(HypothesisState.Resolved, engine.Get<Hypothesis>("H001").State);
        }

        [Fact]
        public void SecondSupport_KeepsSupported_AndRaisesConfidence()
        {
            var (_, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E002",
                "Evidence B", "MockResearcher", 0.70));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001", "E002"));
            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));

            var afterFirst = engine.Get<Hypothesis>("H001");
            var afterFirstConfidence = afterFirst.Confidence;
            Assert.Equal(HypothesisState.Supported, afterFirst.State);

            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E002"));

            var afterSecond = engine.Get<Hypothesis>("H001");
            Assert.Equal(HypothesisState.Supported, afterSecond.State);
            Assert.True(afterSecond.Confidence > afterFirstConfidence,
                "additional support must raise confidence");
            Assert.Equal(2, afterSecond.SupportingEvidence.Count);
        }

        [Fact]
        public void Confidence_DropsOnContradiction_RisesOnSupport()
        {
            var (_, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001"));
            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));

            var supported = engine.Get<Hypothesis>("H001");
            var supportedConfidence = supported.Confidence;
            Assert.True(supportedConfidence > 0.62, "support must raise confidence above the initial value");

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockFalsifier", "E004",
                "Contradiction", "MockFalsifier", 0.90));
            ApplyAndAssertOk(engine, new ContradictProposal("MockFalsifier", "H001", "E004"));

            var conflicted = engine.Get<Hypothesis>("H001");
            Assert.True(conflicted.Confidence < supportedConfidence,
                "contradiction must lower confidence");
            Assert.Contains("E004", conflicted.ContradictingEvidence);
        }

        [Fact]
        public void Versions_IncrementOnEveryCommit()
        {
            var (_, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001"));

            var h = engine.Get<Hypothesis>("H001");
            Assert.Equal(1, h.Version);

            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));
            ApplyAndAssertOk(engine, new ResolveHypothesisProposal("MockAnalyzer", "H001"));

            Assert.Equal(4, engine.Get<Hypothesis>("H001").Version);
        }

        [Fact]
        public void Provenance_IsRecordedPerEntity()
        {
            var (_, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001"));
            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));

            var h = engine.Get<Hypothesis>("H001");
            Assert.Equal(3, h.Provenance.Count);
            Assert.Equal("CREATE_HYPOTHESIS", h.Provenance[0].Action);
            Assert.Equal("START_INVESTIGATION", h.Provenance[1].Action);
            Assert.Equal("SUPPORT", h.Provenance[2].Action);
            Assert.Equal("MockAnalyzer", h.Provenance[0].Actor);

            var e = engine.Get<Evidence>("E001");
            Assert.Single(e.Provenance);
            Assert.Equal("CREATE_EVIDENCE", e.Provenance[0].Action);
        }

        [Fact]
        public void Decision_IsCommitted_WithProposedAndCommittedEvents()
        {
            var (bus, engine) = TestHarness.NewEngine();

            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81));
            ApplyAndAssertOk(engine, new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001"));
            ApplyAndAssertOk(engine, new StartInvestigationProposal("MockAnalyzer", "H001"));
            ApplyAndAssertOk(engine, new SupportProposal("MockAnalyzer", "H001", "E001"));
            ApplyAndAssertOk(engine, new CreateEvidenceProposal("MockFalsifier", "E004",
                "Contradiction", "MockFalsifier", 0.90));
            ApplyAndAssertOk(engine, new ContradictProposal("MockFalsifier", "H001", "E004"));
            ApplyAndAssertOk(engine, new StartFalsificationProposal("MockFalsifier", "H001"));
            ApplyAndAssertOk(engine, new FailHypothesisProposal("MockFalsifier", "H001"));

            var result = engine.Apply(new ProposeDecisionProposal("MockDecision", "H001",
                "REJECT_HYPOTHESIS", "Falsification evidence dominates", 0.87));

            Assert.True(result.Ok, result.Error);

            var decision = engine.Get<Decision>("DC001");
            Assert.NotNull(decision);
            Assert.Equal("H001", decision.HypothesisId);
            Assert.Equal("REJECT_HYPOTHESIS", decision.Verdict);
            Assert.Equal(0.87, decision.Confidence, 3);

            Assert.Contains(result.Events, e => e.Type == Winjay.Core.Events.WinjayEventType.DecisionProposed);
            Assert.Contains(result.Events, e => e.Type == Winjay.Core.Events.WinjayEventType.DecisionCommitted);
            Assert.Contains(bus.Journal, e => e.Type == Winjay.Core.Events.WinjayEventType.DecisionCommitted);
        }
    }
}
