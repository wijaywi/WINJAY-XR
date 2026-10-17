using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winjay.Core.Events;
using Winjay.Core.Proposals;
using Winjay.Core.State;
using Xunit;

namespace Winjay.Tests
{
    public class EventTests
    {
        private static void RunFullConflictScenario(WinjayStateEngine engine)
        {
            engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001", "Evidence A", "MockResearcher", 0.81));
            engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001", "A hypothesis.", 0.62, "E001"));
            engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001"));
            engine.Apply(new SupportProposal("MockAnalyzer", "H001", "E001"));
            engine.Apply(new CreateEvidenceProposal("MockFalsifier", "E004", "Contradiction", "MockFalsifier", 0.90));
            engine.Apply(new ContradictProposal("MockFalsifier", "H001", "E004"));
            engine.Apply(new StartFalsificationProposal("MockFalsifier", "H001"));
            engine.Apply(new FailHypothesisProposal("MockFalsifier", "H001"));
        }

        [Fact]
        public void Journal_ContainsEveryExpectedEventType()
        {
            var (bus, engine) = TestHarness.NewEngine();
            RunFullConflictScenario(engine);

            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.EntityCreated);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.EvidenceAdded);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.HypothesisCreated);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.StateChanged);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.ConflictDetected);
            Assert.Contains(bus.Journal, e => e.Type == WinjayEventType.FalsificationStarted);
        }

        [Fact]
        public void Journal_SequencesAreMonotonicAndUnique()
        {
            var (bus, engine) = TestHarness.NewEngine();
            RunFullConflictScenario(engine);

            long last = 0;
            var seen = new HashSet<long>();
            foreach (var evt in bus.Journal)
            {
                Assert.True(evt.Sequence > last, $"sequence must be strictly increasing at {evt.Sequence}");
                Assert.True(seen.Add(evt.Sequence), $"duplicate sequence {evt.Sequence}");
                last = evt.Sequence;
            }
        }

        [Fact]
        public void Subscribers_ReceiveEveryPublishedEvent()
        {
            var (bus, engine) = TestHarness.NewEngine();
            var received = new List<WinjayEvent>();
            using (bus.Subscribe(e => received.Add(e)))
            {
                RunFullConflictScenario(engine);
            }
            Assert.Equal(bus.Journal.Count, received.Count);
        }

        [Fact]
        public async Task SubscribeAndPublish_AreSafeWhenRunConcurrently()
        {
            var bus = new WinjayEventBus();
            var delivered = 0;
            const int workers = 4;
            const int operationsPerWorker = 100;

            var subscribers = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < operationsPerWorker; i++)
                {
                    bus.Subscribe(_ => Interlocked.Increment(ref delivered));
                }
            }));
            var publishers = Enumerable.Range(0, workers).Select(worker => Task.Run(() =>
            {
                for (var i = 0; i < operationsPerWorker; i++)
                {
                    bus.Publish(new WinjayEvent(worker * operationsPerWorker + i,
                        WinjayEventType.EntityCreated, "test"));
                }
            }));

            await Task.WhenAll(subscribers.Concat(publishers));

            Assert.Equal(workers * operationsPerWorker, bus.Journal.Count);
            Assert.Equal(workers * operationsPerWorker, bus.Journal.Select(e => e.Sequence).Distinct().Count());
            Assert.True(delivered > 0);
        }

        [Fact]
        public void Publish_ContinuesAfterSubscriberThrowsAndReportsFailure()
        {
            var bus = new WinjayEventBus();
            var laterSubscriberCalls = 0;
            bus.Subscribe(_ => throw new InvalidOperationException("subscriber failure"));
            bus.Subscribe(_ => laterSubscriberCalls++);

            var error = Assert.Throws<AggregateException>(() => bus.Publish(
                new WinjayEvent(1, WinjayEventType.EntityCreated, "test")));

            Assert.IsType<InvalidOperationException>(Assert.Single(error.InnerExceptions));
            Assert.Equal(1, laterSubscriberCalls);
            Assert.Single(bus.Journal);
        }

        [Fact]
        public void Publish_UsesStableSubscriberSnapshot()
        {
            var bus = new WinjayEventBus();
            var firstCalls = 0;
            var lateCalls = 0;
            bus.Subscribe(_ =>
            {
                firstCalls++;
                if (firstCalls == 1)
                {
                    bus.Subscribe(__ => lateCalls++);
                }
            });

            bus.Publish(new WinjayEvent(1, WinjayEventType.EntityCreated, "test"));
            Assert.Equal(0, lateCalls);

            bus.Publish(new WinjayEvent(2, WinjayEventType.EntityCreated, "test"));
            Assert.Equal(1, lateCalls);
        }

        [Fact]
        public void JournalView_RemainsLiveAcrossPublishes()
        {
            var bus = new WinjayEventBus();
            var journal = bus.Journal;

            bus.Publish(new WinjayEvent(1, WinjayEventType.EntityCreated, "test"));

            Assert.Single(journal);
        }

        [Fact]
        public void StateChangedEvent_CarriesDelta_WithStateAndConfidence()
        {
            var (bus, engine) = TestHarness.NewEngine();
            RunFullConflictScenario(engine);

            var supportEvent = Assert.Single(bus.Journal,
                e => e.Type == WinjayEventType.StateChanged && e.Detail == "supported by E001");
            Assert.NotNull(supportEvent.Delta);
            Assert.Equal("H001", supportEvent.Delta.EntityId);
            Assert.Equal(2, supportEvent.Delta.FromVersion);
            Assert.Equal(3, supportEvent.Delta.ToVersion);
            Assert.Contains(supportEvent.Delta.Changes, c => c.Field == "state" && c.From == "Investigating" && c.To == "Supported");
            Assert.Contains(supportEvent.Delta.Changes, c => c.Field == "confidence");
            Assert.Contains(supportEvent.Delta.Changes, c => c.Field == "supporting_evidence");
        }

        [Fact]
        public void ConflictDetectedEvent_ReferencesConflictEntity()
        {
            var (bus, engine) = TestHarness.NewEngine();
            RunFullConflictScenario(engine);

            var conflictEvent = Assert.Single(bus.Journal, e => e.Type == WinjayEventType.ConflictDetected);
            Assert.Equal("CF001", conflictEvent.EntityId);
            Assert.Equal("E004 contradicts H001", conflictEvent.Detail);
        }

        [Fact]
        public void Delta_SendsOnlyChanges_NotFullState()
        {
            var (bus, engine) = TestHarness.NewEngine();
            engine.Apply(new CreateEvidenceProposal("MockResearcher", "E001", "Evidence A", "MockResearcher", 0.81));
            engine.Apply(new CreateHypothesisProposal("MockAnalyzer", "H001", "A hypothesis.", 0.62, "E001"));
            engine.Apply(new StartInvestigationProposal("MockAnalyzer", "H001"));

            var delta = Assert.Single(bus.Journal,
                e => e.Type == WinjayEventType.StateChanged && e.EntityId == "H001").Delta;

            Assert.NotNull(delta);
            Assert.Single(delta.Changes); // only the state field changed
            Assert.Equal("state", delta.Changes[0].Field);
        }
    }
}
