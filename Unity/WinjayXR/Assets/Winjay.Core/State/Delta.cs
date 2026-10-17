using System.Collections.Generic;

namespace Winjay.Core.State
{
    public sealed class FieldChange
    {
        public FieldChange(string field, string from, string to)
        {
            Field = field;
            From = from;
            To = to;
        }

        public string Field { get; }
        public string From { get; }
        public string To { get; }
    }

    public sealed class Delta
    {
        public Delta(string entityId, int fromVersion, int toVersion, IEnumerable<FieldChange> changes)
        {
            EntityId = entityId;
            FromVersion = fromVersion;
            ToVersion = toVersion;
            Changes = new List<FieldChange>(changes);
        }

        public string EntityId { get; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        public IReadOnlyList<FieldChange> Changes { get; }
    }
}
