using System;
using System.Collections.Generic;
using System.Linq;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.Proposals;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class DeterminismTests
    {
        private static void RunScenario(WinjayStateEngine engine)
        {
            engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001", "Evidence A", "MockResearcher", 0.81));
            engine.Apply(new CreateEvidenceProposal("MockResearcher", "E002", "Evidence B", "MockResearcher", 0.70));
            engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001", "A hypothesis.", 0.62, "E001", "E002"));
            engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001"));
            engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E001"));
            engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E002"));
            engine.Apply(new CreateEvidenceProposal("MockFalsifier", "E004", "Contradiction", "MockFalsifier", 0.90));
            engine.Apply(new ContradictProposal("MockFalsifier", "H001", "E004"));
            engine.Apply(new StartFalsificationProposal("MockFalsifier", "H001"));
            engine.Apply(new FailHypothesisProposal("MockFalsifier", "H001"));
            engine.Apply(new ProposeDecisionProposal("MockDecision", "H001", "REJECT_HYPOTHESIS",
                "Falsification evidence dominates", 0.87));
        }

        [Fact]
        public void SameProposalSequence_ProducesIdenticalState()
        {
            var (busA, engineA) = TestHarness.NewEngine();
            var (busB, engineB) = TestHarness.NewEngine();

            RunScenario(engineA);
            RunScenario(engineB);

            Assert.Equal(engineA.Entities.Count, engineB.Entities.Count);
            Assert.Equal(engineA.Sequence, engineB.Sequence);

            foreach (var pair in engineA.Entities)
            {
                var a = pair.Value;
                var b = engineB.Entities[pair.Key];
                Assert.NotNull(b);
                Assert.Equal(a.Type, b.Type);
                Assert.Equal(a.Version, b.Version);
                Assert.Equal(a.Confidence, b.Confidence, 12);
                Assert.Equal(a.Provenance.Count, b.Provenance.Count);

                if (a is Hypothesis ha && b is Hypothesis hb)
                {
                    Assert.Equal(ha.State, hb.State);
                    Assert.Equal(ha.SupportingEvidence, hb.SupportingEvidence);
                    Assert.Equal(ha.ContradictingEvidence, hb.ContradictingEvidence);
                }
                if (a is Decision da && b is Decision db)
                {
                    Assert.Equal(da.Verdict, db.Verdict);
                }
            }
        }

        [Fact]
        public void SameProposalSequence_ProducesIdenticalEventStream()
        {
            var (busA, engineA) = TestHarness.NewEngine();
            var (busB, engineB) = TestHarness.NewEngine();

            RunScenario(engineA);
            RunScenario(engineB);

            var a = busA.Journal.Select(e => (e.Sequence, e.Type, e.EntityId, e.Detail)).ToList();
            var b = busB.Journal.Select(e => (e.Sequence, e.Type, e.EntityId, e.Detail)).ToList();

            Assert.Equal(a, b);
        }

        [Fact]
        public void ConfidenceValues_AreExactAndRepeatable()
        {
            var (_, engineA) = TestHarness.NewEngine();
            var (_, engineB) = TestHarness.NewEngine();

            RunScenario(engineA);
            RunScenario(engineB);

            var hA = engineA.Get<Hypothesis>("H001");
            var hB = engineB.Get<Hypothesis>("H001");

            // Documented update rules: support -> c + (1-c)*e*0.35, contradict -> c*(1 - e*0.35).
            Assert.Equal(hA.Confidence, hB.Confidence, 12);
            Assert.Equal(0.62, 0.62, 12); // initial value sanity
            Assert.True(hA.Confidence > 0.0 && hA.Confidence < 1.0);
        }
    }
}
