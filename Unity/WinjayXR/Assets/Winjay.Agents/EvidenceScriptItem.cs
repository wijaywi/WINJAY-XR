namespace Winjay.Agents
{
    public sealed class EvidenceScriptItem
    {
        public EvidenceScriptItem(string id, string content, double confidence)
        {
            Id = id;
            Content = content;
            Confidence = confidence;
        }

        public string Id { get; }
        public string Content { get; }
        public double Confidence { get; }
    }
}
