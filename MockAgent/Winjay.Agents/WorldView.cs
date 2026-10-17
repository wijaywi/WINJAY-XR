using System.Collections.Generic;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace Winjay.Agents
{
    /// <summary>
    /// Read-only window onto the Winjay world. Agents receive a fresh view on
    /// every step; they cannot reach the engine and therefore cannot mutate
    /// anything — they can only propose.
    /// </summary>
    public sealed class WorldView
    {
        private readonly WinjayStateEngine _engine;

        public WorldView(WinjayStateEngine engine, WinjayEventBus bus)
        {
            _engine = engine;
            Entities = engine.Entities;
            Journal = bus.Journal;
        }

        public IReadOnlyDictionary<string, WinjayEntity> Entities { get; }
        public IReadOnlyList<WinjayEvent> Journal { get; }

        public bool Exists(string id)
        {
            return Entities.ContainsKey(id);
        }

        public T Get<T>(string id) where T : WinjayEntity
        {
            return _engine.Get<T>(id);
        }
    }
}
