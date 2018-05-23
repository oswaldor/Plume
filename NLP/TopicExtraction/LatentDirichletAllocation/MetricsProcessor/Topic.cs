using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.TopicExtraction
{
    class Topic
    {
        private const int NUM_OF_WORDS_IN_TEMP_LABEL = 10;
        public int TopicId { get; set; }

        public bool IsBad { get; set; }

        public float Allocations { get; set; }

        public float TC { get; set; }

        public float TS { get; set; }

        public float TD { get; set; }

        public float NormalizedTC { get; set; }

        public float NormalizedTS { get; set; }

        public float NormalizedTD { get; set; }

        public long PromimentDF { get; set; }

        public List<Tuple<int, double>> TopProminentDocuments { get; set; }

        public List<string> HighProbWords { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}",
                TopicId,
                IsBad ? 1 : 0,
                Allocations,
                TC,
                TS,
                TD,
                NormalizedTC,
                NormalizedTS,
                NormalizedTD,
                PromimentDF,
                string.Join(", ", TopProminentDocuments.Select(d => string.Format("{0}|{1:0.0000}", d.Item1, d.Item2))),
                string.Join(",", HighProbWords.Take(NUM_OF_WORDS_IN_TEMP_LABEL)),
                string.Join("\t", HighProbWords));
            return sb.ToString();            
        }
    }
}
