using Winjay.Core.Proposals;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class ValidationTests
    {
        private static (WinjayStateEngine Engine, Winjay.Core.Events.WinjayEventBus Bus) SetupHypothesis(WinjayStateEngine engine, Winjay.Core.Events.WinjayEventBus bus)
        {
            Assert.True(engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81)).Ok);
            Assert.True(engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001")).Ok);
            Assert.True(engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001")).Ok);
            return (engine, bus);
        }

        [Fact]
        public void DuplicateEvidenceId_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            Assert.True(engine.Apply(new CreateEvidenceProposal("A", "E001", "x", "A", 0.5)).Ok);

            var result = engine.Apply(new CreateEvidenceProposal("B", "E001", "y", "B", 0.5));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.DuplicateId, result.ErrorCode);
        }

        [Fact]
        public void DuplicateHypothesisId_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            Assert.True(engine.Apply(new CreateEvidenceProposal("A", "E001", "x", "A", 0.5)).Ok);
            Assert.True(engine.Apply(new CreateHypothesisProposal("A", "H001", "h", 0.5, "E001")).Ok);

            var result = engine.Apply(new CreateHypothesisProposal("B", "H001", "h2", 0.5, "E001"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.DuplicateId, result.ErrorCode);
        }

        [Fact]
        public void HypothesisBasedOnMissingEvidence_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();

            var result = engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001",
                "h", 0.5, "E999"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.NotFound, result.ErrorCode);
        }

        [Fact]
        public void TransitionOnUnknownHypothesis_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();

            var result = engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H999"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.NotFound, result.ErrorCode);
        }

        [Fact]
        public void SupportOnUnknownEvidence_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            SetupHypothesis(engine, bus);

            var result = engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E999"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.NotFound, result.ErrorCode);
        }

        [Fact]
        public void IllegalTransitions_AreRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            SetupHypothesis(engine, bus);

            // FAIL from INVESTIGATING is illegal (only from FALSIFYING).
            Assert.Equal(Result.Codes.InvalidTransition,
                engine.Apply(new FailHypothesisProposal("A", "H001")).ErrorCode);

            // RESOLVE from INVESTIGATING is illegal (only from SUPPORTED).
            Assert.Equal(Result.Codes.InvalidTransition,
                engine.Apply(new ResolveHypothesisProposal("A", "H001")).ErrorCode);

            // START_FALSIFICATION from INVESTIGATING is illegal (only from CONFLICTED).
            Assert.Equal(Result.Codes.InvalidTransition,
                engine.Apply(new StartFalsificationProposal("A", "H001")).ErrorCode);
        }

        [Fact]
        public void StartInvestigation_Twice_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            SetupHypothesis(engine, bus);

            // Already INVESTIGATING; second START_INVESTIGATION has no transition.
            Assert.Equal(Result.Codes.InvalidTransition,
                engine.Apply(new StartInvestigationProposal("A", "H001")).ErrorCode);
        }

        [Fact]
        public void StaleVersion_IsRejected()
        {
            var (bus, engine) = TestHarness.NewEngine();
            Assert.True(engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81)).Ok);
            Assert.True(engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001")).Ok);

            // Hypothesis is at version 1. A proposal pinned to version 3 is stale.
            var result = engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001", expectedVersion: 3));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.StaleVersion, result.ErrorCode);
        }

        [Fact]
        public void VersionPinnedToCurrent_IsAccepted()
        {
            var (bus, engine) = TestHarness.NewEngine();
            Assert.True(engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001",
                "Evidence A", "MockResearcher", 0.81)).Ok);
            Assert.True(engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001",
                "A hypothesis.", 0.62, "E001")).Ok);

            var result = engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001", expectedVersion: 1));

            Assert.True(result.Ok, result.Error);
        }

        [Fact]
        public void EvidenceCannotFlip_FromSupportToContradict()
        {
            var (bus, engine) = TestHarness.NewEngine();
            SetupHypothesis(engine, bus);

            Assert.True(engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E001")).Ok);

            var result = engine.Apply(new ContradictProposal("MockFalsifier", "H001", "E001"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public void SameEvidence_CannotSupportTwice()
        {
            var (bus, engine) = TestHarness.NewEngine();
            SetupHypothesis(engine, bus);

            Assert.True(engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E001")).Ok);

            var result = engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E001"));

            Assert.False(result.Ok);
            Assert.Equal(Result.Codes.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public void RejectedProposal_EmitsProposalRejectedEvent()
        {
            var (bus, engine) = TestHarness.NewEngine();

            var result = engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H999"));

            Assert.False(result.Ok);
            Assert.Contains(bus.Journal, e => e.Type == Winjay.Core.Events.WinjayEventType.ProposalRejected);
            Assert.Single(result.Events);
            Assert.Equal(Winjay.Core.Events.WinjayEventType.ProposalRejected, result.Events[0].Type);
        }
    }
}
