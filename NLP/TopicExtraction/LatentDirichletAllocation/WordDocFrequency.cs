using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.TopicExtraction
{
    internal class WordDocFrequency
    {
        public WordDocFrequency(int wordId, int frequency)
        {
            this.WordId = wordId;
            this.Frequency = frequency;
        }

        public int WordId { get; set; }

        public int Frequency { get; set; }
    }
}
