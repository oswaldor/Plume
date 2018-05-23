using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Configuration;
using Newtonsoft.Json;

using Microsoft.Content.Recommendations.Common;
using WrapperForFastLDATuning;

namespace Microsoft.Content.TopicExtraction
{
    class Program
    {

        public static readonly string ProjResourceSubFolder = "TuneLDAParameters";

        public static readonly string LDALearningConfigsSubFolder = "LDALearningConfigFiles";

        public static readonly string RangeOfParamsSubFolder = "RangesOfParams";

        private const string DetailUsage =
            "\r\n"
            + "Command: Train multiple LDA models and compute intrinsic/extrinsic metrics\r\n"
            + "    WorkerRole    --   the role of the worker: \"training\" or \"metrics\".\r\n"
            + "    NumOfThreads  --   the number of threads for this worker, depending on the resources (CPUs, RAM) of your machine.\r\n"
            + "    MetricsType   --   the metrics you want to compute: \"intr\", \"extr\" or \"both\" (default). \r\n"
            + "    All           --   Assumes all the models are generated and calculates the metrics for all the models (by default it loops infinately, waiting for models). \r\n"
            + "  Examples:\r\n"
            + "    WrapperForFastLDATuning  training  6\r\n"
            + "    WrapperForFastLDATuning  metrics   4\r\n"
            + "    WrapperForFastLDATuning  metrics   10  extr\r\n"
            + "    WrapperForFastLDATuning  metrics   10  extr  all\r\n";

        public static string ModelRepositoryPath;

        public static string DefaultModelConfigFile;

        /// <summary>
        /// training sample for LDA (.tsv)
        /// </summary>
        public static string TrainingSampleName;

        /// <summary>
        /// test sample for LDA (.tsv)
        /// </summary>
        public static string TestSampleName;

        // Below variables for inter-thread communication.
        private static long flag;
        private static ConcurrentQueue<string> configFileQueue;

		public static WorkerRoles WorkerRole;

        public static int NumOfThreads;

        /// <summary>
        /// the type of metrics to be computed. Options are: Intr(insic), Extr(insic), or Both.
        /// </summary>
        public static ModelMetricTypes MetricsType;

		public static bool NeedCopyToRemote;

        /// <summary>
        /// Boolean variable indicating if we should delete models once they are successfully copied to remote.
        /// </summary>
        public static bool NeedDeleteFromLocal;

        /// <summary>
        /// The remote model repository (file share).
        /// </summary>
		public static string RemoteModelRepositoryPath;

        /// <summary>
        /// Boolean variable indicating if we need to load (existing) LDA config files from a given location.
        /// true if we want to load them,
        /// false if we want to generate new config files by combination of different parameters.
        /// </summary>
        public static bool NeedLoadLDAConfigFiles;
        public static string SourceFolderOfLDAConfigFiles;

        public static bool NeedLoadLDAParameterTable;
        public static string LDAParameterTablePath;
        
        public static bool RunAllMetrics;

        public static bool NeedDeleteConfig;

        public static void Main(string[] args)
        {
            if (args.Length < 2)
			{
                System.Console.WriteLine("Usage: WrapperForFastLDATuning.exe <WorkerRole> <NumOfThreads> [MetricsType]");
                System.Console.WriteLine(DetailUsage);
                Environment.Exit(1);
            }

            if (!Enum.TryParse(args[0], true, out WorkerRole))
            {
                StatusMessage.Write("Unrecognized value for worker role. Exiting...");
                return;
            }

            if (!Int32.TryParse(args[1], out NumOfThreads))
            {
                StatusMessage.Write("Invalid value for number of threads. Exiting...");
                return;
            }

            if (WorkerRole == WorkerRoles.Metrics)
            {
                if (args.Length < 3)
                {
                    // By default we compute both intrinsic and extrinsic metrics.
                    MetricsType = ModelMetricTypes.Both;
                }
                else if (!Enum.TryParse(args[2], true, out MetricsType))
                {
                    StatusMessage.Write("Unrecognized value for metrics type. Exiting...");
                    return;
                }

                if (args.Length > 3)
                {
                    RunAllMetrics = args[3].Equals("all", StringComparison.OrdinalIgnoreCase);
                }
            }

			Initialize();

            // Load default LDA config as seed
            var defaultLDAConfig = LoadLDAConfig(DefaultModelConfigFile);

            // Get the folders of parameter range files and training config files.
            // Examples of folder structure: d:\ModelRepository\TuneLDAParameters\RangesOfParams
            //                               d:\ModelRepository\TuneLDAParameters\LDALearningConfigFiles
            string folderOfParamRangeFiles = Path.Combine(ModelRepositoryPath, ProjResourceSubFolder, RangeOfParamsSubFolder);
            string folderOfTrainingConfigFiles = Path.Combine(ModelRepositoryPath, ProjResourceSubFolder, LDALearningConfigsSubFolder);

            List<string> listOfLDAConfigFilesForFeaturization = new List<string>();
            List<string> listOfLDAConfigFilesForTest = new List<string>();

            // Generate multiple LDA configs for featurization, training and test.
            List<string> listOfLDAConfigFilesForTrain;
            if (NeedLoadLDAConfigFiles)
            {
                listOfLDAConfigFilesForTrain =
                        LDAConfigFileGenerator.LoadLDAConfigFiles(ModelRepositoryPath,
                                                                  TrainingSampleName,
                                                                  defaultLDAConfig,
                                                                  SourceFolderOfLDAConfigFiles,
                                                                  folderOfTrainingConfigFiles,
                                                                  ref listOfLDAConfigFilesForFeaturization,
                                                                  ref listOfLDAConfigFilesForTest);
            }
            else if (NeedLoadLDAParameterTable)
            {
                listOfLDAConfigFilesForTrain =
                        LDAConfigFileGenerator.LoadLDAParameterTable(ModelRepositoryPath, 
                                                                  TrainingSampleName,
                                                                  defaultLDAConfig,
                                                                  LDAParameterTablePath,
                                                                  folderOfTrainingConfigFiles,
                                                                  ref listOfLDAConfigFilesForFeaturization,
                                                                  ref listOfLDAConfigFilesForTest);
            }
            else
            {
                listOfLDAConfigFilesForTrain =
                    LDAConfigFileGenerator.GenerateLDAConfigFiles(ModelRepositoryPath,
                                                                  folderOfParamRangeFiles,
                                                                  TrainingSampleName,
                                                                  defaultLDAConfig,
                                                                  folderOfTrainingConfigFiles,
                                                                  ref listOfLDAConfigFilesForFeaturization,
                                                                  ref listOfLDAConfigFilesForTest);
            }

            switch (WorkerRole)
			{
				case WorkerRoles.Training:
                    FeaturizeSample(listOfLDAConfigFilesForFeaturization, TrainingSampleName, NumOfThreads);
                    if (NeedCopyToRemote)
                    {
                        CopyVocabularies(listOfLDAConfigFilesForTest.First(), RemoteModelRepositoryPath);

                        // Start a thread that:
                        // 1). monitors model directory;
                        // 2). copies models to remote model repository when they are done;
                        // 3). deletes them once copy is successful.
                        Thread newThread = new Thread(Program.CopyModels);
                        newThread.Start(NeedDeleteFromLocal);
                    }
                    TrainLDAModels(listOfLDAConfigFilesForTrain, listOfLDAConfigFilesForTest, NumOfThreads);
                    if (NeedCopyToRemote)
                    {
                        WaitForCopyThread();
                    }
                    break;

                case WorkerRoles.Metrics:
			        if (RunAllMetrics)
			        {
			            long numOfModelsMeasured = 0;
                        ComputeMetrics(listOfLDAConfigFilesForTest, NumOfThreads, MetricsType, ref numOfModelsMeasured);
			            break;
			        }

                    // Get common parent of individual model directories.
                    string commonParentOfModelDirectories = FileManager.GetGrandparentOfFilePath(listOfLDAConfigFilesForTest.First());
                    ComputeMetrics(commonParentOfModelDirectories, NumOfThreads, MetricsType);
                    break;

				default:
                    return;
			}

            StatusMessage.Write("Done!");
        }

        /// <summary>
        /// Initialization
        /// </summary>
        private static void Initialize()
        {
            ModelRepositoryPath = ConfigurationManager.AppSettings["LocalModelRepository"];

            DefaultModelConfigFile = ConfigurationManager.AppSettings["DefaultModelConfig"];

            TrainingSampleName = ConfigurationManager.AppSettings["TrainingSampleName"];

            TestSampleName = ConfigurationManager.AppSettings["TestSampleName"];

            flag = 1;

            configFileQueue = new ConcurrentQueue<string>();

			NeedCopyToRemote = Boolean.Parse( ConfigurationManager.AppSettings["NeedCopyToRemote"] );
            NeedDeleteFromLocal = Boolean.Parse(ConfigurationManager.AppSettings["NeedDeleteFromLocal"]);
			if (NeedCopyToRemote)
			{
				RemoteModelRepositoryPath = ConfigurationManager.AppSettings["RemoteModelRepository"];
			}

            NeedLoadLDAConfigFiles = Boolean.Parse(ConfigurationManager.AppSettings["NeedLoadLDAConfigFiles"]);
            if (NeedLoadLDAConfigFiles)
            {
                SourceFolderOfLDAConfigFiles = ConfigurationManager.AppSettings["FolderOfLDALearningConfigFiles"];
            }

            NeedLoadLDAParameterTable = Boolean.Parse(ConfigurationManager.AppSettings["NeedLoadLDAParameterTable"]);
            if (NeedLoadLDAParameterTable)             
            {
                LDAParameterTablePath = ConfigurationManager.AppSettings["LDAParameterTablePath"];
            }

            NeedDeleteConfig = Boolean.Parse(ConfigurationManager.AppSettings["NeedDeleteConfig"]);
        }

        /// <summary>
        /// This is an indicator function used to sync between training thread(s) and copy thread.
        /// </summary>
        /// <returns></returns>
        private static bool IsRunning()
        {
            return (Interlocked.Read(ref flag) == 1 || configFileQueue.Count > 0);
        }

        /// <summary>
        /// Compute corpus vocabulary, document vocabularies, and featurized documents
        /// for each combination of min/max(relative) word document frequency.
        /// </summary>
        /// <param name="listOfLDAConfigFilesForFeaturization">a list of LDAConfig files for featurization.</param>
        /// <param name="sampleName">the name of the sample to be featurized</param>
        private static void FeaturizeSample(List<string> listOfLDAConfigFilesForFeaturization, string sampleName, int numOfThreads)
        {
            var tuner = new LDAParameterTuner("featurizedocs", sampleName, listOfLDAConfigFilesForFeaturization.First());
            tuner.Run();

            Parallel.ForEach(listOfLDAConfigFilesForFeaturization.Skip(1), new ParallelOptions { MaxDegreeOfParallelism = numOfThreads*2/3 }, config =>
                {
                    tuner = new LDAParameterTuner("featurizedocs", sampleName, config);
                    tuner.Run();
                });
        }

        private static void TrainLDAModels(List<string> listOfLDAConfigFilesForTrain, List<string> listOfLDAConfigFilesForTest, int numOfThreads)
		{
            // Generate a list of indexes
            // for accessing each element of listOfLDAConfigFilesForTraining and listOfLDAConfigFilesForTest
            List<int> indexes = Enumerable.Range(0, listOfLDAConfigFilesForTrain.Count).ToList();

            long totalNumber = listOfLDAConfigFilesForTrain.Count;
            long numOfModelsTrained = 0;

            // Spawn multiple threads for training
            Parallel.ForEach(indexes, new ParallelOptions { MaxDegreeOfParallelism = numOfThreads }, index =>
            {
                string configFileForTrain = listOfLDAConfigFilesForTrain[index];
                string configFileForTest = listOfLDAConfigFilesForTest[index];
                var tuner = new LDAParameterTuner("learnlda", TrainingSampleName, configFileForTrain, configFileForTest);
                int retVal = tuner.Run();

                if (retVal == 0)
                {
                    // Push the configFileForTest into queue once the model training is done.
                    configFileQueue.Enqueue(configFileForTest);

                    Interlocked.Increment(ref numOfModelsTrained);
                    StatusMessage.Write(string.Format("Model #{0} out of {1} has been trained.", Interlocked.Read(ref numOfModelsTrained), totalNumber), ConsoleColor.Green);

                    if (NeedDeleteConfig)
                    {
                        ConsoleColor color;
                        var message = FileManager.DeleteFile(configFileForTrain, out color);
                        StatusMessage.Write(message);
                    }
                }
                else
                {
                    StatusMessage.Write("Learn LDA failed! config = " + configFileForTrain, ConsoleColor.Red);
                }
            });

            // Tell the copy thread that no more models to be generated.
            Interlocked.Decrement(ref flag);
		}

        private static void ComputeMetrics(string commonParentOfModelDirs, int numOfThreads, ModelMetricTypes metricsType)
        {
            long i = 0;
            long oneHour = 60 * 60 * 1000L;
            long numOfModelsMeasured = 0;

            while (true)
            {
                int numOfModelsDemanded = 0;
                // Find all LDAConfig files (.json)
                List<string> listOfLDAConfigFilesForTest = FileManager.SearchFileInDir(commonParentOfModelDirs, "*.LDAConfig.json");
                do
                {
                    // Find the models that are ready and need to compute metrics.
                    var configFilesReady = listOfLDAConfigFilesForTest.Where(configFile =>
                                                                        LDAModelStatusChecker.AreModelFilesReady(configFile) &&
                                                                        !LDAModelStatusChecker.HaveMetricsBeenComputed(configFile, TestSampleName, metricsType)).ToList();

                    if (configFilesReady.Count() == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    ComputeMetrics(configFilesReady, numOfThreads, metricsType, ref numOfModelsMeasured);

                    Thread.Sleep(1);

                    // Find the number of models that demand computation of metrics.
                    numOfModelsDemanded = listOfLDAConfigFilesForTest.Count(configFile => !LDAModelStatusChecker.HaveMetricsBeenComputed(configFile, TestSampleName, metricsType));

                } while (numOfModelsDemanded > 0);

                Thread.Sleep(1);
                if (i++ % oneHour == 0)
                {
                    // Display the message when the metrics thread has been idle for every hour.
                    StatusMessage.Write("Waiting for models to be ready...");
                }
            }
        }

        private static void ComputeMetrics(List<string> configFilesReady, int numOfThreads, ModelMetricTypes metricsType, ref long totalNumOfModelsMeasured)
        {
            long numOfModelsMeasured = Interlocked.Read(ref totalNumOfModelsMeasured);

            // Group LDAConfig files by featurization parameters, i.e. <min, max>.
            var groupsOfLDAConfigFiles = configFilesReady.GroupBy(f => new
            {
                ExtractFeaturizationParameters(f).MinWordDocumentFrequency,
                ExtractFeaturizationParameters(f).MaxRalativeWordDocumentFrequency
            }).ToArray();

            // Compute metrics for very first item to generate DocumentVocabularies in case of need.
            ComputeMetrics(groupsOfLDAConfigFiles.First().First(), metricsType);
            
            int numOfThreadsPerGroup = (int)(Math.Ceiling((decimal)numOfThreads / groupsOfLDAConfigFiles.Length));

            // Run multi-threading over different groups, then run single-thread within each group.
            Parallel.ForEach(groupsOfLDAConfigFiles, new ParallelOptions {MaxDegreeOfParallelism = numOfThreads},
                        groupOfConfigFiles =>
                        {
                            long groupSize = groupOfConfigFiles.Count();
                            ComputeMetrics(groupOfConfigFiles, metricsType, numOfThreadsPerGroup);

                            Interlocked.Add(ref numOfModelsMeasured, groupSize);
                            StatusMessage.Write(
                                string.Format("Metrics of model #{0} computed.", Interlocked.Read(ref numOfModelsMeasured)),
                                ConsoleColor.Green);

                        });
        }

        /// <summary>
        /// Compute metrics for each config file within a group.
        /// </summary>
        /// <param name="groupOfConfigFiles"></param>
        private static void ComputeMetrics(IGrouping<object, string> groupOfConfigFiles, ModelMetricTypes metricsType, int numOfThreadsPerGroup=1)
        {
            long count = 0;
            int totalCount = groupOfConfigFiles.Count();
            Parallel.ForEach(groupOfConfigFiles, new ParallelOptions { MaxDegreeOfParallelism = numOfThreadsPerGroup },
                configFile =>
                {
                    Interlocked.Increment(ref count);
                    StatusMessage.Write(string.Format("Computing metrics for model #{0} of {1} within group {2},\r\n\tunder {3}\r\n",
                        Interlocked.Read(ref count),
                        totalCount,
                        groupOfConfigFiles.Key,
                        Path.GetDirectoryName(configFile)));

                    ComputeMetrics(configFile, metricsType);
                });
        }



        private static void ComputeMetrics(string configFileForTest, ModelMetricTypes metricsType)
        {
            // Compute metrics.
            bool needComputePerplexity = (metricsType == ModelMetricTypes.Intr ||
                                          metricsType == ModelMetricTypes.Both);
            var tuner = new LDAParameterTuner("getmetrics", TestSampleName, configFileForTest, "", needComputePerplexity);
            tuner.Run();
            
            ConsoleColor color;
            FileManager.DeleteFile(Path.Combine(Path.GetDirectoryName(configFileForTest), @"build\DocumentTopicAllocations.txt"), out color);
        }

        /// <summary>
        /// Copy model files from current model repository to remote model repository (file share).
        /// </summary>
        private static void CopyModels(object needDeleteModel)
        {
            bool needDeleteFromLocal;
            if (!bool.TryParse(needDeleteModel.ToString(), out needDeleteFromLocal))
            {
                throw new Exception("Invalid value for boolean flag\"needDeleteModel\".");
            }

            string currentConfigFileForTest = "";
            while (IsRunning())
            {
                string configFileForTest;

                if (configFileQueue.TryPeek(out configFileForTest))
                {
                    // Make sure model files are ready.
                    while (!LDAModelStatusChecker.AreModelFilesReady(configFileForTest))
                    {
                        Thread.Sleep(5);
                        if (configFileForTest != currentConfigFileForTest)
                        {
                            StatusMessage.Write("Waiting for model under\r\n\t" + Path.GetDirectoryName(configFileForTest));
                            currentConfigFileForTest = configFileForTest;
                        }
                    }

                    if (configFileQueue.TryDequeue(out configFileForTest))
                    {
                        string sourceDir = Path.GetDirectoryName(configFileForTest);

                        string destinationDir = GetDestinationPathFromSourcePath(sourceDir, RemoteModelRepositoryPath);

                        StatusMessage.Write(string.Format("Copying model under\r\n\t{0}-->{1}", sourceDir, RemoteModelRepositoryPath), ConsoleColor.DarkGreen);
                        string message = FileManager.CopyDirectoryOrFile(sourceDir, destinationDir);
                        StatusMessage.Write(message, ConsoleColor.Green);
                        int retVal = int.Parse(message.Split('\t')[0]);

                        if (retVal == 0 && needDeleteFromLocal)
                        {
                            StatusMessage.Write(string.Format("Deleting directory\r\n\t{0}", sourceDir), ConsoleColor.DarkGreen);
                            ConsoleColor color;
                            message = FileManager.DeleteDirectory(sourceDir, out color);
                            StatusMessage.Write(message, color);
                        }
                    }
                }
            }

            // Tell main thread that all models listed in the queue have been copied.
            Interlocked.Decrement(ref flag);
        }

        /// <summary>
        /// Generate doc vector given a LDA config file.
        /// </summary>
        /// <param name="configFileForTest"></param>
        private static void GenerateDV(string configFileForTest)
        {
            // Run DvGen for training corpus.
            LDAConfig ldaConfig;
            try
            {
                ldaConfig = JsonConvert.DeserializeObject<LDAConfig>(File.ReadAllText(configFileForTest));
            }
            catch (Exception)
            {
                throw;
            }
            string corpusPrefix = Path.Combine(ModelRepositoryPath, string.Format(@"Corpora\{0}\{1}", ldaConfig.Locale, ldaConfig.Corpus));
            string sampleFileFullPath = Path.Combine(corpusPrefix, TrainingSampleName);
            var tuner = new LDAParameterTuner("generatedocvectors", sampleFileFullPath, configFileForTest);
            tuner.Run();

            // Copy dv to model directory.
            string src = Path.Combine(corpusPrefix,
                string.Format("{0}.dv", TrainingSampleName),
                TrainingSampleName + "." + ldaConfig.modelName,
                "DocumentVectors.L1.dv");
            string dest = ldaConfig.DocumentTopicAllocations;

            StatusMessage.Write(string.Format("Waiting DV file ready: {0}", src));
            do
            {
                Thread.Sleep(5);
            } while (!File.Exists(src) || FileManager.IsFileLocked(src));

            StatusMessage.Write(string.Format("DV file ready. Copying from {0} to {1}", src, dest));
            try
            {
                File.Copy(src, dest, true);
            }
            catch (Exception)
            {
                throw;
            }

            StatusMessage.Write(string.Format("Successfully copied file\r\n<---{0}\r\n--->{1}", src, dest));
        }

        /// <summary>
        /// Copy vocabulary files from (current) model repository to remote model repository.
        /// </summary>
        /// <param name="ldaConfigFilesForTest">a LDA config file for </param>
        /// <param name="remoteModelRepositoryPath">the path of remote model repository</param>
        private static void CopyVocabularies(string ldaConfigFilesForTest, string remoteModelRepositoryPath)
        {
            // Get common parent of individual model directories.
            string commonParentOfModelDirectories = FileManager.GetGrandparentOfFilePath(ldaConfigFilesForTest);

            // Get source vocabulary directory.
            string sourceVocabDir = Path.Combine(commonParentOfModelDirectories, "CorpusVocabulary");

            // Get destination vocabulary directory.
            string destinationVocabDir = GetDestinationPathFromSourcePath(sourceVocabDir, remoteModelRepositoryPath);

            StatusMessage.Write(string.Format("Copying vocabulary files from {0} to {1} ...", sourceVocabDir, destinationVocabDir), ConsoleColor.Green);
            string message = "";

            // Copy file "CorpusVocabulary.raw.tsv" if it does not exist in destinationVocabDir.
            if (!File.Exists(Path.Combine(destinationVocabDir, "CorpusVocabulary.raw.tsv")))
            {
                message += FileManager.CopyDirectoryOrFile(Path.Combine(sourceVocabDir, "CorpusVocabulary.raw.tsv"), destinationVocabDir, 'F') + "\r\n";
            }
            else
            {
                message += "Skipping file \"CorpusVocabulary.raw.tsv\"\r\n";
            }

            // Copy file "DocumentVocabularies.txt" if it does not exist in destinationVocabDir.
            if (!File.Exists(Path.Combine(destinationVocabDir, "DocumentVocabularies.txt")))
            {
                message += FileManager.CopyDirectoryOrFile(Path.Combine(sourceVocabDir, "DocumentVocabularies.txt"), destinationVocabDir, 'F') + "\r\n";
            }
            else
            {
                message += "Skipping file \"DocumentVocabularies.txt\"\r\n";
            }

            // Copy each subdirectory.
            foreach (var sourceSubDir in Directory.GetDirectories(sourceVocabDir))
            {
                string subDirName = new DirectoryInfo(sourceSubDir).Name;
                string destinationSubDir = Path.Combine(destinationVocabDir, subDirName);
                // check if
                // 1). destination subdirectory exists;
                // 2). the content of source subdirectory is a subset of the content of destination subdirectory.
                if (!Directory.Exists(destinationSubDir) || !FileManager.IsSubsetOf(sourceSubDir, destinationSubDir))
                {
                    message += FileManager.CopyDirectoryOrFile(sourceSubDir, destinationSubDir, 'D') + "\r\n";
                }
                else
                {
                    message += string.Format("Skipping subdirectory \"{0}\"\r\n", subDirName);
                }
            }
            StatusMessage.Write(message, ConsoleColor.Green);
        }

        /// <summary>
        /// Get destination path (likely on remote computer) from source path.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="remoteModelRepositoryPath">the path of remote Model Repository</param>
        /// <returns></returns>
        private static string GetDestinationPathFromSourcePath(string sourcePath, string remoteModelRepositoryPath)
        {
            int index = sourcePath.IndexOf("ModelRepository\\");
            if (index < 0)
                throw new Exception("Unexpected situation: string 'ModelRepository' is not found in the source path.");

            // Get destination path.
            string destinationPath = Path.Combine(remoteModelRepositoryPath, sourcePath.Substring(index + "ModelRepository\\".Length));

            return destinationPath;
        }

        private static void WaitForCopyThread()
        {
            StatusMessage.Write("Waiting for copy thread to finish ...", ConsoleColor.Green);
            do
            {
                Thread.Sleep(1);
            } while (Interlocked.Read(ref flag) >= 0);
        }
        public static LDAConfig LoadLDAConfig(string modelConfigFile)
        {
            var ldaConfig = JsonConvert.DeserializeObject<LDAConfig>(File.ReadAllText(modelConfigFile));
            return ldaConfig;
        }

        /// <summary>
        /// Extract featurization parameters (min, max) from a LDAConfig file path.
        /// </summary>
        /// <param name="ldaConfigFilePath"></param>
        /// <returns></returns>
        private static FeaturizationParameters ExtractFeaturizationParameters(string ldaConfigFilePath)
        {
            var fileName = Path.GetFileName(ldaConfigFilePath);

            var parts = fileName.Split('_');
            var minDF = Int32.Parse(parts[1]);
            var maxDF = Single.Parse(parts[2]);

            return new FeaturizationParameters()
            {
                MinWordDocumentFrequency = minDF,
                MaxRalativeWordDocumentFrequency = maxDF
            };
        }
    }
}
