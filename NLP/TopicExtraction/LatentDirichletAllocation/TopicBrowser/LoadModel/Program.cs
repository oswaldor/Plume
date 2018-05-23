
namespace Microsoft.Content.Recommendations.Common
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Content.Recommendations.LinearAlgebra;
    using Microsoft.Content.TopicExtraction;
    using Newtonsoft.Json;

    using TopicModelDbLib;

    class Program
    {
        private const string DetailUsage =
            "\r\n"
            + "Command: Load - Loads one single Extrinsict metrics file to TopicModels db\r\n"
            + "  Options:\r\n"
            + "    Config       LDA config file of model whose metrics we are loading\r\n"
            + "    ServerName   (Optional) SQL Server name. Can be especified via app.config. Default=oswaldo-server2.\r\n"
            + "    DbName       (Optional) Can be specified via app.config. Default=TopicModels.\r\n"
            + "  Example:   LodaModel.exe Load Config=c:\\models\\ldaConfig.json\r\n"
            + "     \r\n"
            + "\r\n"
            + "Command: LoadAll - Recursive walks a folder, finds metrics files and loads them to the db. \r\n"
            + "    ModelRepository       Directory containing multiple model metric files\r\n"
            + "    ServerName   (Optional) SQL Server name. Can be especified via app.config. Default=oswaldo-server2.\r\n"
            + "    DbName       (Optional) Can be specified via app.config. Default=TopicModels.\r\n"
            + "    dest         (Optional) destination where you want to save your models/metrics, either \"sqldb\"(default) or \"excel\".\r\n"
            + "    locale       (Optional) the market of which you want to load models. Default=en-us.\r\n"
            + "    metricsDestFolder (Optional) the folder where the destination metrics file gets created. Only applicable if dest=excel. Default=\\\\ice-recommender\\ModelRepository\\Models\\LDA\r\n"
            + "  Example:   LodaModel.exe LoadAll ModelRepository=\\\\ICE-ICERecommender\\ModelRepository\r\n"
            + "  Example:   LodaModel.exe LoadAll ModelRepository=\\\\br1iceml002\\ModelRepository dest=excel locale=en-gb\r\n"
            + "  Example:   LodaModel.exe LoadAll ModelRepository=\\\\br1iceml002\\ModelRepository dest=excel locale=en-gb metricsDestFolder=\\\\ice-recommender\\ModelRepository\\Models\\LDA\r\n"
            + "    \r\n";


        public const string DatabaseName = "dbname";

        public const string ModelsDbName = "modelsdb";

        public const string ModelRepositoryPath = "modelrepository";

        private const string connectionStringTemplate = "server={0};database={1};Trusted_Connection=yes";

        private const string DEFAULT_MODEL_METRICS_DESTINATION = "sqldb";

        private const string EXCEL_SPREADSHEET = "excel";

        private const string DEFAULT_MODEL_LOCALE = "all";

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                System.Console.WriteLine("Usage: {0} <command> [options]", AppDomain.CurrentDomain.FriendlyName);
                System.Console.WriteLine(DetailUsage);
                Environment.Exit(1);
            }

            // new command-line handling
            var command = args[0].ToLowerInvariant();
            var parameters = GetParameters(args);

            if (!ProcessCommands(command, parameters))
            {
                StatusMessage.Write("Unknown command");
                }

            System.Console.WriteLine("\n\nFinished execution.");
        }

        /// <summary>
        /// Parse and normalize command-line options
        /// </summary>
        /// <param name="args">command-line</param>
        /// <returns>parameters set</returns>
        private static Dictionary<string, string> GetParameters(string[] args)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            if (args == null)
            {
                return parameters;
            }

            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                var argTokens = arg.Split('=');
                if (argTokens.Length == 1)
                {
                    parameters.Add(argTokens[0].ToLowerInvariant(), string.Empty);
                }
                else
                {
                    parameters.Add(argTokens[0].ToLowerInvariant(), argTokens[1]);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Process commands
        /// </summary>
        /// <param name="command">command</param>
        /// <param name="parameters">command parameters set</param>
        /// <returns>true if successful</returns>
        private static bool ProcessCommands(string command, IDictionary<string, string> parameters)
        {
            bool validCommand = true;
            LDAConfig ldaConfig;

            // Get the model metrics destination type.
            string modelMetricsDest = GetModelMetricsDestination(parameters);
            string modelMetricsDestPath = string.Empty;
            StreamWriter writer = null;
            bool isModelMetricsFileEmpty = false;

            switch (command)
            {
                case Commands.Read:

                    break;

                case Commands.Load:
                    // to do: handle the case of saveto excel.
                    var modelRepositoryPath = GetmodelRepositoryPath(parameters);
                    ldaConfig = GetLdaConfig(parameters);
                    var sqlServer = GetServerName(parameters);
                    var modelsDbName = GetModelsDbName(parameters);
                    var modelsDb = GetModelsDb(sqlServer, modelsDbName);
                    bool success = false;
                    AddModelParametersToModelsDb(modelsDb, ldaConfig, modelRepositoryPath, ref success);
                    break;

                case Commands.LoadAll:
                    modelRepositoryPath = GetmodelRepositoryPath(parameters);
                    string locale = GetModelLocale(parameters);

                    modelsDb = null;

                    var ldaConfigFiles = new List<string>();
                    GetListOfFiles(modelRepositoryPath + @"\Models", @"*.LDAConfig.json", ldaConfigFiles, locale);                    
                                        
                    if (ldaConfigFiles.Count > 0)
                    {
                        if (modelMetricsDest == DEFAULT_MODEL_METRICS_DESTINATION)
                        {
                            sqlServer = GetServerName(parameters);
                            modelsDbName = GetModelsDbName(parameters);
                            modelsDb = GetModelsDb(sqlServer, modelsDbName);
                        }
                        else if (modelMetricsDest == EXCEL_SPREADSHEET)
                        {
                            var destinationFolder = GetParameterOrDefault(
                                parameters,
                                Options.MetricsDestFolder,
                                ConfigurationManager.AppSettings["MetricsDestFolder"]);

                            modelMetricsDestPath = Path.Combine(destinationFolder,
                                                        @"ModelMetrics_" + String.Format("{0:yyyyMMdd}", DateTime.Now) + "_" + locale + ".tsv");
                            writer = new StreamWriter(modelMetricsDestPath, true, Encoding.UTF8);
                            isModelMetricsFileEmpty = new FileInfo(modelMetricsDestPath).Length == 0;
                        }
                        else
                        {
                            break;
                        }

                        int count = 0;
                        foreach (var configFile in ldaConfigFiles)
                        {
                            ldaConfig = GetLdaConfig(configFile);
                            success = false;
                            if (modelMetricsDest == DEFAULT_MODEL_METRICS_DESTINATION)
                            {
                                AddModelParametersToModelsDb(modelsDb, ldaConfig, modelRepositoryPath, ref success);
                            }
                            else if (modelMetricsDest == EXCEL_SPREADSHEET)
                            {
                                bool needWriteTableHeader = (count == 0 && isModelMetricsFileEmpty);
                                AddModelParametersToExcel(writer, ldaConfig, modelRepositoryPath, ref success, needWriteTableHeader);
                            }
                            if (success)
                            {
                                count++;
                            }
                            else
                            {
                                StatusMessage.Write(string.Format("Failed to add {0}\r\n", configFile), ConsoleColor.Red);
                            }
                        }
                        if (writer != null)
                        {
                            writer.Close();
                            if (count == 0 && modelMetricsDest == EXCEL_SPREADSHEET)
                            {
                                File.Delete(modelMetricsDestPath);
                                StatusMessage.Write("Deleting file " + modelMetricsDestPath);
                            }
                        }
                        StatusMessage.Write(count + " model metrics added in total.");

                    }

                    break;
                
                default:
                    validCommand = false;
                    break;
            }

            return validCommand;
        }

        private static Dictionary<string, double> GetMetricsDictionary()
        {
            string metricsList = ConfigurationManager.AppSettings["MetricsList"];

            if (string.IsNullOrEmpty(metricsList)) 
            {
                return null;
            }

            var metricDictionary = new Dictionary<string, double>();
            var metrics = metricsList.Split(',');
            foreach (string metric in metrics)
            {
                metricDictionary.Add(metric, -1.0);
            }

            return metricDictionary;
        }

        private static LDAConfig GetLdaConfig(IDictionary<string, string> parameters)
        {
            string modelConfigFile;
            if (!parameters.TryGetValue(Options.Config, out modelConfigFile))
            {
                StatusMessage.Write("Missing config parameter");
                return null;
            }

            return GetLdaConfig(modelConfigFile);
        }

        private static LDAConfig GetLdaConfig(string modelConfigFile)
        {
            if (!File.Exists(modelConfigFile))
            {
                StatusMessage.Write("Error: Model config file not found: " + modelConfigFile);
                return null;
            }

            try
            {
                var ldaConfig = JsonConvert.DeserializeObject<LDAConfig>(File.ReadAllText(modelConfigFile));

                return ldaConfig;
            }
            catch (Exception e)
            {
                StatusMessage.Write("Error: Cannot load model config file: " + e.Message);
            }

            return null;
        }

        private static string GetServerName(IDictionary<string, string> parameters)
        {
            string serverName;

            // check command-line override
            if (!parameters.TryGetValue(Options.SQLServer, out serverName))
            {
                serverName = ConfigurationManager.AppSettings["SQLServer"];
            }

            return serverName;
        }

        private static string GetModelsDbName(IDictionary<string, string> parameters)
        {
            string modelsDbName;

            // check command-line override
            if (!parameters.TryGetValue(Options.ModelsDbName, out modelsDbName))
            {
                modelsDbName = ConfigurationManager.AppSettings["ModelsDbName"];
            }

            return modelsDbName;
        }

        private static string GetmodelRepositoryPath(IDictionary<string, string> parameters)
        {
            string repositoryPath;
            
            // Check command-line override
            if (!parameters.TryGetValue(Options.ModelRepositoryPath, out repositoryPath))
            {
                repositoryPath = ConfigurationManager.AppSettings["ModelRepositoryPath"];
            }

            return repositoryPath;
        }

        private static string GetDatabaseName(LDAConfig ldaConfig, IDictionary<string, string> parameters)
        {
            var sampleName = ldaConfig.SampleName;

            if (string.IsNullOrWhiteSpace(sampleName))
            {
                StatusMessage.Write("Sample name not specified.");
                return string.Empty;
            }

            string dbName;
            if (!parameters.TryGetValue(Options.DatabaseName, out dbName))
            {
                dbName = string.Format("{0}_{1}_{2}", ldaConfig.SampleName, ldaConfig.FeaturizationParameters.MinWordDocumentFrequency, ldaConfig.FeaturizationParameters.MaxRalativeWordDocumentFrequency);
            }

            return dbName;
        }

        private static string GetModelMetricsDestination(IDictionary<string, string> parameters)
        {
            string modelMetricsDest;

            if (!parameters.TryGetValue(Options.ModelMetricsDestination, out modelMetricsDest))
            {
                modelMetricsDest = DEFAULT_MODEL_METRICS_DESTINATION;
            }

            return modelMetricsDest;
        }

        private static string GetModelLocale(IDictionary<string, string> parameters)
        {
            string modelLocale;

            if (!parameters.TryGetValue(Options.ModelLocale, out modelLocale))
            {
                modelLocale = DEFAULT_MODEL_LOCALE;
            }

            return modelLocale.ToLower();
        }

        private static string GetParameterOrDefault(
            IDictionary<string, string> parameters,
            string parameter,
            string defaultValue)
        {
            string value;

            if (!parameters.TryGetValue(parameter.ToLowerInvariant(), out value))
            {
                value = defaultValue;
            }

            return value;
        }

        private static Dictionary<string, double> ExtractModelMetrics(ref LDAConfig ldaConfig, string modelRepositoryPath)
        {
            if (ldaConfig == null)
            {
                return null;
            }

            ldaConfig.ModelRepositoryPath = modelRepositoryPath;
            ldaConfig.ModelDirectory = null; // Reset all subdirectories

            var metrics = ReadMetrics(ldaConfig.ExtrinsicMetricsProcessed);
            return metrics;
        }

        /// <summary>
        /// Adds a row to the list of Models  (in the ModelsDb) if none exists and returns an object of type ModelDatabase,
        /// representing the new model.  In the process creates a new db to hold the new model's data.  If thes db has already been created it simply opens a connection to it. 
        /// </summary>
        /// <param name="sqlServer"></param>
        /// <param name="modelsDbName"></param>
        /// <param name="ldaConfig"></param>
        /// <returns></returns>
        private static ModelDatabase AddModelParametersToModelsDb(ModelsDb modelsDb, LDAConfig ldaConfig, string modelRepositoryPath, ref bool success)
        {
            var metrics = ExtractModelMetrics(ref ldaConfig, modelRepositoryPath);
            if (metrics == null)
            {
                return null;
            }
            
            string modelDbName;
            int modelId;
            try
            {
                StatusMessage.Write("Adding metrics to Db: " + ldaConfig.ExtrinsicMetricsProcessed);
                modelsDb.AddModel(ldaConfig, metrics, out modelDbName, out modelId);
                success = true;
            }
            catch (Exception e)
            {
                StatusMessage.Write("Could not add a record to the Topic models db:" + e.ToString());
                throw;
            }

            var model = new ModelDatabase("", modelsDb.serverName, modelDbName, false);
            if (model.Open())
            {
                //The database has already been created
            }

            return model;
        }

        /// <summary>
        /// Adds a row to model metrics destination path (tsv file).
        /// </summary>
        /// <param name="modelMetricsDestPath"></param>
        /// <param name="ldaConfig"></param>
        /// <param name="modelRepositoryPath"></param>
        /// <param name="success"></param>
        private static void AddModelParametersToExcel(StreamWriter writer, LDAConfig ldaConfig, string modelRepositoryPath, ref bool success, bool needWriteTableHeader)
        {
            var metrics = ExtractModelMetrics(ref ldaConfig, modelRepositoryPath);
            if (metrics == null)
                return;

            if (needWriteTableHeader)
            {
                // write the table header.
                StatusMessage.Write("Writing table header");
                writer.Write("Locale\tCorpus\tSample\tMin\tMax\tK\tAlpha\tRho\tMinibatch\tPasses\tInitialT\tPowerT");
                foreach (var metric in metrics)
                {
                    writer.Write("\t{0}", metric.Key);
                }
                writer.Write("\tmodelName\tmetricsFilePath");
                writer.WriteLine();
            }

            StatusMessage.Write("Adding metrics to EXCEL: " + ldaConfig.ExtrinsicMetricsProcessed);
            writer.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                ldaConfig.Locale,
                ldaConfig.Corpus,
                ldaConfig.SampleName,
                ldaConfig.FeaturizationParameters.MinWordDocumentFrequency,
                ldaConfig.FeaturizationParameters.MaxRalativeWordDocumentFrequency,
                ldaConfig.LDAParameters.NumTopics,
                ldaConfig.LDAParameters.Alpha,
                ldaConfig.LDAParameters.Rho,
                ldaConfig.LDAParameters.Minibatch,
                ldaConfig.LDAParameters.Passes,
                ldaConfig.LDAParameters.InitialT,
                ldaConfig.LDAParameters.PowerT);
            foreach (var metric in metrics)
            {
                writer.Write("\t{0}", metric.Value);                
            }
            writer.Write("\t{0}\t{1}", ldaConfig.modelName, ldaConfig.ExtrinsicMetricsProcessed);
            writer.WriteLine();
            success = true;
        }

        private static ModelsDb GetModelsDb(string sqlServer, string modelsDbName)
        {
            try
            {
                var metricDictionary = GetMetricsDictionary();
                var modelsDb = new ModelsDb(sqlServer, modelsDbName, metricDictionary);
                modelsDb.Open();
                return modelsDb;
            }
            catch(Exception e)
            {
                StatusMessage.Write("Could not add a record to the Topic models db:" + e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Read both Extrinsic metrics and perplexity (if available)
        /// </summary>
        /// <param name="metricsFilepath"></param>
        /// <returns></returns>
        private static Dictionary<string, double> ReadMetrics(string metricsFilepath)
        {
            if (!File.Exists(metricsFilepath) || FileManager.IsFileLocked(metricsFilepath))
            {
                return null;
            }

            var foundAtLeastOneMetric = false;
            
            // Clear out old values, just in case
            var metricDictionary = GetMetricsDictionary();
            
            var metrics = File.ReadLines(metricsFilepath);
            foreach (var metric in metrics)
            {
                string[] metricTupple = metric.Split('\t');
                if (metricTupple.Length > 2)
                {
                    break;
                }

                string metricName = metricTupple[0].Replace(" ", string.Empty).ToLower();
                if (metricDictionary.ContainsKey(metricName))
                {
                    metricDictionary[metricName] = Convert.ToDouble(metricTupple[1]);
                    foundAtLeastOneMetric = true;
                }
            }

            string perplexityMetricFile = Directory.GetFiles(Directory.GetParent(metricsFilepath).FullName, "*Perplexity*", SearchOption.AllDirectories)
                                          .FirstOrDefault();
            if (perplexityMetricFile != null &&
                !FileManager.IsFileLocked(perplexityMetricFile) &&
                FileManager.GetFileLength(perplexityMetricFile) > 0)
            { 
                metricDictionary["perplexity"] = Convert.ToDouble(File.ReadAllText(perplexityMetricFile).Trim());
                foundAtLeastOneMetric = true;
            }

            if (foundAtLeastOneMetric)
            {
                return metricDictionary;
            }

            return null;
        }

        private static void GetListOfFiles(string directoryPath, string filenamePattern, List<string> listOfFiles, string locale=DEFAULT_MODEL_LOCALE)
        {
            if (listOfFiles == null)
            {
                listOfFiles = new List<string>();
            } 
            
            try
            {
                listOfFiles.AddRange(Directory.GetFiles(directoryPath, filenamePattern, SearchOption.AllDirectories));
                if (locale != DEFAULT_MODEL_LOCALE)
                {
                    listOfFiles.RemoveAll(file => file.IndexOf("\\" + locale + "\\") < 0);
                }  
            }
            catch (System.Exception e)
            {
                StatusMessage.Write(e.Message);
                // Throw the exception and let the upper stack handle it.
                throw;
            }
        }
    }
}