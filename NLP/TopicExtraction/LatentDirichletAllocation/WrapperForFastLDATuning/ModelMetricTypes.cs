using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.TopicExtraction
{
    public enum ModelMetricTypes
    {
        Intr = 1,  // Intrinsic metric, e.g. perplexity
        Extr = 2,  // Extrinsic metrics, e.g. topic coherence
        Both = 3
    };
}
