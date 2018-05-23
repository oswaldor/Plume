using System.Collections.Generic;
using Microsoft.Content.TextProcessing;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;


namespace Microsoft.Content.TopicExtraction
{
    using System;

    public static class ExtensionMethods
    {
        // Deep clone
        public static T DeepClone<T>(this T a)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, a);
                stream.Position = 0;
                return (T)formatter.Deserialize(stream);
            }
        }
    }
    
    [Serializable]
    public class LDAConfig
    {
        private static Dictionary<string, Language> localeLangMap = new Dictionary<string, Language>()
        {
            {"en", Language.English},
            {"pt", Language.Portuguese},
            {"fr", Language.French},
            {"it", Language.Italian},
            {"de", Language.German},
            {"es", Language.Spanish},
            {"zhs", Language.Chinese_Simplified},
            {"zht", Language.Chinese_Traditional},
            {"ru", Language.Russian},
            {"ja", Language.Japanese}
            // TO DO: add more mappings for other languages.
        };

        private string modelDirectory;

        public string ModelRepositoryPath { get; set; }

        public string Corpus { get; set; }

        public string Locale { get; set; }

        public string SampleName { get; set; }

        public LDAParameters LDAParameters { get; set; }

        public FeaturizationParameters FeaturizationParameters { get; set; }

        public ModelStatistics ModelStatistics { get; set; }

        private string corpusVocabularyDirectory;

        /// <summary>
        /// Base directory for all files related to this model
        /// </summary>
        public string ModelDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.modelDirectory))
                {
                    return string.Format(@"{0}\Models\LDA\{1}\{2}\{3}\{4}", this.ModelRepositoryPath, this.Locale, this.Corpus, this.SampleName, this.modelName);
                }

                return this.modelDirectory;
            }

            set
            {
                this.modelDirectory = value;
            }
        }

        /// <summary>
        /// Model file name
        /// </summary>
        public string Model
        {
            get
            {
                return string.Format(@"{0}\{1}.bin", this.ModelDirectory, this.modelName);
            }
        }

        public string LDAConfigFile
        {
            get
            {
                return string.Format(@"{0}\{1}.LDAConfig.json", this.ModelDirectory, this.modelName);
            }
        }

        public string CorpusVocabularyRaw
        {
            get
            {
                return this.corpusVocabularyBaseDirectory + @"\CorpusVocabulary.raw" + this.NEE + ".tsv";
            }
        }

        public string CorpusVocabulary
        {
            get
            {
                return this.CorpusVocabularyDirectory + @"\CorpusVocabulary.tsv";
            }
        }

        public string CorpusVocabularyDropped
        {
            get
            {
                return this.CorpusVocabularyDirectory + @"\CorpusVocabulary.dropped.tsv";
            }
        }

        public string FeaturizedDocuments
        {
            get
            {
                return this.CorpusVocabularyDirectory + @"\FeaturizedDocuments.txt";
            }
        }

        public string DocumentVocabularies
        {
            get
            {
                return this.corpusVocabularyBaseDirectory + @"\DocumentVocabularies" + this.NEE + ".txt";
            }
        }

        public string ExtrinsicMetrics
        {
            get
            {
                return this.modelMiscDirectory + @"\ExtrinsicMetrics.tsv";
            }
        }

        public string ExtrinsicMetricsProcessed
        {
            get
            {
                return this.modelMiscDirectory + @"\ExtrinsicMetrics.processed.tsv";
            }
        }

        public string PerplexityMetric
        {
            get
            {
                return this.modelMiscDirectory + @"\PerplexityMetric.tsv";
            }
        }

        public string DocumentTopicAllocations
        {
            get
            {
                return this.modelMiscDirectory + @"\DocumentTopicAllocations.txt";
            }
        }

        public string WordTopicAllocations
        {
            get
            {
                return this.modelMiscDirectory + @"\WordTopicAllocations.txt";
            }
        }

        public Language Language
        {
            get
            {
                return Locale2Language(this.Locale);
            }
        }

        public string modelName
        {
            get
            {
                return string.Format("{0}{1}_{2}_{3}_{4}_{5}_{6}_{7}_{8}_{9}",
                    this.LDAParameters.NumTopics,
                    this.NEE,
                    this.FeaturizationParameters.MinWordDocumentFrequency,
                    this.FeaturizationParameters.MaxRalativeWordDocumentFrequency,
                    this.LDAParameters.Alpha,
                    this.LDAParameters.Rho,
                    this.LDAParameters.Minibatch,
                    this.LDAParameters.Passes,
                    this.LDAParameters.InitialT,
                    this.LDAParameters.PowerT);
            }
        }

        private string modelMiscDirectory
        {
            get
            {
                return this.ModelDirectory + @"\build";
            }
        }

        private string corpusVocabularyBaseDirectory
        {
            get
            {
                return this.ModelDirectory + @"\..\CorpusVocabulary";
            }
        }

        // Optional NamedEntityExtractor was specified?
        private string NEE
        {
            get
            {
                if (this.FeaturizationParameters.NamedEntityNormalization)
                {
                    return "_NEE";
                }
                return string.Empty;
            }
        }

        public string CorpusVocabularyDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.corpusVocabularyDirectory))
                {
                    
                    return string.Format(
                        @"{0}\MinMaxWordFreq_{1}_{2}{3}",
                        this.corpusVocabularyBaseDirectory,
                        this.FeaturizationParameters.MinWordDocumentFrequency,
                        this.FeaturizationParameters.MaxRalativeWordDocumentFrequency,
                        this.NEE);
                }
                else
                {
                    return this.corpusVocabularyDirectory;
                }
            }

            set
            {
                this.corpusVocabularyDirectory = value;

            }
        }

        public static Language Locale2Language(string locale)
        {
            var localeParts = locale.Split('-');
            if (localeLangMap.ContainsKey(localeParts[0]))
            {
                return localeLangMap[localeParts[0]];
            }

            throw new System.ArgumentException("Specified locale not supported!");
        }
    }
}
