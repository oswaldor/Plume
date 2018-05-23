using System;

namespace Microsoft.Content.TopicExtraction
{
    [Serializable]
    public class LDAParameters
    {
        public int NumTopics { get; set; }

        public double Alpha { get; set; }

        public double Rho { get; set; }

        public int Minibatch { get; set; }

        public double PowerT { get; set; }

        public double InitialT { get; set; }

        public int Passes { get; set; }
    }
}
