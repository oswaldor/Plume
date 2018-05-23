using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Content.Recommendations.Common;

namespace Microsoft.Content.TopicExtraction
{
    class LDAModelStatusChecker
    {
        /// <summary>
        /// Check if the model files (.binn, .DocumentTopicAllocations, .WordTopicAllocations) have been completely written to disk.
        /// </summary>
        /// <param name="configFileForTest"></param>
        /// <returns></returns>
        public static bool AreModelFilesReady(string configFileForTest)
        {
            // Get model directory
            string modelDir = Path.GetDirectoryName(configFileForTest);

            string binFile = configFileForTest.Replace("LDAConfig.json", "bin");

            string documentTopicAllocationFile = Path.Combine(modelDir, "build", "DocumentTopicAllocations.txt");
            string wordTopicAllocationFile = Path.Combine(modelDir, "build", "WordTopicAllocations.txt");

            if (FileManager.IsFileLocked(binFile) || FileManager.IsFileLocked(documentTopicAllocationFile) || FileManager.IsFileLocked(wordTopicAllocationFile))
            {
                return false;
            }
            return true;
        }

        public static bool HaveMetricsBeenComputed(string configFileForTest, string testSampleName, ModelMetricTypes metricsType)
        {
            if (metricsType == ModelMetricTypes.Both)
            {
                return HaveMetricsBeenComputed(configFileForTest, testSampleName, ModelMetricTypes.Intr) &&
                       HaveMetricsBeenComputed(configFileForTest, testSampleName, ModelMetricTypes.Extr);
            }

            // Get model directory
            string modelDir = Path.GetDirectoryName(configFileForTest);

            // Get metrics file path
            string metricsFile = "";
            if (metricsType == ModelMetricTypes.Intr)
            {
                metricsFile = Path.Combine(modelDir, string.Format(@"build\{0}.Perplexity.txt", testSampleName));
            }
            else if (metricsType == ModelMetricTypes.Extr)
            {
                metricsFile = Path.Combine(modelDir, @"build\ExtrinsicMetrics.tsv");
            }

            if (!File.Exists(metricsFile))
                return false;

            // file is locked for writing - important for multi-threading,
            // to avoid potential conflict with another thread that might be computing metrics for the same model.
            if (FileManager.IsFileLocked(metricsFile))
                return true;

            long length = FileManager.GetFileLength(metricsFile);

            return (length > 0L);
        }
    }
}
