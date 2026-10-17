using System;
using System.Collections;
using System.Collections.Generic;

namespace Winjay.Core.Events
{
    public sealed class WinjayEventBus
    {
        private readonly object _sync = new object();
        private readonly List<Action<WinjayEvent>> _handlers = new List<Action<WinjayEvent>>();
        private readonly List<WinjayEvent> _journal = new List<WinjayEvent>();
        private readonly IReadOnlyList<WinjayEvent> _journalView;

        public WinjayEventBus()
        {
            _journalView = new JournalView(this);
        }

        public IReadOnlyList<WinjayEvent> Journal => _journalView;

        public IDisposable Subscribe(Action<WinjayEvent> handler)
        {
            lock (_sync)
            {
                _handlers.Add(handler);
            }
            return new Subscription(this, handler);
        }

        internal void Publish(WinjayEvent evt)
        {
            Action<WinjayEvent>[] handlers;
            lock (_sync)
            {
                _journal.Add(evt);
                handlers = _handlers.ToArray();
            }

            List<Exception> errors = null;
            foreach (var handler in handlers)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception error)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(error);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("One or more event subscribers failed.", errors);
            }
        }

        private sealed class JournalView : IReadOnlyList<WinjayEvent>
        {
            private readonly WinjayEventBus _bus;

            public JournalView(WinjayEventBus bus)
            {
                _bus = bus;
            }

            public int Count
            {
                get
                {
                    lock (_bus._sync)
                    {
                        return _bus._journal.Count;
                    }
                }
            }

            public WinjayEvent this[int index]
            {
                get
                {
                    lock (_bus._sync)
                    {
                        return _bus._journal[index];
                    }
                }
            }

            public IEnumerator<WinjayEvent> GetEnumerator()
            {
                WinjayEvent[] snapshot;
                lock (_bus._sync)
                {
                    snapshot = _bus._journal.ToArray();
                }
                return ((IEnumerable<WinjayEvent>)snapshot).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly WinjayEventBus _bus;
            private readonly Action<WinjayEvent> _handler;

            public Subscription(WinjayEventBus bus, Action<WinjayEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                lock (_bus._sync)
                {
                    _bus._handlers.Remove(_handler);
                }
            }
        }
    }
}
