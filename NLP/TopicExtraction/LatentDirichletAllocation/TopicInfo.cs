using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.TopicExtraction
{
    internal class TopicInfo
    {
        public bool IsBadTopic;

        public double AggregatedAllocation;

        public double ProminentAllocation;

        public int ProminentFrequency;

        public List<Tuple<int, double>> TopProminentDocuments;

        public TopicInfo()
        {
            this.TopProminentDocuments = new List<Tuple<int, double>>();
        }
    }
}
