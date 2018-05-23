using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Concurrent;
using System.IO;

using Microsoft.Content.Recommendations.Common;
using Microsoft.Content.Recommendations.LinearAlgebra;

namespace Microsoft.Content.TopicExtraction
{
    class Program
    {
        public static int NumOfThreads;
        // Threshold of topic coherence (TC). A topic "t" is considered bad if "t.TC < ThresholdForBadTopics".
        public static float ThresholdForBadTopics;

        public static BlockingCollection<float>[] RawMetricsCollections;

        public static float[] MetricMeans;

        public static float[] MetricStdevs;
 
        private static Object thisLock = new Object();


        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Usage: MetricsProcessor  <NumOfThreads> <ThresholdOfTopicCoherenceForBadTopics> <OverrideModelRepository (optional)>\r\n");
                System.Console.WriteLine("\tE.g. MetricsProcessor  10  -210");
                System.Console.WriteLine("\tE.g. MetricsProcessor  10  -210 D:\\ModelRepository");
                Environment.Exit(1);            
            }

            string[] ModelRepositoriesForMetrics = Initialize(args);
            
            List<string[]> listOfArraysOfLDAConfigFiles = new List<string[]>();
            string searchPattern = "*.LDAConfig.json";            

            for (int i = 0; i < ModelRepositoriesForMetrics.Length; i++)
            {
                listOfArraysOfLDAConfigFiles.Add(Directory.GetFiles(ModelRepositoriesForMetrics[i], searchPattern, SearchOption.AllDirectories));
            }

            // Compute distribution mean and stdev for each metric type via Multi-threading.
            // First populate the RawMetricsCollections.
            for (int i = 0; i < listOfArraysOfLDAConfigFiles.Count; i++)
            {
                PopulateRawMetricsCollections(RawMetricsCollections, listOfArraysOfLDAConfigFiles[i], NumOfThreads);
            }

            // Then compute mean and stdev for each metric type
            for (int i = 0; i < MetricMeans.Length; i++)
            {
                MetricMeans[i] = RawMetricsCollections[i].Mean();
                MetricStdevs[i] = RawMetricsCollections[i].Stdev(MetricMeans[i]);
            }

            using (var writer = new StreamWriter(@"MetricsStats.tsv", false, Encoding.UTF8))
            {
                for (int i = 0; i < MetricMeans.Length; i++)
                {
                    writer.WriteLine(MetricMeans[i] + "\t" + MetricStdevs[i]);
                    foreach (var element in RawMetricsCollections[i])
                    {
                        writer.Write(element + "\r\n");
                    }
                }
            } 
            
            //Normalize metrics and write back.
            for (int i = 0; i < listOfArraysOfLDAConfigFiles.Count; i++)
            {
                NormalizeMetrics(listOfArraysOfLDAConfigFiles[i], MetricMeans, MetricStdevs, (int)(1.5 * NumOfThreads));
            }
        }

        private static void NormalizeMetrics(string[] arrayOfLDAConfigFiles, float[] metricMeans, float[] metricStdevs, int numOfThreads)
        {
            Parallel.ForEach(arrayOfLDAConfigFiles, new ParallelOptions { MaxDegreeOfParallelism = numOfThreads }, configFile =>
                {
                    string metricsFile = GetMetricsFileFromConfigFile(configFile);

                    if (File.Exists(metricsFile) && !FileManager.IsFileLocked(metricsFile))
                    {
                        StatusMessage.Write("Processing metrics " + metricsFile);
                        NormalizeMetrics(metricsFile, metricMeans, metricStdevs);
                    }
                });          
        }

        private static void NormalizeMetrics(string metricsFile, float[] metricMeans, float[] metricStdevs)
        {            
            StringBuilder modelSummary = new StringBuilder();
            bool foundRawMetrics = false;
            List<Topic> topics = new List<Topic>();
            float totalAllocations = 0.0f;
            long totalPromimentDF = 0L;
            int numTopics = 0;
            int goodTopicCount = 0;

            foreach (var line in File.ReadAllLines(metricsFile))
            {
                if (!foundRawMetrics)
                {
                    // Read first part.                    
                    if (!line.StartsWith("Topic Id"))
                    {
                        modelSummary.AppendLine(line.Replace("NaN", "0"));
                        var pairs = line.Split('\t');
                        if (pairs[0].ToLowerInvariant() == "topics")
                        {
                            numTopics = int.Parse(pairs[1]);
                        }
                        else if (pairs[0].ToLowerInvariant() == "good topics")
                        {
                            goodTopicCount = int.Parse(pairs[1]);
                        }
                    }
                    else
                    {                        
                        foundRawMetrics = true;
                    }
                    continue;
                }

                var parts = line.Split('\t');
                if (parts[1] == "1")
                {
                    // Skip bad topics.
                    break;
                }

                // Cope with wierd case that "TopDocs" column contains two double quotes.
                // e.g. \\br1iceml001\ModelRepository\Models\LDA\en-us\msn\20150212_250.tsv\500_45_0.5_0.1_0.2_256_2_1_0.5\build\ExtrinsicMetrics.tsv
                if (parts[7].IndexOf('\"') >= 0)
                {
                    parts[7] = parts[7].Replace("\"", "");
                }

                // Read second part and reconstruct each topic
                List<Tuple<int, double>> promDocs;
                if (parts[6] == "0")
                {
                    promDocs = new List<Tuple<int, double>>();
                }
                else
                {
                    if (parts[7].IndexOf('|') >= 0)
                    {
                        promDocs = parts[7].Split(',')
                                        .Select(p => new Tuple<int, double>(int.Parse(p.Split('|')[0]), double.Parse(p.Split('|')[1])))
                                        .ToList();
                    }
                    else
                    { 
                        promDocs = parts[7].Split(',')
                                    .Select(p => new Tuple<int, double>(int.Parse(p), -1.0))
                                    .ToList();                                    
                    }
                }
                Topic t = new Topic()
                {
                    TopicId = int.Parse(parts[0]),
                    IsBad = parts[1] == "1",
                    Allocations = float.Parse(parts[2]),
                    TC = float.Parse(parts[3]),
                    TS = (parts[4] == "NaN" ? 0.0f : float.Parse(parts[4])),
                    TD = float.Parse(parts[5]),
                    PromimentDF = long.Parse(parts[6]),
                    TopProminentDocuments = promDocs,
                    HighProbWords = parts.Skip(8).ToList()
                };

                // Compute the normalized TC/TS/TD for each topic
                t.NormalizedTC = (t.TC - metricMeans[0]) / metricStdevs[0];
                t.NormalizedTS = (t.TS - metricMeans[1]) / metricStdevs[1];
                t.NormalizedTD = (t.TD - metricMeans[2]) / metricStdevs[2];

                totalAllocations += t.Allocations;
                totalPromimentDF += t.PromimentDF;

                topics.Add(t);
            }            

            // Add "Bad Topic Count" to modelSummary
            int badTopicCount = topics.Count(t => t.TC < ThresholdForBadTopics);
            modelSummary.AppendFormat("Bad Topic Count\t{0}\r\n", badTopicCount);
            modelSummary.AppendFormat("Good Topics(%)\t{0}\r\n", (float)goodTopicCount / numTopics);
            modelSummary.AppendFormat("Actual Good Topics\t{0}\r\n", goodTopicCount - badTopicCount);
            // todo: add other model summary statistics here (need to modify metrics list in Model Loader as well).

            DenseVector normalizedTCVector = new DenseVector(topics.Select(t => t.NormalizedTC).ToArray());
            DenseVector normalizedTSVector = new DenseVector(topics.Select(t => t.NormalizedTS).ToArray());
            DenseVector normalizedTDVector = new DenseVector(topics.Select(t => t.NormalizedTD).ToArray());

            DenseVector weightsByAlloc = new DenseVector(topics.Select(t => t.Allocations / totalAllocations * topics.Count).ToArray());
            DenseVector weightsByPromDF = new DenseVector(topics.Select(t => (float)t.PromimentDF / totalPromimentDF * topics.Count).ToArray());

            double[,] cumulativeNormalizedMetricsMatrix = new double[3, 3];
            // TC
            cumulativeNormalizedMetricsMatrix[0, 0] = VectorBase.DotProduct(normalizedTCVector, weightsByAlloc);
            cumulativeNormalizedMetricsMatrix[0, 1] = VectorBase.DotProduct(normalizedTCVector, weightsByPromDF);
            cumulativeNormalizedMetricsMatrix[0, 2] = topics.Sum(t => (double)t.NormalizedTC);

            // TS
            cumulativeNormalizedMetricsMatrix[1, 0] = VectorBase.DotProduct(normalizedTSVector, weightsByAlloc);
            cumulativeNormalizedMetricsMatrix[1, 1] = VectorBase.DotProduct(normalizedTSVector, weightsByPromDF);
            cumulativeNormalizedMetricsMatrix[1, 2] = topics.Sum(t => (double)t.NormalizedTS);

            // TD
            cumulativeNormalizedMetricsMatrix[2, 0] = VectorBase.DotProduct(normalizedTDVector, weightsByAlloc);
            cumulativeNormalizedMetricsMatrix[2, 1] = VectorBase.DotProduct(normalizedTDVector, weightsByPromDF);
            cumulativeNormalizedMetricsMatrix[2, 2] = topics.Sum(t => (double)t.NormalizedTD);

            // append the 3x3 matrix elements to modelSummary, save as "ExtrinsicMetrics.processed.tsv"
            RegenerateModelSynopsis(metricsFile, modelSummary, topics, cumulativeNormalizedMetricsMatrix);              
        }

        private static void RegenerateModelSynopsis(string metricsFile, StringBuilder modelSummary, List<Topic> topics, double[,] cumulativeNormalizedMetricsMatrix)
        {
            string modelSynopsisFilename = Path.Combine(Directory.GetParent(metricsFile).FullName, "ExtrinsicMetrics.processed.tsv");

            StatusMessage.Write("Regenerating Model Synopsis...");

            using (var writer = new StreamWriter(modelSynopsisFilename, false, Encoding.UTF8))
            {
                writer.Write(modelSummary);
                writer.WriteLine("Cum Allocation Weighted Normalized TC\t{0}", cumulativeNormalizedMetricsMatrix[0, 0]);
                writer.WriteLine("Cum Allocation Weighted Normalized TS\t{0}", cumulativeNormalizedMetricsMatrix[1, 0]);
                writer.WriteLine("Cum Allocation Weighted Normalized TD\t{0}", cumulativeNormalizedMetricsMatrix[2, 0]);

                writer.WriteLine("Cum ProminentDF Weighted Normalized TC\t{0}", cumulativeNormalizedMetricsMatrix[0, 1]);
                writer.WriteLine("Cum ProminentDF Weighted Normalized TS\t{0}", cumulativeNormalizedMetricsMatrix[1, 1]);
                writer.WriteLine("Cum ProminentDF Weighted Normalized TD\t{0}", cumulativeNormalizedMetricsMatrix[2, 1]);

                writer.WriteLine("Cum Unweighted Normalized TC\t{0}", cumulativeNormalizedMetricsMatrix[0, 2]);
                writer.WriteLine("Cum Unweighted Normalized TS\t{0}", cumulativeNormalizedMetricsMatrix[1, 2]);
                writer.WriteLine("Cum Unweighted Normalized TD\t{0}", cumulativeNormalizedMetricsMatrix[2, 2]);

                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}", "Topic Id", "isBad", "Allocations", "Coherence", "Specificity", "Distinctiveness", "Normalized TC", "Normalized TS", "Normalized TD", "Prominent DF", "Top Docs", "High probability words");

                foreach (var t in topics)
                {
                    writer.WriteLine(t);
                }
            }
        }

        private static void PopulateRawMetricsCollections(BlockingCollection<float>[] rawMetricsCollections, string[] arrayOfLDAConfigFiles, int numOfThreads)
        {            
            Parallel.ForEach(arrayOfLDAConfigFiles, new ParallelOptions { MaxDegreeOfParallelism = numOfThreads }, configFile =>
            {
                string metricsFile = GetMetricsFileFromConfigFile(configFile);

                if (File.Exists(metricsFile))
                {
                    if (FileManager.IsFileLocked(metricsFile))
                    {
                        StatusMessage.Write("Metrics file is locked: " + metricsFile, ConsoleColor.Yellow);
                    }
                    else
                    {
                        // Read through to find raw metrics {TC, TS, TD}
                        List<float>[] rawMetrics = ExtractRawMetrics(metricsFile);

                        Thread.Sleep(Helper.RandomNumber(0, 2500));
                        StatusMessage.Write("Reading raw metrics from " + metricsFile);
                        // Add raw metrics to collections.
                        lock (thisLock)
                        {
                            for (int i = 0; i < rawMetricsCollections.Length; i++)
                            {
                                foreach (var element in rawMetrics[i])
                                {
                                    rawMetricsCollections[i].Add(element);
                                }
                            }
                        }                     
                    }
                }
            });
        }

        private static List<float>[] ExtractRawMetrics(string metricsFile)
        {
            List<float>[] rawMetrics = new List<float>[3];
            for (int i = 0; i < rawMetrics.Length; i++)
            {
                rawMetrics[i] = new List<float>();
            }

            bool foundRawMetrics = false;
            StreamReader sr = new StreamReader(metricsFile);
            do
            {
                string line = sr.ReadLine();
                if (line == null)
                    break;

                if (!foundRawMetrics)
                {
                    if (line.StartsWith("Topic Id"))
                    {
                        foundRawMetrics = true;
                    }
                    continue;
                }

                var parts = line.Split('\t');
                if (parts[1] == "1")
                    break;                

                // parts[3] is TC, parts[4] is TS, parts[5] is TD.
                for (int i = 0; i < rawMetrics.Length; i++)
                {                                      
                    rawMetrics[i].Add((parts[i + 3] == "NaN" ? 0.0f : Single.Parse(parts[i + 3])));
                }

            } while (true);

            sr.Close();

            return rawMetrics;
        }



        private static string[] Initialize(string[] args)
        {
            NumOfThreads = int.Parse(args[0]);
            ThresholdForBadTopics = float.Parse(args[1]);
        
            string[] ModelRepositoriesForMetrics = ConfigurationManager.AppSettings["ModelRepositoriesForMetrics"].Split(',');

            var overrideModelRepository = args.ElementAtOrDefault(2);
            if (!string.IsNullOrEmpty(overrideModelRepository))
            {
                ModelRepositoriesForMetrics = new[] { overrideModelRepository };
            }

            RawMetricsCollections = new BlockingCollection<float>[3];
            for (int i = 0; i < RawMetricsCollections.Length; i++)
            {
                RawMetricsCollections[i] = new BlockingCollection<float>();
            }

            MetricMeans = new float[3];
            MetricStdevs = new float[3];

            return ModelRepositoriesForMetrics;
        }

        private static string GetMetricsFileFromConfigFile(string configFile)
        {
            var parent = Directory.GetParent(configFile);
            return Path.Combine(parent.FullName, @"build\ExtrinsicMetrics.tsv");
        }

    }
}
