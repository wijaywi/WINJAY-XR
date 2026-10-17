using System;
using System.Collections.Generic;
using System.Linq;
using Winjay.Core.Events;
using Winjay.Core.State;

namespace Winjay.Agents
{
    /// <summary>
    /// Consumer-side view of the delta stream. Instead of full-state sync,
    /// renderers and future remote clients follow only what changed:
    /// DeltaJournal.Since(sequence) returns the deltas after that point.
    /// </summary>
    public sealed class DeltaJournal : IDisposable
    {
        private readonly List<(long Sequence, Delta Delta)> _entries = new List<(long Sequence, Delta Delta)>();
        private readonly IDisposable _subscription;

        public DeltaJournal(WinjayEventBus bus)
        {
            _subscription = bus.Subscribe(OnEvent);
        }

        public IReadOnlyList<Delta> Deltas => _entries.Select(e => e.Delta).ToList();

        public IEnumerable<(long Sequence, Delta Delta)> Since(long sequence)
        {
            return _entries.Where(e => e.Sequence > sequence);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void OnEvent(WinjayEvent evt)
        {
            if (evt.Delta != null)
            {
                _entries.Add((evt.Sequence, evt.Delta));
            }
        }
    }
}
