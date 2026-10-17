using Winjay.Core.Mind;

namespace Winjay.Gateway
{
    /// <summary>
    /// Full-payload wire form of a MindEvent: the client rebuilds the whole
    /// map by folding these events, so nothing visible needs a side channel.
    /// </summary>
    public sealed class MindEventDto
    {
        public long Seq { get; set; }
        public string Type { get; set; }
        public string Actor { get; set; }

        public string NodeId { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public string Contributor { get; set; }
        public string ParentId { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? Z { get; set; }
        public bool? Ghost { get; set; }
        public string Phase { get; set; }
        public string MergedIntoId { get; set; }

        public string LinkId { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string LinkType { get; set; }
        public string Originator { get; set; }
        public double? Weight { get; set; }
        public string Detail { get; set; }

        public double? OldStrength { get; set; }
        public double? NewStrength { get; set; }

        public static MindEventDto From(MindEvent e)
        {
            return new MindEventDto
            {
                Seq = e.Sequence,
                Type = e.Type.ToString(),
                Actor = e.Actor,
                NodeId = e.NodeId,
                Label = e.Label,
                Kind = e.Kind?.ToString(),
                Contributor = e.Contributor?.ToString(),
                ParentId = e.ParentId,
                X = e.Position?.X,
                Y = e.Position?.Y,
                Z = e.Position?.Z,
                Ghost = e.Ghost,
                Phase = e.Phase,
                MergedIntoId = e.MergedIntoId,
                LinkId = e.LinkId,
                FromId = e.FromId,
                ToId = e.ToId,
                LinkType = e.LinkType?.ToString(),
                Originator = e.LinkOriginator?.ToString(),
                Weight = e.Weight,
                Detail = e.Detail,
                OldStrength = e.OldStrength,
                NewStrength = e.NewStrength
            };
        }
    }
}
