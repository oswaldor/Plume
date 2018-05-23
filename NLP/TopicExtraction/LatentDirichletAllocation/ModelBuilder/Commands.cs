using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.Recommendations.Common
{
    public class Commands
    {
        public const string GetMetrics = "getmetrics";

        public const string LearnLDA = "learnlda";

        public const string Docs2Text = "docs2text";

        public const string VocabularyStatistics = "vocabularystats";

        public const string GenerateDocTopicVectorsLDA = "generatedocvectors";
        
        public const string ConvertVectors = "convertvectors";
        
        public const string FeaturizeDocuments = "featurizedocs";

        public const string SimilarityPerfMetrics = "similarityperfmetrics";

        public const string PairwiseSimilarity = "pairwisesimilarity";

        public const string OneToManySimilarity = "onetomanysimilarity";

        public const string Silhouette = "silhouette";

        public const string Partition = "partition";

        public const string NER = "ner";
    }

    public class Options
    {
        public const string Config = "config";

        public const string TopWords = "topwords";

        public const string Epsilon = "epsilon";

        public const string InputFile = "inputfile";

        public const string InputFile2 = "inputfile2";

        public const string OutputFile = "outputfile";

        public const string CorpusRepository = "corpusrepo";

        public const string Locale = "locale";

        public const string Corpus = "corpus";

        public const string SampleName = "samplename";

        public const string Encoding = "encoding";

        public const string Normalization = "norm";

        public const string Compression = "compress";

        public const string CopyFeaturizedDoc = "copyfd";

        public const string PartitionCount = "partcount";

        public const string FirstDocument = "firstdoc";

        public const string DocumentCount = "doccount";

        // Format Similarity or Distance matrices by padding lower-left corner with 0's 
        public const string Padding = "padding";

        // Silhouette computation Options
        
        // Limit silhouette computation to top ClusterCount clusters
        // Default = 0 => All clusters
        public const string ClusterCount = "clustercount";

        // Compute silhouette on a sample of SampleRate%.  e.g. 0.01 = 1%
        // Default = 0.01
        public const string SampleRate = "samplerate";

        
        // Reserve this many gigs of ram to keep clusters in memory.  
        // Go to disk for those that do not fit
        // Default = 3 Gigs
        public const string MaxMemory = "maxmemory";
        
        // Use this many threds to compute vector distances.
        // Default = 8
        public const string MaxThreads = "maxthreads";
    }
}
