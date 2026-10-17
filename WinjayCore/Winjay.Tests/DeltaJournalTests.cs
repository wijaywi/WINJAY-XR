using System.Linq;
using Winjay.Agents;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class DeltaJournalTests
    {
        private static void RunScenario(WinjayStateEngine engine)
        {
            engine.Apply(new Winjay.Core.Proposals.CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new Winjay.Core.Proposals.CreateHypothesisProposal("A", "H001", "h", 0.62, "E001"));
            engine.Apply(new Winjay.Core.Proposals.StartInvestigationProposal("A", "H001"));
            engine.Apply(new Winjay.Core.Proposals.SupportProposal("A", "H001", "E001"));
            engine.Apply(new Winjay.Core.Proposals.CreateEvidenceProposal("F", "E003", "C", "F", 0.9));
            engine.Apply(new Winjay.Core.Proposals.ContradictProposal("F", "H001", "E003"));
            engine.Apply(new Winjay.Core.Proposals.StartFalsificationProposal("F", "H001"));
            engine.Apply(new Winjay.Core.Proposals.FailHypothesisProposal("F", "H001"));
        }

        [Fact]
        public void Journal_CollectsOnlyDeltas_InOrder()
        {
            var (bus, engine) = TestHarness.NewEngine();
            using (var journal = new DeltaJournal(bus))
            {
                RunScenario(engine);

                // expected deltas: investigate, support, contradict, falsify, fail = 5
                Assert.Equal(5, journal.Deltas.Count);

                var states = journal.Deltas
                    .SelectMany(d => d.Changes.Where(c => c.Field == "state"))
                    .Select(c => c.To)
                    .ToList();
                Assert.Equal(new[]
                {
                    "Investigating", "Supported", "Conflicted", "Falsifying", "Failed"
                }, states);
            }
        }

        [Fact]
        public void Journal_Since_ReturnsDeltasAfterSequence()
        {
            var (bus, engine) = TestHarness.NewEngine();
            using (var journal = new DeltaJournal(bus))
            {
                RunScenario(engine);

                var supportEvent = bus.Journal.Single(e => e.Type == WinjayEventType.StateChanged && e.Detail == "supported by E001");
                var later = journal.Since(supportEvent.Sequence).ToList();

                Assert.Equal(3, later.Count); // contradict, falsify, fail
                Assert.All(later, e => Assert.True(e.Sequence > supportEvent.Sequence));
            }
        }

        [Fact]
        public void Journal_Dispose_StopsReceivingEvents()
        {
            var (bus, engine) = TestHarness.NewEngine();
            var journal = new DeltaJournal(bus);

            engine.Apply(new Winjay.Core.Proposals.CreateEvidenceProposal("R", "E001", "A", "R", 0.8));
            engine.Apply(new Winjay.Core.Proposals.CreateHypothesisProposal("A", "H001", "h", 0.62, "E001"));
            Assert.Empty(journal.Deltas); // no state changes yet

            journal.Dispose();

            engine.Apply(new Winjay.Core.Proposals.StartInvestigationProposal("A", "H001"));
            Assert.Empty(journal.Deltas); // unsubscribed
        }
    }
}
