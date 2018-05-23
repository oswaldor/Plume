using System;

namespace Microsoft.Content.TopicExtraction
{
    [Serializable]
    public class FeaturizationParameters
    {
        public int MinWordDocumentFrequency { get; set; }

        // Drop words from vacabulaty if they show up in more than this percentage
        public float MaxRalativeWordDocumentFrequency { get; set; }
        
        // Vacabulary contains Normalized Entity Names
        public bool NamedEntityNormalization { get; set; }
    }
}
