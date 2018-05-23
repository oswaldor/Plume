///
/// 
/// 

namespace Microsoft.Content.Recommendations.Common
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Content.Recommendations.LinearAlgebra;
    using Microsoft.Content.TextProcessing;
    using Microsoft.Content.TopicExtraction;
    using Newtonsoft.Json;

    public enum ProcessStatus
    { 
        InvalidCommand = -1,
        Success = 0,
        Failed = 1
    }

    /// <summary>
    /// Drives the LDA Model building and document analysis jobs
    /// </summary>
    public class Program
    {
        private const string DetailUsage =
            "\r\n" 
            + "Command: LearnLDA - Train LDA model\r\n" 
            + "  Options:\r\n"
            + "    SampleName   Name of file containing training sample documents, pulled from\r\n"
            + "                 the specified corpus\r\n"
            + "    Config       LDA config file that specifies the learning parameters\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder learnlda samplename=20150203.tsv config=c:\\config1.json\r\n"
            + "\r\n" 
            + "Command: Docs2Text - Convert corpus sample file to TSV format\r\n"
            + "  Options:\r\n"
            + "    SampleName   Name of file containing sample documents, pulled from the\r\n"
            + "                 specified corpus\r\n"
            + "    Locale       Corpus locale\r\n" 
            + "    Corpus       Source of corpus, e.g. Reuters, MSN\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder docs2text samplename=20150203.tsv locale=en-us corpus=msn\r\n"
            + "\r\n"
            + "Command: NER - Stops after applying Named-Entity-Recognition to the corpus sample.\r\n"
            + "               This generates an intermediate file with a .NEE.tsv suffix in its name that\r\n"
            + "               can be consume by the Vocabulary building step of model builder's pipeline.\r\n"
            + "  Options:\r\n"
            + "    SampleName   Name of file containing sample documents, pulled from the\r\n"
            + "                 specified corpus\r\n"
            + "    Config       LDA config file specifying corpus featurization parameters\r\n"
            + "                 Note: NamedEntityNormalization member set to true must be present\r\n"
            + "                 inside the FeaturizationParameters section.\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder vocabularystats samplename=20150203.tsv config=c:\\conf.json\r\n"
            + "\r\n"
            + "Command: VocabularyStats - Generate vocabulary statistics for a corpus sample\r\n" 
            + "  Options:\r\n"
            + "    SampleName   Name of file containing sample documents, pulled from the\r\n" 
            + "                 specified corpus\r\n"
            + "    Config       LDA config file specifying corpus featurization parameters\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder vocabularystats samplename=20150203.tsv config=c:\\conf.json\r\n"
            + "\r\n" 
            + "Command: GenerateDocVectors - Generate document vectors using specified model\r\n" 
            + "  Options:\r\n"
            + "    Config       LDA config file of model used to analyze documents\r\n"
            + "    InputFile    Documents file in supported format\r\n"
            + "    Norm         Optional. L1 or L2 (default) normalizaiton for vectors\r\n"
            + "    Encoding     Optional. Encode output vectors as Text or Base64 (default)\r\n"
            + "    Compress     Optional. The best compression is automatically chosen.\r\n"
            + "                 But you can set it to S (Sparse) or D (Dense) to override.\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder generatedocvectors inputfile=c:\\docs.tsv config=\r\n"
            + "                 c:\\models\\1000_10_0.275_0.001_0.001_256_2_1_0.5.LDAConfig.json\r\n"
            + "                 norm=l1 encoding=Text compress=S\r\n"
            + "\r\n"
            + "Command: FeaturizeDocs - Preprocess and cache training sample before training\r\n" 
            + "  Options:\r\n"
            + "    SampleName   Name of file containing sample documents, pulled from the\r\n" 
            + "                 specified corpus\r\n"
            + "    Config       LDA config file specifying corpus featurization parameters\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder featurizedocs samplename=20150203.tsv config=\r\n"
            + "                 c:\\models\\1000_10_0.275_0.001_0.001_256_2_1_0.5.LDAConfig.json\r\n"
            + "\r\n" 
            + "Commands: GetMetrics - Generate metrics for the a specified model\r\n"
            + "  Options:\r\n" 
            + "    Config       LDA config file for model we are generating metrics for\r\n"
            + "    SampleName   Optional.  If specified this test sample is used to compute\r\n"
            + "                 model's Perpexity\r\n"
            + "    TopWordCount Optional.  Default is 10. Number of top most frequent words\r\n"
            + "                 in a topic used to compute its coherence\r\n"
            + "    Epsilon      Optional. Default is 10^-12-1\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder getmetrics samplename=20150203.test.tsv config=c:\\models\r\n"
            + "                 \\1000_10_0.275_0.001_0.001_256_2_1_0.5.LDAConfig.json\r\n"
            + "\r\n"
            + "Commands: ConvertVectors - Generate metrics for the a specified model\r\n"
            + "  Options:\r\n"
            + "    InputFile    Contains list of vectors to transform\r\n"
            + "    Norm         Optional. L1 or L2 (default) normalizaiton for vectors\r\n"
            + "    Encoding     Optional. Encode output vectors as Text or Base64 (default)\r\n"
            + "    Compress     Optional. Defaults to D (Dense). S (Sparse) to override.\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder ConvertVectors InputFile=20150203.test.dv norm=l1 encoding=Text compress=S\r\n"
            + "\r\n"
            + "Commands: SimilarityPerfMetrics - Compute similarities for all possible pairs of items in a list\r\n"
            + "  Options:\r\n"
            + "    InputFile    Tab separated list of labels and vectors\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder SimilarityPerfMetrics InputFile=c:\\vectors.tsv\r\n"
            + "\r\n"
            + "Commands: OneToManySimilarity - Compute similarity between the first and rest of items in a list\r\n"
            + "  Options:\r\n"
            + "    InputFile    Tab separated list of labels and vectors\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder OneToManySimilarity InputFile=c:\\vectors.tsv\r\n"
            + "\r\n"
            + "Commands: PairwiseSimilarity - For each Nth item in list 1, compare to Nth item in list 2\r\n"
            + "  Options:\r\n"
            + "    InputFile    Tab separated list of labels and vectors\r\n"
            + "    InputFile2   Tab separated list of labels and vectors\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder PairwiseSimilarity InputFile=c:\\vectors.tsv InputFile2=c:\\vectors2.tsv\r\n"
            + "\r\n"
            + "Commands: Partition - Break up sample corpus into multiple partitions\r\n"
            + "  Options:\r\n"
            + "    Config       LDA config file specifying market info to locate input sample\r\n"
            + "    SampleName   Corpus to be broken up into partitions\r\n"
            + "    FirstDoc        Optional, defautls to 1. Start first partition from this line in the input sample\r\n"
            + "    PartCount       Optional if DocCount is specified.  Otherwise it's the number of partition to break input into\r\n"
            + "    DocCount        Optional, defaults to 1000 if PartCount is not specified\r\n"
            + "  If PartCount is specified and DocCount is not, the input will be broken into evenly sized partitions starting from FirstDoc\r\n."
            + "  Example:\r\n"
            + "    LDAModelBuilder Partition SampleName=Documents.tsv\r\n"
            + "\r\n"
            + "Commands: Silhouette - Compute siloutte on a set of vector clusters.\r\n"
            + "  Options:\r\n"
            + "    InputFile    Required the first time Silhouette runs.  Optional once the cluster partition files have been created\r\n"
            + "                 Path to folder that contains a set of cluster files c0.csv, c1.csv, etc. Each with vectors\r\n"
            + "    SampleRate   Optional. Default = 1% (0.01).  Use this many vectors per cluster to estimate cluster avg. silhouette\r\n"
            + "                 Silhouette is O(N^2), so you can use this param to control runtime.\r\n"
            + "                 If set too low the Min() of 500 or the actual cluster size is used.\r\n"
            + "    MaxMemory    Optional. Default is 3.0 Gigs.  Number of gigs used to cache vectors in clusters.  We go to disk for overflow\r\n"
            + "    MaxThreads   Optional. Default is 8. Number of threads used to compute vector similarity\r\n"
            + "    ClusterCount Optional. Compute silhouette only on this many clusters.  Used for testing only\r\n"
            + "  Example:\r\n"
            + "    LDAModelBuilder Silhouette SampleRate=0.05 MaxMemory=2.5 MaxThreads=12 InputFile=c:\\ClusterFolder ClusterCount=3\r\n";


        private const int DefaultTCTopWords = 10;

        private static Stopwatch stopWatch;

        /// <summary>
        /// Entrance of LDAModelBuilder.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>0 if success, 1 otherwise</returns>
        static int Main(string[] args)
        {
            stopWatch = new Stopwatch();
            stopWatch.Start();

            if (args.Length < 1)
            {
                System.Console.WriteLine("Usage: {0} <command> [options]", AppDomain.CurrentDomain.FriendlyName);
                System.Console.WriteLine(DetailUsage);
                Environment.Exit(1);
            }

            // new command-line handling
            var command = args[0].ToLowerInvariant();
            var parameters = GetParameters(args);

            ProcessStatus status = ProcessCommands(command, parameters);
            if (status == ProcessStatus.Success)
            {
                stopWatch.Stop();
                System.Console.WriteLine("\n\nFinished execution. Elapsed time {0:D2}:{1:D2}:{2:D2}", stopWatch.Elapsed.Hours, stopWatch.Elapsed.Minutes, stopWatch.Elapsed.Seconds);
                return 0;
            }

            if (status == ProcessStatus.InvalidCommand)
            {
                StatusMessage.Write("Unknown command", ConsoleColor.Yellow);
            }
            else
            {
                StatusMessage.Write("Execution failed!", ConsoleColor.Red);
            }
            return 1;
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
        /// <returns>0 if success, 1 if failed, -1 if command is invalid.</returns>
        /// To do: modify other process methods to return a value indicating success/failure.
        private static ProcessStatus ProcessCommands(string command, IDictionary<string, string> parameters)
        {
            ProcessStatus status = ProcessStatus.Success;
            LDAConfig ldaConfig;

            switch (command)
            {
                case Commands.Docs2Text:
                    var inputFile = GetInputDocumentsFile(null, parameters);
                    string locale = string.Empty;
                    parameters.TryGetValue(Options.Locale, out locale);
                    Language lang = LDAConfig.Locale2Language(locale);
                    if (!string.IsNullOrEmpty(inputFile))
                    {
                        Docs2Text(inputFile, out inputFile, lang);
                    }

                    break;

                case Commands.NER:
                    ldaConfig = GetLdaConfig(parameters);
                    inputFile = GetInputDocumentsFile(ldaConfig, parameters);
                    int maxThread = 2 * Environment.ProcessorCount;
                    string param = string.Empty;
                    if (parameters.TryGetValue(Options.MaxThreads, out param))
                    {
                        maxThread = int.Parse(param);
                        if (maxThread <= 0) maxThread = 1;
                    }
                    NamedEntityRecognition(ldaConfig, inputFile, maxThread);
                    
                    break;

                case Commands.Partition:
                    ldaConfig = GetLdaConfig(parameters);
                    inputFile = GetInputDocumentsFile(ldaConfig, parameters);
                    PartitionInput(inputFile, parameters);

                    break;

                case Commands.VocabularyStatistics:
                    ldaConfig = GetLdaConfig(parameters);
                    inputFile = GetInputDocumentsFile(ldaConfig, parameters);
                    VocabularyStatistics(ldaConfig, ref inputFile, true);
                    break;

                case Commands.FeaturizeDocuments:
                    ldaConfig = GetLdaConfig(parameters);
                    inputFile = GetInputDocumentsFile(ldaConfig, parameters);
                    FeaturizeDocuments(ldaConfig, inputFile);
                    break;

                case Commands.LearnLDA:
                    ldaConfig = GetLdaConfig(parameters);
                    inputFile = GetInputDocumentsFile(ldaConfig, parameters);
                    if (!LearnLDA(ldaConfig, inputFile, parameters.ContainsKey(Options.CopyFeaturizedDoc)))
                    {
                        status = ProcessStatus.Failed;
                    }
                    break;

                case Commands.GetMetrics:
                    if (!GetModelMetrics(parameters))
                    {
                        status = ProcessStatus.Failed;
                    }
                    break;

                case Commands.GenerateDocTopicVectorsLDA:
                    ldaConfig = GetLdaConfig(parameters);
                    GenerateLdaDocumentTopicVectors(ldaConfig, parameters);
                    break;

                case Commands.SimilarityPerfMetrics:
                    GenerateSimilarittyScores_N_vs_All(-1, parameters);
                    break;

                case Commands.OneToManySimilarity:
                    GenerateSimilarittyScores_N_vs_All(1, parameters);
                    break;

                case Commands.PairwiseSimilarity:
                    GeneratePairwiseSimilarittyScores(parameters);
                    break;

                case Commands.ConvertVectors:
                    ConvertVectors(parameters);
                    break;

                case Commands.Silhouette:
                    Silhouette(parameters);
                    break;

                default:
                    status = ProcessStatus.InvalidCommand;
                    break;
            }

            return status;
        }

        private static string GetSampleName(IDictionary<string, string> parameters)
        {
            if (parameters.ContainsKey(Options.SampleName))
            {
                return parameters[Options.SampleName];
            }

            return string.Empty;
        }

        private static LDAConfig GetLdaConfig(IDictionary<string, string> parameters)
        {
            string modelConfigFile;
            if (!parameters.TryGetValue(Options.Config, out modelConfigFile))
            {
                StatusMessage.Write("Missing config parameter");
                return null;
            }

            if (!File.Exists(modelConfigFile))
            {
                StatusMessage.Write("Model config file not found");
                return null;
            }

            try
            {
                var ldaConfig = JsonConvert.DeserializeObject<LDAConfig>(File.ReadAllText(modelConfigFile));

                // Model configs (used analyze documents, e.g DVGen) already have the SampleName set.  It refers to 
                // the corpus sample used to train the model.  We don't want to overwrite it.
                if (string.IsNullOrEmpty(ldaConfig.SampleName))
                {
                    ldaConfig.SampleName = GetSampleName(parameters);
                }

                if (ldaConfig.ModelStatistics == null)
                {
                    ldaConfig.ModelStatistics = new ModelStatistics();
                }

                return ldaConfig;
            }
            catch (Exception e)
            {
                StatusMessage.Write("Error loading configuration file: " + e.Message);
            }

            return null;
        }

        private static string GetInputDocumentsFile(LDAConfig ldaConfig, IDictionary<string, string> parameters)
        {
            string sampleName = GetSampleName(parameters);
            if (string.IsNullOrEmpty(sampleName) && ldaConfig != null)
            {
                sampleName = ldaConfig.SampleName;
            }

            if (string.IsNullOrWhiteSpace(sampleName))
            {
                StatusMessage.Write("Sample name not specified.");
                return string.Empty;
            }

            string locale;
            if (!parameters.TryGetValue(Options.Locale, out locale))
            {
                locale = ldaConfig != null ? ldaConfig.Locale : string.Empty;
            }

            string corpus;
            if (!parameters.TryGetValue(Options.Corpus, out corpus))
            {
                corpus = ldaConfig != null ? ldaConfig.Corpus : string.Empty;
            }

            string corpusRepository;

            // check command-line override
            if (!parameters.TryGetValue(Options.CorpusRepository, out corpusRepository))
            {
                corpusRepository = string.Format(ConfigurationManager.AppSettings["CorpusRepository"], ConfigurationManager.AppSettings["CorpusFolder"], locale, corpus);
            }

            var inputDocumentsFile = string.Format(@"{0}\{1}", corpusRepository, sampleName);
            if (!File.Exists(inputDocumentsFile))
            {
                StatusMessage.Write(string.Format("Input documents file {0} not found!", inputDocumentsFile));
                return string.Empty;
            }

            return inputDocumentsFile;
        }

        private static void PrepareWorkingDirectories(LDAConfig ldaConfig)
        {
            // If needed, create the distination folders for all the model files
            try
            {
                Directory.CreateDirectory(ldaConfig.ModelDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(ldaConfig.CorpusVocabulary));
                Directory.CreateDirectory(Path.GetDirectoryName(ldaConfig.DocumentVocabularies));
                Directory.CreateDirectory(Path.GetDirectoryName(ldaConfig.WordTopicAllocations));
            }
            catch (Exception e)
            {
                StatusMessage.Write("Could not create model output directory.  Verify you have write access: " + e.ToString());
                throw;
            }
        }

        /// <summary>
        /// If the input training corpus is not in tab delimited format
        /// make a tab-delimited copy to speed up training of future models with different paramenters, w/o having to parse the source again.
        /// </summary>
        /// <param name="inputDocumentsFile"></param>
        /// <param name="tsvDocumentsFile"></param>
        /// <returns></returns>
        private static int Docs2Text(string inputDocumentsFile, out string tsvDocumentsFile, Language language)
        {
            tsvDocumentsFile = Path.GetDirectoryName(inputDocumentsFile) + "\\"
                + Path.GetFileNameWithoutExtension(inputDocumentsFile) + ".tsv";

            // Before we proceed, check to see if the tsv version already exists.  If it does, just return the count of documents.
            int documentCount = 0;
            if (File.Exists(tsvDocumentsFile))
            {
                documentCount = File.ReadLines(tsvDocumentsFile).Count(); 
            }
            else
            {
                var documents = DocumentFeeders.Documents(inputDocumentsFile, language);
                using (var file = new StreamWriter(tsvDocumentsFile))
                {
                    foreach (var document in documents)
                    {
                        documentCount++;
                        file.WriteLine(
                            "{0}\t{1}\t{2}",
                            document.Id.Identifier.Replace('\t', ' '),
                            document.Title.Replace('\t', ' '),
                            document.Text.Replace('\t', ' '));
                    }
                }
            }

            return documentCount;
        }

        public static CorpusVocabulary BuildCorpusVocabulary(string inputDocumentsFile, string documentVocabulariesFileName, Language language)
        {
            StatusMessage.Write("Generating corpus vocabulary...");

            var corpusVocabulary = CorpusVocabulary.NewInstance();

            // Aggregate document vocabularies into a single Corpus Vocabulary
            foreach (var documentVocabulary in LoadDocumentVocabularies(inputDocumentsFile, documentVocabulariesFileName, language))
            {
                corpusVocabulary.Add(documentVocabulary);
            }

            corpusVocabulary.ResetIds();
            return corpusVocabulary;
        }

        private static IEnumerable<DocumentVocabulary> LoadDocumentVocabularies(string inputDocumentsFile, string documentVocabulariesFilePath, Language language)
        {
            if (!File.Exists(documentVocabulariesFilePath))
            {
                StatusMessage.Write("Generating document vocabularies...");
                var documents = DocumentFeeders.Documents(inputDocumentsFile, language);

                WriteDocumentVocabularies(documentVocabulariesFilePath, documents, language);
            }

            return File
                .ReadLines(documentVocabulariesFilePath)
                .Select(DocumentVocabulary.Deserialize);
        }

        private static void WriteDocumentVocabularies(string documentVocabulariesFilePath, IEnumerable<Document> documents, Language language)
        {
            var documentVocabularyFactory = DocumentVocabularyFactory.NewInstance(language);
            using (var file = new StreamWriter(documentVocabulariesFilePath))
            {
                foreach (var document in documents)
                {                    
                    var docVocab = documentVocabularyFactory.Get(document.Id.ToString(), (document.Title + ' ' + document.Text).ConvertMSWordQuotesToPlainQuotes());
                    file.WriteLine(docVocab.Serialized);
                }
            }
        }

        private static CorpusVocabulary GenerateTruncatedCorpusVocabulary(string inputDocumentsFile, LDAConfig ldaConfig, bool writeStatistics)
        {
            // If the truncated vocabulary already exists and the statistics were previously computed
            // (e.g. CorpusVocabularyDropped already exists) we can simply load the truncated file and return.
            if (File.Exists(ldaConfig.CorpusVocabulary) && (File.Exists(ldaConfig.CorpusVocabularyDropped) || !writeStatistics))
            {
                return CorpusVocabulary.Load(ldaConfig.CorpusVocabulary);
            }

            // Both, CorpusVocabularyDropped and CorpusVocabulary (truncated) are generated from the raw CorpusVocabulary,
            // so if either file is missing we need to load (or rebuild) the raw version.
           
            // First see if we have a pre-existing copy of the raw corpus-wide vocabulary
            var corpusVocab = CorpusVocabulary.Load(ldaConfig.CorpusVocabularyRaw);

            // If not, create it starting from the document vocabularies
            if (corpusVocab == null)
            {
                corpusVocab = BuildCorpusVocabulary(
                    inputDocumentsFile,
                    ldaConfig.DocumentVocabularies,
                    ldaConfig.Language);

                corpusVocab.PersistCorpusVocabulary(
                    ldaConfig.CorpusVocabularyRaw,
                    ldaConfig.ModelStatistics.DocumentCount);
            }

            StatusMessage.Write(string.Format("Dropping words below min. doc frequency of {0}, and above relative doc frequency of {1}", ldaConfig.FeaturizationParameters.MinWordDocumentFrequency, ldaConfig.FeaturizationParameters.MaxRalativeWordDocumentFrequency));

            // Should we persist removed words to disk?
            string droppedWordsFileName = (writeStatistics == true) ? ldaConfig.CorpusVocabularyDropped : string.Empty;

            corpusVocab.TruncateVocabulary(
                ldaConfig.FeaturizationParameters.MinWordDocumentFrequency,
                (int)(ldaConfig.ModelStatistics.DocumentCount * ldaConfig.FeaturizationParameters.MaxRalativeWordDocumentFrequency),
                droppedWordsFileName,
                ldaConfig.ModelStatistics.DocumentCount);
                
            corpusVocab.PersistCorpusVocabulary(ldaConfig.CorpusVocabulary);
           
            return corpusVocab;
        }

        private static bool FeaturizeDocuments(LDAConfig ldaConfig, string inputDocumentsFile)
        {
            CorpusVocabulary corpusVocabulary = VocabularyStatistics(ldaConfig, ref inputDocumentsFile, true);            
            if (!FeaturizeDocuments(ldaConfig, inputDocumentsFile, corpusVocabulary))
            {
                return false;
            }

            // Pre-generate word docs list map (*.bin) file for metrics evaluation.
            var ldaMetrics = new LDAMetrics(ldaConfig.ModelStatistics.VocabularySize);
            StatusMessage.Write("Generating word documents list bin file...");
            return ldaMetrics.BuildWordDocsListMapIfNotExists(ldaConfig.FeaturizedDocuments);            
        }


        private static bool FeaturizeDocuments(LDAConfig ldaConfig, string inputDocumentsFile, CorpusVocabulary corpusVocabulary)
        {
            if (corpusVocabulary == null || corpusVocabulary.Count() == 0)
            {
                return false;
            }

            if (File.Exists(ldaConfig.FeaturizedDocuments) && FileManager.GetFileLength(ldaConfig.FeaturizedDocuments) > 0L)
            {
                StatusMessage.Write(string.Format("Document featurization: Skipping. Featurized documents already exist {0}", ldaConfig.FeaturizedDocuments));
                return true;
            }

            var documentVocabularies = LoadDocumentVocabularies(inputDocumentsFile, ldaConfig.DocumentVocabularies, ldaConfig.Language);
            using (var output = new StreamWriter(ldaConfig.FeaturizedDocuments))
            {
                StatusMessage.Write("Featurizing documents...");
                foreach (var documentVocabulary in documentVocabularies)
                {
                    output.WriteLine(corpusVocabulary.SerializeDocumentForVW(documentVocabulary));
                }
            }

            return true;
        }

        /// <summary>
        /// Launch vw to learn LDA model, do featurization first if needed.
        /// </summary>
        /// <param name="ldaConfig"></param>
        /// <param name="inputDocumentsFile">the file containing training documents</param>
        /// <param name="copyFeaturizedDoc">true if need to copy featurized docs to model build folder, false otherwise</param>
        /// <returns>true if success, false otherwise</returns>
        private static bool LearnLDA(LDAConfig ldaConfig, string inputDocumentsFile, bool copyFeaturizedDoc)
        {
            if (FeaturizeDocuments(ldaConfig, inputDocumentsFile))
            {
                LDALearner.Learn(ldaConfig, copyFeaturizedDoc);

                StatusMessage.Write("Generating LDA config...");
                using (var output = new StreamWriter(ldaConfig.LDAConfigFile))
                {
                    output.Write(JsonConvert.SerializeObject(ldaConfig));
                }
                return true;
            }
            else
            {
                StatusMessage.Write("Document featurization failed! Please check corpus vocabulary and document vocabularies.");
                return false;
            }
        }

        private static CorpusVocabulary VocabularyStatistics(LDAConfig ldaConfig, ref string inputDocumentsFile, bool writeStatistics)
        {
           
            inputDocumentsFile = NamedEntityRecognition(ldaConfig, inputDocumentsFile, 2 * Environment.ProcessorCount);

            CorpusVocabulary vocabulary = GenerateTruncatedCorpusVocabulary(inputDocumentsFile, ldaConfig, writeStatistics);
            ldaConfig.ModelStatistics.VocabularySize = vocabulary.Count();
            return vocabulary;
        }


        private static string NamedEntityRecognition(LDAConfig ldaConfig, string inputDocumentsFile, int maxThreads)
        {
            if (ldaConfig == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(inputDocumentsFile))
            {
                return null;
            }

            PrepareWorkingDirectories(ldaConfig);
            StatusMessage.Write(string.Format("inputDocumentsFile={0}", inputDocumentsFile));
            ldaConfig.ModelStatistics.DocumentCount = Docs2Text(inputDocumentsFile, out inputDocumentsFile, ldaConfig.Language);

            // If we are asked to treat named entity names as a single unit...
            if (ldaConfig.FeaturizationParameters.NamedEntityNormalization)
            {
                inputDocumentsFile = TokenizeNamedEntities(inputDocumentsFile, ldaConfig.Language, ldaConfig.ModelStatistics.DocumentCount, maxThreads);
            }

            return inputDocumentsFile;
        }

        const int QueueLength = 50;
        private static string TokenizeNamedEntities(string tsvDocumentsFile, Language language, int countOfInputDocs, int maxThreads)
        {

            StatusMessage.Write("NEE - Attempting Name Entity Extraction.");
            if (!NamedEntityExtractor.IsLanguageSupported(language))
            {
                StatusMessage.Write("NEE - Warning:  Language ({0}) not supported.  Aborting Name Entity Extraction.");
                return tsvDocumentsFile;
            }
            
            
            string neeDocumentsFile = Path.GetDirectoryName(tsvDocumentsFile) + "\\"  + Path.GetFileNameWithoutExtension(tsvDocumentsFile) + ".NEE.tsv";

            
            // Before we proceed, check to see if the NEE version of the inpput already exists.  
            // If it does, see if it is fully processed or if we can skip over a few already proceesed docs
            int totalProcessed;
            if ((totalProcessed = CountNeeProcessedDocs(neeDocumentsFile, countOfInputDocs)) == countOfInputDocs)
            {
                StatusMessage.Write("NEE - Named Entity Extraction was previously completed. Skipping.");
                return neeDocumentsFile;
            }

            StatusMessage.Write("NEE - Initializing NEMO Name Entity Extractor.");
            var nee = new NamedEntityExtractor(language, 0);

            using (var file = File.AppendText(neeDocumentsFile))
            {
                Document[] docsInQueue = new Document[QueueLength];
                int curDoc = 0;
                var documents = DocumentFeeders.Documents(tsvDocumentsFile, language, true).Skip(totalProcessed);
                StatusMessage.Write("NEE - Finished  NEMO initialization.");
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxThreads };
                System.Console.WriteLine("NEE - Using {0} degrees of parallelism. Skipping first {1} documents previously processed.", maxThreads, totalProcessed);

                foreach (var document in documents)
                {
                    // Load a batch of docs into memory
                    docsInQueue[curDoc++] = document;

                    // If the batch has reached the Queue Size proceed to NER in parallel
                    if (curDoc == QueueLength)
                    {
                        if (parallelOptions.MaxDegreeOfParallelism <= 1)
                            foreach (var doc in docsInQueue)
                            {
                                nee.NormalizeNamedEntities(doc);
                                System.Console.Write(".");
                            }
                        else
                        {
                            Parallel.ForEach(docsInQueue, parallelOptions, (doc) =>
                            {
                                nee.NormalizeNamedEntities(doc);
                                System.Console.Write(".");
                            });
                        }

                        foreach (var doc in docsInQueue)
                        {
                            file.WriteLine("{0}\t{1}\t{2}", doc.Id, doc.Title, doc.Text);
                        }
                        file.Flush();
                        totalProcessed += QueueLength;
                        System.Console.WriteLine(string.Format("{0} - Parsed {1,6:D0} TSV documents.", DateTime.Now, totalProcessed));
                        curDoc = 0;
                    }
                }

                // Process the last batch
                for (int i = 0; i < curDoc; i++)
                {
                    nee.NormalizeNamedEntities(docsInQueue[i]);
                    file.WriteLine("{0}\t{1}\t{2}", docsInQueue[i].Id, docsInQueue[i].Title, docsInQueue[i].Text);
                    System.Console.Write(docsInQueue[i].Id + ".");
                }
                System.Console.WriteLine(string.Format("{0} - Parsed {1,6:D0} TSV documents.", DateTime.Now, totalProcessed + curDoc));

            }

            StatusMessage.Write("NEE - Finished Name Entity Extraction.");
            return neeDocumentsFile;
        }


        private static int CountNeeProcessedDocs(string neeDocumentsFile, int countInputDocs)
        {
            if (!File.Exists(neeDocumentsFile))
            {
                return 0;
            }

            // NEE file exists.  Count how many docs were previously processed.
            int countProcessedDocs = File.ReadLines(neeDocumentsFile).Count();
            
            // If it wasn't fully processed last time...
            if ( (countProcessedDocs > 0) && (countProcessedDocs < countInputDocs) )
            {
                countProcessedDocs--;
                int count = 0;

                // Throw away last processed document in the NEE file becasue it might be corrupt.
                //      1) First rename file + ".bak"
                //      2) Then copy back to the orginal file only top N-1 documents
                //      3) Then delete the ".bak" file
                File.Copy(neeDocumentsFile, neeDocumentsFile + ".bak");
                using (var file = new StreamWriter(neeDocumentsFile))
                {
                    foreach (var line in File.ReadLines(neeDocumentsFile + ".bak"))
                    {
                        if (count >= countProcessedDocs)
                            break;
                        file.WriteLine(line);
                        count++;
                    }
                }
                
                File.Delete(neeDocumentsFile + ".bak");
            }
            return countProcessedDocs;
        }


        private static LDAConfig GenerateLdaDocumentTopicVectors(LDAConfig ldaConfig, IDictionary<string, string> parameters)
        {
            if (ldaConfig == null)
            {
                return null;
            }

            string inputDocuments;
            if (!parameters.TryGetValue(Options.InputFile, out inputDocuments))
            {
                StatusMessage.Write(string.Format("{0} is a required parameter.", Options.InputFile));
                return null;
            }

            string outputFilePath;
            return GenerateLdaDocumentTopicVectors(inputDocuments, out outputFilePath, ldaConfig, parameters);
        }

        private static LDAConfig GenerateLdaDocumentTopicVectors(string documentsFilePath, out string outputFilePath, LDAConfig ldaModelConfig, IDictionary<string, string> parameters)
        {
            if (!File.Exists(documentsFilePath))
            {
                StatusMessage.Write(string.Format("DVGen: Error. Input file not found - {0}", documentsFilePath));
                outputFilePath = string.Empty;
                return null;
            }
                        
            // Create a "test" version of the model's LDA config.
            // All folder paths will include the same parameter values of the model 
            // to help keep track of which model was used to generate these Doc Vectors
            
            var testLdaConfig = ExtensionMethods.DeepClone<LDAConfig>(ldaModelConfig);

            // But we need to add the SampleName used to train the model to avoid name collitions 
            // between two models that might have been trained using the same LDA Parameterscollition.
            testLdaConfig.ModelDirectory = documentsFilePath + @".dv\" + ldaModelConfig.SampleName + @"." + ldaModelConfig.modelName;
            testLdaConfig.CorpusVocabularyDirectory = testLdaConfig.ModelDirectory + @"\..\CorpusVocabulary\" + ldaModelConfig.SampleName + "."  +
                testLdaConfig.CorpusVocabularyDirectory.Substring(testLdaConfig.CorpusVocabularyDirectory.IndexOf(@"MinMaxWordFreq_"));

            // Now create the folder structure to store all intermediate files generated while processing the test set
            PrepareWorkingDirectories(testLdaConfig);
            string outputFileName = @"DocumentVectors";

            // Default is L2-normalized vectors (i.e. unit vectors)
            var l2Norm = true;
            string value;
            if (parameters.TryGetValue(Options.Normalization, out value) && value.ToLowerInvariant() == "l1")
            {
                l2Norm = false;
                outputFileName += ".L1";
            }

            var vectorEncoding = SerializationEncoding.Base64;
            if (parameters.TryGetValue(Options.Encoding, out value) && (value.ToLowerInvariant() == "text"))
            {
                vectorEncoding = SerializationEncoding.Text;
                outputFileName += ".Text";
            }
            else if (parameters.TryGetValue(Options.Encoding, out value) && (value.ToLowerInvariant() == "compress"))
            {
                vectorEncoding = SerializationEncoding.Compressed;
            }

            var vectorDensity = VectorType.Default;
            if (parameters.TryGetValue(Options.Compression, out value))
            {
                switch (value.ToLowerInvariant())
                {
                    case "d":
                        vectorDensity = VectorType.DenseVector;
                        break;
                    case "s":
                        vectorDensity = VectorType.SparseVector;
                        break;
                }
            }

            if (!parameters.TryGetValue(Options.OutputFile, out outputFilePath))
            {
                outputFilePath = testLdaConfig.ModelDirectory + @"\" + outputFileName + @".dv";
            }

            if (File.Exists(outputFilePath) && FileManager.GetFileLength(outputFilePath) > 0L)
            {
                StatusMessage.Write(string.Format("DVGen: Skipping processing - vectors file already exists {0}", outputFilePath));
                return testLdaConfig;
            }

            StatusMessage.Write(string.Format("DVGen: Processing file {0}", documentsFilePath));
            var lda = LDA.NewInstance(
                ldaModelConfig.LDAParameters.NumTopics,
                ldaModelConfig.Model,
                ldaModelConfig.CorpusVocabulary,
                ldaModelConfig.ModelStatistics.BadTopics,
                ldaModelConfig.Language);


            if (vectorDensity == VectorType.Default)
            {
                vectorDensity = lda.RecommendedCompressionType;
            }

            if (vectorDensity == VectorType.SparseVector)
            {
                outputFileName += ".Sparse";
            }


            // Load the CorpusVocabulary generated while training the model
            // We'll use it to featurize documents in the test set
            var corpusVocabulary = CorpusVocabulary.Load(ldaModelConfig.CorpusVocabulary);
            FeaturizeDocuments(testLdaConfig, documentsFilePath, corpusVocabulary);

            using (var file = new StreamWriter(outputFilePath))
            {
                var listOfDocumentFeatures = File.ReadLines(testLdaConfig.FeaturizedDocuments);                
                foreach (var documentFeatures in listOfDocumentFeatures)
                {                    
                    // Extract documentId from the features
                    var documentId = documentFeatures.Substring(0, documentFeatures.IndexOf('|'));

                    var ldaVector = lda.GetTopicAllocations(documentFeatures, true);
                    if (ldaVector == null)
                    {                        
                        file.WriteLine("");
                        continue;
                    }

                    if (l2Norm)
                    {
                        ldaVector.L2Normalize();
                    }
                    else
                    {
                        ldaVector.L1Normalize();
                    }

                    var serializedVector = ldaVector.Serialize(vectorDensity, vectorEncoding);
                    file.WriteLine(documentId + " " + ldaModelConfig.Locale + " " + ldaModelConfig.Corpus + " " + serializedVector);                    
                }
            }

            return testLdaConfig;
        }

        /// <summary>
        /// Compute metrics for the specified model
        /// </summary>
        /// <param name="parameters">command parameters</param>
        /// <returns>true if success, false otherwise</returns>
        private static bool GetModelMetrics(IDictionary<string, string> parameters)
        {
            var ldaModelConfig = GetLdaConfig(parameters);
            if (ldaModelConfig == null)
            {
                return false;
            }

            var skipExtrinsic = false;
            if (File.Exists(ldaModelConfig.ExtrinsicMetrics))
            {
                StatusMessage.Write(string.Format("Metrics calculator: Skipping. File already exists - {0}", ldaModelConfig.ExtrinsicMetrics));
                skipExtrinsic = true;
            }

            // If a test sample set was specified we need to compute the Model's Perplexity
            var perplexityMetricFilePath = string.Empty;
            var sampleName = GetSampleName(parameters);
            if (!string.IsNullOrEmpty(sampleName))
            {
                // Each sample tested has its own unique perplexity, so prefix the file name with the test Sample's name
                perplexityMetricFilePath = ldaModelConfig.PerplexityMetric.Replace(
                    @"\PerplexityMetric.tsv",
                    @"\" + sampleName + @".Perplexity.txt");

                if (File.Exists(perplexityMetricFilePath) && FileManager.GetFileLength(perplexityMetricFilePath) > 0L)
                {
                    StatusMessage.Write(string.Format("Metrics calculator: Skipping. File already exists - {0}", perplexityMetricFilePath));
                    perplexityMetricFilePath = string.Empty;
                }
            }

            if (skipExtrinsic && string.IsNullOrEmpty(perplexityMetricFilePath))
            {
                // No further computation needed.
                return true;
            }

            StatusMessage.Write("Metrics calculator: Initializing metrics module");
            var ldaMetrics = new LDAMetrics();
            if (!ldaMetrics.Initialize(ldaModelConfig))
            {
                StatusMessage.Write("Metrics calculator: Error initializing metrics module");
                return false;
            }

            //////////////////////////////////////
            // Compute extrinsic and intrinsic (if test sample is provided) metrics. 
            //////////////////////////////////////

            if (!skipExtrinsic)
            {
                int topWords = DefaultTCTopWords;
                if (parameters.ContainsKey(Options.TopWords))
                {
                    topWords = Convert.ToInt32(parameters[Options.TopWords]);
                }

                double epsilon = -1;
                if (parameters.ContainsKey(Options.Epsilon))
                {
                    epsilon = Convert.ToDouble(parameters[Options.Epsilon]);
                }

                // First the average Topic Coherence of the model
                double avgTC = ldaMetrics.ComputeAvgTopicsCoherence(topWords, epsilon);
                StatusMessage.Write(string.Format("Average topic coherence for top {0} words and {1} topics: {2}", topWords, ldaMetrics.NumTopics, avgTC));

                double avgTS = ldaMetrics.ComputeAvgTopicSpecificity();
                StatusMessage.Write(string.Format("Average topic specificity for {0} topics: {1}", ldaMetrics.NumTopics, avgTS));

                double avgTD = ldaMetrics.ComputeAvgTopicDistinctiveness();
                StatusMessage.Write(string.Format("Average topic distinctiveness for {0} topics: {1}", ldaMetrics.NumTopics, avgTD));

                // Generate brief summary of the model
                ldaModelConfig.ModelStatistics.BadTopics = ldaMetrics.GenerateModelSynopsis(ldaModelConfig.ExtrinsicMetrics);
                
                // Update the Model's LDA Config with the list of bad topics.  This will be used by DVGen to compress the vectors.
                File.WriteAllText(ldaModelConfig.LDAConfigFile, JsonConvert.SerializeObject(ldaModelConfig));
            }

            // Was a test corpus sample specified?
            if (!string.IsNullOrEmpty(perplexityMetricFilePath))
            {
                // Then we need to compute its perpexity against the model
                var testSampleFilepath = GetInputDocumentsFile(ldaModelConfig, parameters);

                string docVectorsFilePath;
                var testLdaConfig = GenerateLdaDocumentTopicVectors(testSampleFilepath, out docVectorsFilePath, ldaModelConfig, parameters);
                if (testLdaConfig != null)
                {
                    var perpexity = ldaMetrics.Perplexity(docVectorsFilePath, testLdaConfig.FeaturizedDocuments);
                    StatusMessage.Write(string.Format("Perpexity = {0}", perpexity));
                    File.WriteAllText(perplexityMetricFilePath, perpexity.ToString());
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static void GenerateSimilarittyScores_N_vs_All(int n, IDictionary<string, string> parameters)
        {
            var vectorFile = LoadVectorsFile(parameters, 0);
            if (vectorFile == null)
            {
                return;
            }
            vectorFile.Load();
            // The default Max number of items to compare is 2K (1,999,000 comparisons)
            int maxCountItemsToCompare = 2000;
            string value;
            if (parameters.TryGetValue(Options.DocumentCount, out value))
            {
                maxCountItemsToCompare = int.Parse(value);
            }

            bool printPadding = false;
            if (parameters.TryGetValue(Options.Padding, out value))
            {
                printPadding = bool.Parse(value);
            }

            StatusMessage.Write("Creating diagonal similarity matrix...");
            var similarityMatrix = vectorFile.GenerateSimDistMatrix(VectorMatrix.VectorFunction.Similarity, n, maxCountItemsToCompare);


            using (var file = new StreamWriter(vectorFile.FilePath + ".similarities.tsv"))
            {
                vectorFile.PrintMatrix(similarityMatrix, printPadding, file, null);
            }
        }

        private static void GeneratePairwiseSimilarittyScores(IDictionary<string, string> parameters)
        {
            var vectorFile = LoadVectorsFile(parameters, 1);
            var vectorFile2 = LoadVectorsFile(parameters, 2);
            if ((vectorFile == null) || (vectorFile2 == null))
            {
                return;
            }

            if (vectorFile.RowCount != vectorFile2.RowCount)
            {
                StatusMessage.Write("Error: The number of vectors in each input file must be equal.");
                return;
            }

            // Create a 1 x vectorCount matrix to store the pairwise similarity scores
            var similarityMatrix = new double[1][];
            similarityMatrix[0] = new double[vectorFile.RowCount];

            for (var i = 0; i < vectorFile.RowCount; i++)
            {
                similarityMatrix[0][i] = VectorBase.CosineSimilarity(vectorFile[i], vectorFile2[i]);
            }

            using (var file = new StreamWriter(vectorFile.FilePath + ".pairwise.similarities.tsv"))
            {
                vectorFile.PrintMatrix(similarityMatrix, false, file, vectorFile2);
            }
        }
        
        private static VectorMatrix LoadVectorsFile(IDictionary<string, string> parameters, int inputParameterRank)
        {
            string filePath = string.Empty;
            var paramName = (inputParameterRank <= 1) ? Options.InputFile : Options.InputFile + inputParameterRank;
            if (!parameters.TryGetValue(paramName, out filePath))
            {
                StatusMessage.Write("Error: Mising input document sample parameter.");
                return null;
            }

            var vectorFile = new VectorMatrix(filePath, 0);
            if (vectorFile.RowCount > 0)
            {
                return vectorFile; 
            }
            StatusMessage.Write("Error: Input document sample not found or is empty: " + filePath);
            return null;
        }


        private static void ConvertVectors(IDictionary<string, string> parameters)
        {
            var vectorFile = LoadVectorsFile(parameters, 0);
            if (vectorFile == null)
            {
                return;
            }
            vectorFile.Load();
            string outputFilePath = vectorFile.FilePath;

            // Default is L2-normalized vectors (i.e. unit vectors)
            var l2Norm = true;
            string value;
            if (parameters.TryGetValue(Options.Normalization, out value) && value.ToLowerInvariant() == "l1")
            {
                l2Norm = false;
                outputFilePath += ".L1";
            }

            var vectorEncoding = SerializationEncoding.Base64;
            if (parameters.TryGetValue(Options.Encoding, out value) && (value.ToLowerInvariant() == "text"))
            {
                vectorEncoding = SerializationEncoding.Text;
                outputFilePath += ".Text";
            }
            else if (parameters.TryGetValue(Options.Encoding, out value) && (value.ToLowerInvariant() == "compress"))
            {
                vectorEncoding = SerializationEncoding.Compressed;
                outputFilePath += ".Compressed";
            }

            var vectorDensity = VectorType.DenseVector;
            if (parameters.TryGetValue(Options.Compression, out value) && value.ToLowerInvariant() == "s")
            {
                    vectorDensity = VectorType.SparseVector;
                    outputFilePath += ".Sparse";
            }

            outputFilePath += @".dv";

            if (File.Exists(outputFilePath) && FileManager.GetFileLength(outputFilePath) > 0L)
            {
                StatusMessage.Write(string.Format("Warning: Skipping processing - vectors file already exists {0}", outputFilePath));
                return;
            }

            using (var file = new StreamWriter(outputFilePath))
            {
                for (int row = 0; row < vectorFile.RowCount; row++)
                {
                    var vector = vectorFile[row];
                    if (l2Norm)
                    {
                        vector.L2Normalize();
                    }
                    else
                    {
                        vector.L1Normalize();
                    }

                    var serializedVector = vector.Serialize(vectorDensity, vectorEncoding);
                    file.WriteLine(vectorFile.ItemId(row) + " unk unk " + serializedVector);
                }
            }
        }

        private static void Silhouette(IDictionary<string, string> parameters)
        {
            
            // Extract parameters:
            // * InputFile: Required the first time Silhouette runs.  Optional once the cluster partition files have been created
            // * SampleRate: Optional. Default = 1% (0.01)
            // * ClusterCount: Optional.  Default is to compute Silhouette on all clusters
            // * MaxMemory: Optional. Default is 3.0 Gigs.  Memory used to cached vectors from clusters.  Go to disk for overflow
            // * MaxThreads: Optional. Default is 8. Number of threads used to compute vector similarity

            string filePath = string.Empty;
            if (!parameters.TryGetValue(Options.InputFile, out filePath))
            {
                StatusMessage.Write("Warning: Missing input cluster file. This is OK if this is the second run and the cluster partitions were already created.");
                return;
            }

            // Default sample size is 1% 
            float sampleRate = 0.01F;
            string value = "";
            if (parameters.TryGetValue(Options.SampleRate, out value))
            {
                sampleRate = float.Parse(value);
            }

            int clusterCount = 0;
            if (parameters.TryGetValue(Options.ClusterCount, out value))
            {
                clusterCount = int.Parse(value);
            }

            float maxGigsOfMemory = 3.0F;
            if (parameters.TryGetValue(Options.MaxMemory, out value))
            {
                maxGigsOfMemory = float.Parse(value);
            }

            
            int maxThreads = 8;
            if (parameters.TryGetValue(Options.MaxThreads, out value))
            {
                maxThreads = int.Parse(value);
            }

            var S = new Silhouette(filePath, sampleRate, maxGigsOfMemory)
                {
                    MaxThreads = maxThreads
                };
            S.ComputeSilhouettes(clusterCount);
            
            // Print silhouettes for each cluster's individual vectors
            S.PrintSilhouettes();
        }


        // Takes a document corpus, one line per document, and splits it into multiple files
        // Parameters:
        //      FirstDoc        Optional, defautls to 1
        //      PartCount       Optional if DocCount is specified.  Otherwise it's the number of partitions to break input into
        //      DocCount        Optional, defaults to 1000 if PartCount is not specified.
        // If PartCount is specified and DocCount is not, the input will be broken into evenly sized partitions tarting from StartFromLine
        private static void PartitionInput(string inputFileName, IDictionary<string, string> parameters)
        {
            if (!File.Exists(inputFileName))
            {
                return;
            }

            int startFromLine = 1;
            int lineCount = 0, partitionCount = 0;

            string value = "";
            if (parameters.TryGetValue(Options.FirstDocument, out value))
            {
                startFromLine = int.Parse(value);
                if (startFromLine <= 0) startFromLine = 1;
            }

            if (parameters.TryGetValue(Options.DocumentCount, out value))
            {
                lineCount = int.Parse(value);
            }

            if (lineCount < 1)
            {
                if (parameters.TryGetValue(Options.PartitionCount, out value))
                {
                    partitionCount = int.Parse(value);
                }

                if (partitionCount > 0)
                {
                    // Compute size of each partition based on the size of the input
                    int documentCount = File.ReadLines(inputFileName).Count();
                    lineCount = (documentCount - startFromLine + 1) / partitionCount;
                    if ((documentCount % partitionCount) > 0) lineCount++;
                }
                else
                {
                    lineCount = 1000;
                    partitionCount = 1;
                }
            }
            else
            {
                partitionCount = 1;
            }

            string extension = Path.GetExtension(inputFileName);
            string outFileNameFormat = Path.GetDirectoryName(inputFileName) + "\\" + Path.GetFileNameWithoutExtension(inputFileName) + ".{0}.{1}.{2}" + extension;

            for (int partId = 0; partId < partitionCount; partId++)
            {
                var fileInput = File.ReadLines(inputFileName).Skip(startFromLine - 1);
                string outFileName = string.Format(outFileNameFormat, lineCount, partId, startFromLine);
                CreateDataPartition(fileInput, outFileName, startFromLine, lineCount);
                startFromLine += lineCount;
            }
        }


        private static void CreateDataPartition(IEnumerable<string> fileInput, string outFileName, int startFromLine, int lineCount)
        {
            using (var fileOut = new StreamWriter(outFileName))
            {
                int count = 0;
                foreach (var line in fileInput)
                {
                    if (count >= lineCount)
                        break;
                    fileOut.WriteLine(line);
                    count++;
                }
            }
        }


    }
}
