
namespace Microsoft.Content.TopicExtraction
{
    using System;

    [Serializable]
    public class ModelStatistics
    {
        public int VocabularySize { get; set; }
        public int DocumentCount { get; set; }
        public int[] BadTopics { get; set; }
    }
}
