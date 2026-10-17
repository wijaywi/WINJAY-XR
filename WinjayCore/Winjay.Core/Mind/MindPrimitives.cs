namespace Winjay.Core.Mind
{
    /// <summary>
    /// Lightweight 3D position (netstandard2.1 has no Unity dependency).
    /// Layout is authored by the scenario script so the demo always looks tidy.
    /// </summary>
    public struct MindVec3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public MindVec3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.00") + ", " + Y.ToString("0.00") + ", " + Z.ToString("0.00") + ")";
        }
    }

    public enum MindNodeKind
    {
        Root,
        Branch,
        Idea,
        Human
    }

    public enum MindLinkType
    {
        /// <summary>Plain hierarchy edge: "part of".</summary>
        Branch,
        /// <summary>Green: this connection supports both ends.</summary>
        Support,
        /// <summary>Red, vibrating: the two ideas collide.</summary>
        Conflict,
        /// <summary>Curved cross-branch arc: an unexpected, not-yet-judged connection.</summary>
        Bridge
    }

    public enum MindContributor
    {
        Explorer,
        Connector,
        Challenger,
        Human
    }
}
