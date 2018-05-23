using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Microsoft.Content.Recommendations.Common;

namespace Microsoft.Content.TopicExtraction
{
    class LDAConfigFileGenerator
    {
        static Dictionary<string, int>
            PARAMETER_INDEX_DICTIONARY = new Dictionary<string, int>()
            {
                {"alpha", 0},
                {"rho", 1},
                {"minworddocumentfrequency", 2},
                {"min", 2},
                {"maxralativeworddocumentfrequency", 3},
                {"max", 3},
                {"numtopics", 4},
                {"k", 4},
                {"passes", 5},
                {"minibatch", 6},
                {"initialt", 7},
                {"powert", 8}
            };

        /// <summary>
        /// Load individual parameter range files and generate List<LDAConfig>
        /// </summary>
        /// <param name="modelRepositoryPath">The top folder where you want to save all the models that will be learned</param>
        /// <param name="folderOfParamRangeFiles">The folder where the parameter range files are located</param>
        /// <param name="trainingSampleName">the name of the training sample</param>
        /// <param name="defaultModelConfig">the template of LDAConfig file</param>
        /// <param name="configFilesFolder">The folder where LDAConfig files (for training) will be saved</param>
        /// <param name="listOfLDAConfigFilesForFeaturization">absolute paths for LDAConfig files (for featurization)</param>
        /// <param name="listOfLDAConfigFilesForTest">absolute paths for LDAConfig files (for metrics computation)</param>
        /// <returns>absolute paths for LDAConfig files (for training)</returns>
        public static List<string> GenerateLDAConfigFiles( string modelRepositoryPath,
                                                           string folderOfParamRangeFiles,
                                                           string trainingSampleName,
                                                           LDAConfig defaultModelConfig,
                                                           string configFilesFolder,
                                                           ref List<string> listOfLDAConfigFilesForFeaturization,
                                                           ref List<string> listOfLDAConfigFilesForTest)
        {
            var alphaRange = new List<double>();
            var rhoRange = new List<double>();
            var numOfTopicsRange = new List<int>();
            var minibatchRange = new List<int>();
            var powerTRange = new List<double>();
            var initialTRange = new List<double>();
            var passesRange = new List<int>();
            var minWordDocFreqRange = new List<int>();
            var maxRelWordDocFreqRange = new List<float>();

            // Load LDA parameters from a single file or individual files.
            int parameterIndex = -1;
            if (File.Exists(Path.Combine(folderOfParamRangeFiles, "LDAParameters.tsv")))
            {
                foreach (var line in File.ReadLines(Path.Combine(folderOfParamRangeFiles, "LDAParameters.tsv")))
                {
                    string lineContent = line.Trim().ToLower();
                    if (PARAMETER_INDEX_DICTIONARY.ContainsKey(lineContent))
                    { 
                        parameterIndex = PARAMETER_INDEX_DICTIONARY[lineContent];
                    }
                    else
                    {
                        switch (parameterIndex)
                        {
                            case 0:
                                alphaRange = Helper.ParseListOfValues<double>(lineContent);                                     
                                break;

                            case 1:
                                rhoRange = Helper.ParseListOfValues<double>(lineContent);
                                break;

                            case 2:
                                minWordDocFreqRange = Helper.ParseListOfValues<int>(lineContent);
                                break;

                            case 3:
                                maxRelWordDocFreqRange = Helper.ParseListOfValues<float>(lineContent);
                                break;

                            case 4:
                                numOfTopicsRange = Helper.ParseListOfValues<int>(lineContent);
                                break;

                            case 5:
                                passesRange = Helper.ParseListOfValues<int>(lineContent);
                                break;

                            case 6:
                                minibatchRange = Helper.ParseListOfValues<int>(lineContent);
                                break;

                            case 7:
                                initialTRange = Helper.ParseListOfValues<double>(lineContent);
                                break;

                            case 8:
                                powerTRange = Helper.ParseListOfValues<double>(lineContent);
                                break;

                            default:
                                break;
                        }
                        parameterIndex = -1;
                    }

                }
            }
            else 
            {
                alphaRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "Alpha.txt")).Select(Double.Parse).ToList();
                rhoRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "Rho.txt")).Select(Double.Parse).ToList();
                numOfTopicsRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "NumTopics.txt")).Select(int.Parse).ToList();
                minibatchRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "Minibatch.txt")).Select(int.Parse).ToList();
                powerTRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "PowerT.txt")).Select(Double.Parse).ToList();
                initialTRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "InitialT.txt")).Select(Double.Parse).ToList();
                passesRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "Passes.txt")).Select(int.Parse).ToList();

                minWordDocFreqRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "MinWordDocumentFrequency.txt")).Select(int.Parse).ToList();
                maxRelWordDocFreqRange = File.ReadLines(Path.Combine(folderOfParamRangeFiles, "MaxRalativeWordDocumentFrequency.txt")).Select(Single.Parse).ToList();            
            }

            List<string> listOfLDAConfigFilesForTraining = new List<string>();


            foreach (var min in minWordDocFreqRange)
            {
                foreach (var max in maxRelWordDocFreqRange)
                {
                    var f = new FeaturizationParameters()
                    {
                        MinWordDocumentFrequency = min,
                        MaxRalativeWordDocumentFrequency = max
                    };

                    // a boolean flag ensuring each combination of min/max to be added into the list only once.
                    bool minMaxAdded = false;

                    foreach (var alpha in alphaRange)
                    {
                        foreach (var rho in rhoRange)
                        {
                            foreach (var numOfTopics in numOfTopicsRange)
                            {
                                foreach (var miniBatch in minibatchRange)
                                {
                                    foreach (var powerT in powerTRange)
                                    {
                                        foreach (var initialT in initialTRange)
                                        {
                                            foreach (var passes in passesRange)
                                            {

                                                var p = new LDAParameters()
                                                {
                                                    Alpha = alpha,
                                                    Rho = rho,
                                                    NumTopics = numOfTopics,
                                                    Minibatch = miniBatch,
                                                    PowerT = powerT,
                                                    InitialT = initialT,
                                                    Passes = passes
                                                };

                                                var config = new LDAConfig()
                                                {
                                                    LDAParameters = p,
                                                    FeaturizationParameters = f,
                                                    SampleName = trainingSampleName,
                                                    ModelRepositoryPath = modelRepositoryPath, 
                                                    Locale = defaultModelConfig.Locale,
                                                    Corpus = defaultModelConfig.Corpus,                                                                                                                                                           
                                                    ModelStatistics = new ModelStatistics()
                                                };

                                                UpdateConfigFileLists(ref listOfLDAConfigFilesForTraining, ref listOfLDAConfigFilesForTest, ref listOfLDAConfigFilesForFeaturization, configFilesFolder, config, ref minMaxAdded);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    minMaxAdded = false;

                }  // foreach max
            }

            return listOfLDAConfigFilesForTraining;
        }        

        // Load existing LDA config files (*.LDAConfig.json)
        public static List<string> LoadLDAConfigFiles( string modelRepositoryPath,                                                           
                                                        string trainingSampleName,
                                                        LDAConfig defaultModelConfig,
                                                        string SourceFolderOfLDAConfigFiles,
                                                        string learningConfigFilesFolder,           
                                                        ref List<string> listOfLDAConfigFilesForFeaturization,
                                                        ref List<string> listOfLDAConfigFilesForTest)
        {
            // Get all ldaconfig.json files.
            List<string> listOfLDAConfigFilesForTraining = new List<string>();
            
            var ldaConfigFiles = Directory.GetFiles(SourceFolderOfLDAConfigFiles, "*LDAConfig.json", SearchOption.AllDirectories);

            foreach (string ldaConfigFile in ldaConfigFiles)
            {
                // Load ldaconfig
                var config = Program.LoadLDAConfig(ldaConfigFile);

                var newConfig = new LDAConfig()
                {
                    LDAParameters = config.LDAParameters,
                    FeaturizationParameters = config.FeaturizationParameters,
                    SampleName = trainingSampleName,
                    ModelRepositoryPath = modelRepositoryPath,
                    Locale = defaultModelConfig.Locale,
                    Corpus = defaultModelConfig.Corpus,
                    ModelStatistics = new ModelStatistics()
                };

                bool minMaxAdded=false;
                UpdateConfigFileLists(ref listOfLDAConfigFilesForTraining, ref listOfLDAConfigFilesForTest, ref listOfLDAConfigFilesForFeaturization, learningConfigFilesFolder, newConfig, ref minMaxAdded);
            }

            listOfLDAConfigFilesForFeaturization.AddRange(listOfLDAConfigFilesForTraining.GroupBy(
                config => new
                {
                    Program.LoadLDAConfig(config).FeaturizationParameters.MinWordDocumentFrequency,
                    Program.LoadLDAConfig(config).FeaturizationParameters.MaxRalativeWordDocumentFrequency
                })
                .Select(g => g.First()));

            return listOfLDAConfigFilesForTraining;
        }

        public static List<string> LoadLDAParameterTable(string modelRepositoryPath,
                                                        string trainingSampleName,
                                                        LDAConfig defaultModelConfig,
                                                        string ldaParameterTablePath,
                                                        string learningConfigFilesFolder,
                                                        ref List<string> listOfLDAConfigFilesForFeaturization,
                                                        ref List<string> listOfLDAConfigFilesForTest)
        {
            // Get all ldaconfig.json files.
            List<string> listOfLDAConfigFilesForTraining = new List<string>();
            
            List<List<string>> lists = File.ReadLines(ldaParameterTablePath)
                .Skip(1)    // skip column header
                .Select(line => line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList())
                .ToList();

            // Group all rows by <min, max>.
            var minMaxGroups = lists.GroupBy(list => new
                                        {
                                            min = Helper.GetValue<int>(list[0]),
                                            max = Helper.GetValue<float>(list[1])
                                        });

            foreach (var group in minMaxGroups)
            {
                var f = new FeaturizationParameters()
                {
                    MinWordDocumentFrequency = group.Key.min,
                    MaxRalativeWordDocumentFrequency = group.Key.max
                };

                // a boolean flag ensuring each combination of min/max to be added into the list only once.
                bool minMaxAdded = false;
                foreach(var row in group)
                {
                    // The default (LDA parameter) table has the following columns.                    
                    // min	max	topicCount	alpha	rho	miniBatch	passes	initialT	powerT
                    // E.g. \\ICE-Recommender\ModelRepository\TuneLDAParameters\RangesOfParams\LDAParameterTable.tsv
                    int numOfTopics = Helper.GetValue<int>(row[2]);
                    double alpha = Helper.GetValue<double>(row[3]);
                    double rho = Helper.GetValue<double>(row[4]);
                    int miniBatch = Helper.GetValue<int>(row[5]);
                    int passes = Helper.GetValue<int>(row[6]);
                    double initialT = Helper.GetValue<double>(row[7]);
                    double powerT = Helper.GetValue<double>(row[8]);

                    var p = new LDAParameters()
                    {
                        Alpha = alpha,
                        Rho = rho,
                        NumTopics = numOfTopics,
                        Minibatch = miniBatch,
                        PowerT = powerT,
                        InitialT = initialT,
                        Passes = passes
                    };

                    var config = new LDAConfig()
                    {
                        LDAParameters = p,
                        FeaturizationParameters = f,
                        SampleName = trainingSampleName,
                        ModelRepositoryPath = modelRepositoryPath,
                        Locale = defaultModelConfig.Locale,
                        Corpus = defaultModelConfig.Corpus,
                        ModelStatistics = new ModelStatistics()
                    };

                    UpdateConfigFileLists(ref listOfLDAConfigFilesForTraining, ref listOfLDAConfigFilesForTest, ref listOfLDAConfigFilesForFeaturization, learningConfigFilesFolder, config, ref minMaxAdded);
                }
                minMaxAdded = false;
            }            

            return listOfLDAConfigFilesForTraining;
        }

        /// <summary>
        /// Update the following lists of LDA config files, given an instance of LDAConfig:
        /// listOfLDAConfigFilesForTraining, listOfLDAConfigFilesForTest and listOfLDAConfigFilesForFeaturization.
        /// </summary>
        private static void UpdateConfigFileLists(ref List<string> listOfLDAConfigFilesForTraining, ref List<string> listOfLDAConfigFilesForTest, ref List<string> listOfLDAConfigFilesForFeaturization, string learningConfigFilesFolder, LDAConfig config, ref bool minMaxAdded)
        {
            string json = JsonConvert.SerializeObject(config);

            string ldaConfigFilePath = string.Format(@"{0}\{1}.LDAConfig.json", learningConfigFilesFolder, config.modelName);
            File.WriteAllText(ldaConfigFilePath, json);

            // Add config files into lists.
            listOfLDAConfigFilesForTraining.Add(ldaConfigFilePath);
            listOfLDAConfigFilesForTest.Add(config.LDAConfigFile);
            if (!minMaxAdded)
            {
                listOfLDAConfigFilesForFeaturization.Add(ldaConfigFilePath);
                minMaxAdded = true;
            }            
        }
    }
}
