///
///
///
namespace ConvertModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.IO;
    using System.Configuration;

    class Program
    {
        private enum BadConditions { badParameneters, badDbConnection };
        
        static void Main(string[] args)
        {

            uint maxDocumentsToLoad                             = Convert.ToUInt32(ConfigurationManager.AppSettings["maxDocumentsToLoad"]);    ////Number of items to read.  Defaults to 1000
            string outputFolderName                             = ConfigurationManager.AppSettings["outputFolderName"];
            
            // Input model files
            string documentTermFrequenciesInputFile             = ConfigurationManager.AppSettings["documentTermFrequenciesInputFile"];
            string documentTopicAllocationsInputFile            = ConfigurationManager.AppSettings["documentTopicAllocationsInputFile"];
            string wordTopicAllocationsInputFile                = ConfigurationManager.AppSettings["wordTopicAllocationsInputFile"];
            uint countOfHeaderLinesInWordTopicAllocationsFile = Convert.ToUInt32(ConfigurationManager.AppSettings["countOfHeaderLinesInWordTopicAllocationsFile"]);    

            
            // Names of truncated model files, after processing. These will be placed under outputFolderName.
            // They are the input into the Dtsx jobs that will load them into the TopicBrowser Db
            string truncatedDocumentTermFrequenciesOutputFile   = ConfigurationManager.AppSettings["truncatedDocumentTermFrequenciesOutputFile"];
            string truncatedDocumentTopicAllocationsOutputFile  = ConfigurationManager.AppSettings["truncatedDocumentTopicAllocationsOutputFile"];
            string truncatedWordTopicAllocationsOutputFile      = ConfigurationManager.AppSettings["truncatedWordTopicAllocationsOutputFile"];

            // Read command line arguments into local vars
            string modelLocationPath = "";
            uint trainingIterations = 1;
            uint linesToSkip = 0;
            uint vocabularySize = 0;
            bool goodParameters = true;
            
            int argumentCount = args.Length;
            try
            {
                switch (argumentCount)
                {
                    case 1:
                        modelLocationPath = args[0]; 
                        break;
                    case 2:
                        modelLocationPath = args[0]; 
                        maxDocumentsToLoad = Convert.ToUInt32(args[1]);
                        break;
                    case 3:
                        modelLocationPath = args[0]; 
                        maxDocumentsToLoad = Convert.ToUInt32(args[1]);
                        trainingIterations = Convert.ToUInt32(args[2]);
                        break;
                    case 4:
                        modelLocationPath = args[0]; 
                        maxDocumentsToLoad = Convert.ToUInt32(args[1]);
                        trainingIterations = Convert.ToUInt32(args[2]);
                        vocabularySize = Convert.ToUInt32(args[3]);
                        break;
                    default:
                        goodParameters = false;
                        break;
                };
            }
            catch
            {
                goodParameters = false; 
            }

            if (!goodParameters)
            {
                Help(BadConditions.badParameneters);
                return;
            }

            string modelFilepath;
            try 
            {
                if (!Directory.Exists(modelLocationPath + "\\" + outputFolderName))
                    Directory.CreateDirectory(modelLocationPath + "\\" + outputFolderName);
                
                //Open ParseDocumentTermFrequencies
                modelFilepath = modelLocationPath + @"\\" + documentTermFrequenciesInputFile;
                string processedModelFilepath = modelLocationPath + @"\\" + outputFolderName + "\\" + truncatedDocumentTermFrequenciesOutputFile;

                uint documentId = 0;
                if (CheckFilesAndPaths(modelFilepath, processedModelFilepath)) 
                {
                    using (StreamWriter sw = new StreamWriter(processedModelFilepath)) 
                    {
                        using (StreamReader sr = new StreamReader(modelFilepath)) 
                        {
                            uint lineCount = 0;
                            while (sr.Peek() >= 0 ) 
                                 if (lineCount++ < maxDocumentsToLoad)
                                     ParseDocumentTermFrequencies(documentId++, sr.ReadLine(), sw);
                                 else
                                 {
                                     sr.ReadLine();                             ////Need to count total number of documents there are ...
                                     ////lineCount++; 
                                 }
                            linesToSkip = lineCount * (trainingIterations - 1); ////...so we know how many lines to skip in DocumentTopicAllocations file
                        }
                    }
                }

                //Open DocumentTopicAllocations
                modelFilepath = modelLocationPath + @"\\" + documentTopicAllocationsInputFile;
                processedModelFilepath = modelLocationPath + "\\" + outputFolderName + "\\" + truncatedDocumentTopicAllocationsOutputFile;
                if (CheckFilesAndPaths(modelFilepath, processedModelFilepath))
                {
                    using (StreamWriter sw = new StreamWriter(processedModelFilepath))
                    {
                        using (StreamReader sr = new StreamReader(modelFilepath)) 
                        {
                            uint linesSkipped = 0;
                            documentId = 0;
                            while ((sr.Peek() >= 0) && (documentId < maxDocumentsToLoad))
                                if (linesSkipped < linesToSkip)
                                {
                                    sr.ReadLine();
                                    linesSkipped++;
                                } 
                                else
                                    ParseDocumentTopicProbabilities(documentId++, sr.ReadLine(), sw);
                        }
                    }
                } 

                //Open ParseTermToTopicAllocations
                modelFilepath = modelLocationPath + @"\\" + wordTopicAllocationsInputFile;
                processedModelFilepath = modelLocationPath + "\\" + outputFolderName + "\\" + truncatedWordTopicAllocationsOutputFile;
                if (CheckFilesAndPaths(modelFilepath, processedModelFilepath))
                {
                    using (StreamWriter sw = new StreamWriter(processedModelFilepath))
                    {
                        using (StreamReader sr = new StreamReader(modelFilepath)) 
                        {
                            uint linesSkipped = 0;
                            uint termId = 0;
                            while (sr.Peek() >= 0 && (termId < vocabularySize))
                                if (linesSkipped < countOfHeaderLinesInWordTopicAllocationsFile)
                                {
                                    sr.ReadLine();
                                    linesSkipped++;
                                }
                                else 
                                    ParseTermToTopicProbabilities(sr.ReadLine(), sw, termId++);
                        }
                    }
                } 
            }
            catch (Exception e) 
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }


        private static bool CheckFilesAndPaths(string modelFilepath, string processedModelFilepath)
        {
            if (File.Exists(modelFilepath))
            {
                if (File.Exists(processedModelFilepath))
                    File.Delete(processedModelFilepath);
                return true;
            }
            return false;
        }


        private static void ParseDocumentTermFrequencies(uint documentId, String sourceText, StreamWriter sw)
        {

            int iTouple = 0;
            sourceText.Trim();
            string[] termFrequencies = sourceText.Split(' ');

            foreach (string wordFrquencyTouple in termFrequencies)
            {
                if (iTouple != 0)  //Skip first "|" character
                {
                    int colonPos = wordFrquencyTouple.IndexOf(':');
                    string termId = wordFrquencyTouple.Substring(0, colonPos);
                    string termFrequency = wordFrquencyTouple.Substring(colonPos + 1);

                    sw.WriteLine(String.Format("{0}\t{1}\t{2}", documentId, termId, termFrequency));
                }
                else iTouple = 1;
            }
        }

        // =============================================
        // Author:		OswaldoR
        // Description:	Data loading helper function.
        //				Parses values from a delimited string  & return the result as an indexed table
        // =============================================
        private static void ParseDocumentTopicProbabilities(uint documentId, String sourceText, StreamWriter sw )
        {
            int topicId = 0;
            string[] topicProbabilities = sourceText.Trim().Split(' ');

            foreach (string topicProbability in topicProbabilities)
                sw.WriteLine (String.Format("{0}\t{1}\t{2}", documentId, topicId++, topicProbability));
        }

        // =============================================
        // Author:		OswaldoR
        // Description:	Data loading helper function.
        //				Parses values from a delimited string  & inserts results in TopicToTerm table
        // =============================================
        private static void ParseTermToTopicProbabilities(String termProbabilities, StreamWriter sw, uint termId)
        {
            bool firstColumn = true;
            int topicId = 0;
            double termProbability;

            string[] probabilities = termProbabilities.Trim().Split(' ');

            foreach (string probability in probabilities)
            {
                if (firstColumn)  //First column is the TermId.  Skip it
                    firstColumn = false;
                else
                {
                    termProbability = Convert.ToDouble(probability);
                    sw.WriteLine (String.Format("{0}\t{1}\t{2}", termId, topicId++, termProbability));
                }
            }
        }

        private static void Help(BadConditions condition)
        {
            switch (condition)
            {
                case BadConditions.badParameneters:
                    Console.Error.WriteLine("Bad parameters. The options are:");
                    Console.Error.WriteLine("   <ModelLocationPath> [LineCount]       Location of Model files. \n\t\t\t\tCount is optional and defaults to 1000. \n\t\t\t\tCount=0 returns all.");
                    Console.Error.WriteLine("\nExample:\n   C:>ConvertModel \\\\oswaldo-server2\\LDAModels\n");
                    break;
                case BadConditions.badDbConnection:
                    break;
                default:
                    Console.Error.WriteLine("Error: Unexpected condition.  You shouldn't be seeing this, so this is pretty bad!");
                    break;
            }
        }
    }
}
