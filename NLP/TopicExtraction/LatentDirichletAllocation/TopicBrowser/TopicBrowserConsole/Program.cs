
namespace TopicBrowserConsole
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using TopicModelDbLib;

    class Program
    {
        private enum BadConditions { badParameneters, badDbConnection };
        
        static void Main(string[] args)
        {
            uint id = 0;            //Topic or Document Id parameter
            uint count = 1000;      //Number of items to return.  Defaults to 1000
            uint sortBy = 0;
            bool goodParameters = true;
            Term[] terms = null;
            Document[] documents = null;
            
            int argumentCount = args.Length;
            try
            {
                switch (argumentCount)
                {
                    case 1:
                        if (args[0].ToLower() !=  "-l")
                            goodParameters = false; //-L is the only action that accepts no additional params.
                        break;
                    case 2:
                        id = Convert.ToUInt32(args[1]); //Topic or Document Id
                        break;
                    case 3:
                        id = Convert.ToUInt32(args[1]); //Topic or Document Id
                        sortBy = count = Convert.ToUInt32(args[2]);
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

            //ModelDatabase modeldb = null;
            ModelDatabase modeldb = null;

            //Read Model db connection parameters from web.config 
            string sqlServerName = ConfigurationManager.AppSettings["sqlServerName"];
            string databaseName = ConfigurationManager.AppSettings["databaseName"];

                //Connect to the Model database
            modeldb = ModelDatabase.Instance;
            modeldb.Open(sqlServerName, databaseName);

            if ((modeldb == null) || !modeldb.validConnection)
            {
                Help(BadConditions.badDbConnection);
                return;
            }
            
            //Interpret action argument: 
            //  D2W: DocumentId to Words
            //  T2W: TopicId to Words
            //  D2T: DocumentId to TOpics
            //  T2D: TopicId to Documents
            //  D2D: Document to Document ranked by similarity
            switch (args[0].ToLower())
            {
                case "-l":
                    Dictionary<uint, Topic> topics = modeldb.GetTopics();
                    if (topics != null)
                    {
                        Console.WriteLine("\nId\tDoc Count\tLabel\n======================================================");
                        foreach (Topic t in topics.Values)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2}", t.id, t.documentCount, t.label));
                    }
                    break;
                case "-d":
                    Document doc = modeldb.GetDocument(id, sortBy);
                    if (doc != null)
                    {
                        Console.WriteLine("\nURI: {0}\tTitle: \"{1}\"", doc.uri, doc.title);
                        Console.WriteLine("\nTerm\tTFIDF\tFrequency\tCorpusFrequency\tDocumentFrquency\n===========================================================================");
                        foreach (Term t in doc.terms)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2}\t{3}\t{4}", t.term, t.tfidf, t.frequency, t.corpusFrequency, t.documentFrequency));
                    }
                    break;
                case "-d2w":
                    terms = modeldb.GetTermsFromDocumentId(id, sortBy);
                    if (terms != null)
                    {
                        Console.WriteLine("\nTerm\tTFIDF\tFrequency\tCorpusFrequency\tDocumentFrquency\n===========================================================================");
                        foreach (Term t in terms)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2}\t{3}\t{4}", t.term, t.tfidf, t.frequency, t.corpusFrequency, t.documentFrequency));
                    }
                    break;
                case "-t2w":
                    terms = modeldb.GetTermsFromTopicId(id, count);
                    if (terms != null)
                    {
                        Console.WriteLine("\nTerm\tProbability\tCorpusFrequency\tDocumentFrquency\n===========================================================================");
                        foreach (Term t in terms)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2}\t{3}", t.term, t.probability, t.corpusFrequency, t.documentFrequency));
                    }
                    break;
                case "-t2d":
                    documents = modeldb.GetDocumentsFromTopicId(id, count);
                    if (documents != null)
                    {
                        Console.WriteLine("\nID \tURI \tProbability \tTitle\n===========================================================================");
                        foreach (Document document in documents)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2:N7}\t{3}", document.id, document.uri, document.probability, document.title));
                    }
                    break;
                case "-d2t":
                    double[] topicVector = modeldb.GetTopicVectorFromDocumentId(id);
                    if (topicVector != null)
                    {
                        Console.WriteLine("\nTopic Signature\n================");
                        foreach (double topicProbability in topicVector)
                            Console.WriteLine(String.Format("{0:N7}", topicProbability));
                    }
                    break;
                case "-d2d":
                    documents = modeldb.GetSimilarDocumentsFromDocumentId(id, count);
                    if (documents != null)
                    {
                        Console.WriteLine("\n ID \tURI \tSimilarity \tTitle\n===========================================================================");
                        foreach (Document document in documents)
                            Console.WriteLine(String.Format("{0}\t{1}\t{2:N7}\t{3}", document.id, document.uri, document.probability, document.title));
                    }
                    break;
                default:
                    Help(BadConditions.badParameneters);
                    break;
            };
            
        }

        private static void Help(BadConditions condition)
        {
            switch (condition)
            {
                case BadConditions.badParameneters:
                    Console.Error.WriteLine("Bad parameters. The options are:");
                    Console.Error.WriteLine("   -L\t\t\t    List topics.");
                    Console.Error.WriteLine("   -D <DocumentId>              Document meta-data.");
                    Console.Error.WriteLine("   -T2W <TopicId> [Count]       List Words associated with <TopicId>. \n\t\t\t\tCount is optional and defaults to 1000. \n\t\t\t\tCount=0 returns all.");
                    Console.Error.WriteLine("   -T2D <TopicId>  [Count]      List documents tagged with <TopicId>, \n\t\t\t\tin descending order of affinity. \n\t\t\t\tCount is optional and defaults to 1000. \n\t\t\t\tCount=0 returns all.");
                    Console.Error.WriteLine("   -D2T <DocumentId>\t        Topic vector \"Signature\" for <DocumentId>.");
                    Console.Error.WriteLine("   -D2D <DocumentId> [Count]    Documents similar to <DocumentId>, in descending\n\t\t\t\torder of similarity. \n\t\t\t\tCount is ptional and defaults to 1000.");
                    Console.Error.WriteLine("   -D2W <DocumentId> [SortBy]   List a document's salient words, sorted by \n\t\t\t\tTFIDF. To sort by in-document frequency set \n\t\t\t\tSortBy to 1.");
                    Console.Error.WriteLine("\nExample:\n   C:>TopicBrowser -T2W 9 100\n");
                    break;
                case BadConditions.badDbConnection:
                    Console.Error.WriteLine("Error: Unable to connect to the Topic Model database. Check connection parametrs in the app.config and make sure you have read permissions on the database.");
                    break;
                default:
                    Console.Error.WriteLine("Error: Unexpected condition.  You shouldn't be seeing this, so this is pretty bad!");
                    break;
            }
        }
    }
}
