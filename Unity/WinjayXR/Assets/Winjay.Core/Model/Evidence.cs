namespace Winjay.Core.Model
{
    public sealed class Evidence : WinjayEntity
    {
        public Evidence(string id, string content, string source, double confidence)
            : base(id, EntityType.Evidence, confidence)
        {
            Content = content;
            Source = source;
        }

        public string Content { get; }
        public string Source { get; }
    }
}
