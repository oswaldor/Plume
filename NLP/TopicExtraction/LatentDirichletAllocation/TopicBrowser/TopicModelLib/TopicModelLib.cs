using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace TopicModelDbLib
{
    public class Topic
    {
        public uint id = 0;
        public int documentCount = 0;
        public string label = "";
        public Term[] terms = null;
        public Document[] documents = null;
        public String termsJSON = null;
    }

    public class Term
    {
        public string term;
        public double tfidf;
        public double probability;
        public int frequency;
        public int corpusFrequency;
        public int documentFrequency;
    }

    public class Document
    {
        public int id;
        public string uri;
        public string title;
        public double probability;
        public double[] topicProbabilities;
        public string topicProbabilitiesJSON;
        public Term[] terms;
        public string termsJSON;
        public Document[] relatedDocuments;
    }

    
    public class ModelDatabase
    {
        private static ModelDatabase model = null;
        public static ModelDatabase Instance
        {
            get
            {
                if (model == null)
                    model = new ModelDatabase();

                return model;
            }
        }

        //Model Statistics
        public Dictionary<uint, Topic> topics = null;

        private const string connectionStringTemplate = "server={0};database={1};Trusted_Connection=yes";
        private SqlConnection topicDbConnection = null;

        private String serverName = null;
        private String databaseName = null;
        private SqlDataAdapter topicModelStatistics;
        private SqlDataAdapter documentTopicVectorSQLAdapter;
        private SqlDataAdapter similarDocumentsFromDocumentIdSQLAdapter;
        private SqlDataAdapter termsFromDocumentIdSQLAdapter;
        private SqlDataAdapter termsFromTopicIdSQLAdapter;
        private SqlDataAdapter documentsFromTopicIdSQLAdapter;
        private SqlDataAdapter getTopics;
        private SqlDataAdapter getDocumentFromDocumentIdSQLAdapter;
        public bool validConnection = false;
        
        //Model Statistics
        public string modelName = null;
        public int topicCount;
        public int documentCount;
        public int termCount;

        //Constructor
        private ModelDatabase()
        {
        }

        public ModelDatabase(string name, string sqlServerName, string sqlDatabaseName, bool blnOpen)
        {
            modelName = name;
            serverName = sqlServerName;
            databaseName = sqlDatabaseName; 
            if (blnOpen) Open(sqlServerName, databaseName);
        }

        public bool Open()
        {
            return Open(serverName, databaseName);
        }

        public bool Open(string sqlServerName, string databaseName)
        {
            if ((sqlServerName != null) && (databaseName != null) && (sqlServerName.Length > 0) && (databaseName.Length > 0))
            {
                string connectionString = String.Format(connectionStringTemplate, sqlServerName, databaseName);
                try
                {
                    topicDbConnection = new SqlConnection(connectionString);

                    //Create DataAdapters for each stored procedure.
                    topicModelStatistics = new SqlDataAdapter("spGetTopicModelStatistics", topicDbConnection);
                    documentTopicVectorSQLAdapter = new SqlDataAdapter("spGetTopicVectorFromDocumentId", topicDbConnection);
                    termsFromDocumentIdSQLAdapter = new SqlDataAdapter("spGetTermsFromDocumentId", topicDbConnection);
                    termsFromTopicIdSQLAdapter = new SqlDataAdapter("spGetTermsFromTopicId", topicDbConnection);
                    documentsFromTopicIdSQLAdapter = new SqlDataAdapter("spGetDocumentsFromTopicId", topicDbConnection);
                    similarDocumentsFromDocumentIdSQLAdapter = new SqlDataAdapter("spGetSimilarDocumentsFromDocumentId", topicDbConnection);
                    getTopics = new SqlDataAdapter("spGetTopics", topicDbConnection);
                    getDocumentFromDocumentIdSQLAdapter = new SqlDataAdapter("spDocumentFromDocumentId", topicDbConnection);

                    topicModelStatistics.SelectCommand.CommandType = CommandType.StoredProcedure;
                    documentTopicVectorSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    termsFromDocumentIdSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    termsFromTopicIdSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    documentsFromTopicIdSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    similarDocumentsFromDocumentIdSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    getTopics.SelectCommand.CommandType = CommandType.StoredProcedure;
                    getDocumentFromDocumentIdSQLAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;

                    //Create Parameters collection for the stored procedure.
                    documentTopicVectorSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@documentId", SqlDbType.Int, 4));

                    termsFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@documentId", SqlDbType.Int, 4));
                    termsFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@sortBy", SqlDbType.Int, 4)); //TFID is default (0).  1=In Document Frequency
                    termsFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@sortOrder", SqlDbType.Int, 4)); //Descending by default (0)

                    termsFromTopicIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@topicId", SqlDbType.Int, 4));
                    termsFromTopicIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@count", SqlDbType.Int, 4));
                    termsFromTopicIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@mostProbable", SqlDbType.Int, 4));

                    documentsFromTopicIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@topicId", SqlDbType.Int, 4));
                    documentsFromTopicIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@count", SqlDbType.Int, 4));
                    similarDocumentsFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@documentId", SqlDbType.Int, 4));
                    similarDocumentsFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@count", SqlDbType.Int, 4));
                    getDocumentFromDocumentIdSQLAdapter.SelectCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.Int, 4));

                    GetTopicModelStatistics();
                    validConnection = true;
                    //Cache all topics and their term probabilities.
                    topics = GetTopics();
                }
                catch
                {
                    validConnection = false;    //failure connecting to db or creating SQL adapters for stored procs.
                }
            }
            return validConnection;
        }

        ~ModelDatabase()
        {
            //Dispose of the DataAdapters.
            documentTopicVectorSQLAdapter.Dispose(); 
            termsFromDocumentIdSQLAdapter.Dispose();
            termsFromTopicIdSQLAdapter.Dispose();
            documentsFromTopicIdSQLAdapter.Dispose();
            similarDocumentsFromDocumentIdSQLAdapter.Dispose();
            topicModelStatistics.Dispose();
            getTopics.Dispose();
            getDocumentFromDocumentIdSQLAdapter.Dispose();

		    topicDbConnection.Close(); //Close the connection.
            topicDbConnection.Dispose();
        }

        //Db read methods
        public void GetTopicModelStatistics()
        {
            DataSet ds = new DataSet();
            //Fill the DataSet with the rows that are returned.
            topicModelStatistics.Fill(ds, "TopicModelStatistics");

            DataRow dr = ds.Tables["TopicModelStatistics"].Rows[0];
            topicCount      = dr.Field<int>("topicCount");
            documentCount   = dr.Field<int>("documentCount");
            termCount       = dr.Field<int>("termCount");
        }

        public static int sizeScale(double probability, double previousProbability, int currentFontSize)
        {
            int size = currentFontSize;
            if (probability < previousProbability) --size;
            if (size < 3) size = 3;
            return size;
        }

        public Dictionary<uint, Topic> GetTopics()
        {
            if (!validConnection) return null;
            if (topics != null) return topics;  //Return cached version

            DataTable table = null;
            try
            {
                //Execute stored proc and fill the DataSet
                DataSet ds = new DataSet();
                getTopics.Fill(ds, "Topics");
                table = ds.Tables["Topics"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            if ((table != null) && (table.Rows.Count > 0))
            {
                //Populate a vector of doubles with the topic probabilities
                topics = new Dictionary<uint, Topic>(); //[table.Rows.Count];
                foreach (DataRow topicRow in table.Rows)
                {
                    Topic topic = new Topic();
                    topic.id = (uint) topicRow.Field<int>("Id");
                    topics.Add(topic.id, topic); 
                    topic.documentCount = topicRow.Field<int>("documentCount");
                    topic.label = topicRow.Field<string>("label");
                    topic.documents = GetDocumentsFromTopicId(topic.id, 200);
                    topic.terms = this.GetTermsFromTopicId(topic.id, 100);

                    String topicsTermsJSON = "[";
                    if (topic.terms != null)
                    {
                        int currentFontSize = 81;
                        double lastProbability = 1;
                        foreach (Term t in topic.terms)
                        {
                            topicsTermsJSON = topicsTermsJSON + "{\"text\":\"" + t.term + "\",\"size\":" + Convert.ToString(currentFontSize = sizeScale(t.probability, lastProbability, currentFontSize)) + "},";
                            lastProbability = t.probability;
                        }
                    }
                    topic.termsJSON = topicsTermsJSON.Substring(0, topicsTermsJSON.Length - 1) + "]";  //Remove last comma
                    
                }
            }
            return topics;
        }

        public double[] GetTopicVectorFromDocumentId(uint documentId)
        {
            //DocumentId is a zero-indexed value over the document set.  It cannot be larger than the number of documents in the collection
            if (!validConnection || (documentId >= documentCount)) return null;

            DataTable table = null;
            try
            {
                //Assign values to Parameters collection for the stored procedure
                documentTopicVectorSQLAdapter.SelectCommand.Parameters["@documentId"].Value = documentId;

                //Execute stored proc and fill the DataSet
                DataSet ds = new DataSet();
                documentTopicVectorSQLAdapter.Fill(ds, "TopicVectorFromDocument");
                table = ds.Tables["TopicVectorFromDocument"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            if ((table != null) && (table.Rows.Count > 0))
            {
                if (table.Rows.Count != topicCount )    
                    throw new IndexOutOfRangeException();
                else 
                {
                    //Populate a vector of doubles with the topic probabilities
                    double[] topicVector = new double[topicCount];
                    int topicId = 0;
                    foreach (DataRow probabilityRow in table.Rows)
                        topicVector[topicId++] = probabilityRow.Field<double>(1);
                    return topicVector;
                }
            }
            return null;
        }

        public Document GetDocument(uint documentId, uint sortTermsBy)
        {
            //DocumentId is a zero-indexed value over the document set.  It cannot be larger than the number of documents in the collection
            if (!validConnection || (documentId >= documentCount)) return null;

            DataTable table = null;
            try
            {
                //Assign values to Parameters collection for the stored procedure.
                getDocumentFromDocumentIdSQLAdapter.SelectCommand.Parameters["@id"].Value = documentId;
                //Fill the DataSet with the rows that are returned.
                DataSet ds = new DataSet();
                getDocumentFromDocumentIdSQLAdapter.Fill(ds, "Document");
                table = ds.Tables["Document"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            if ((table != null) && (table.Rows.Count > 0))
            {
                Document document = new Document();
                foreach (DataRow row in table.Rows)
                {
                    document.id = row.Field<int>("id");
                    document.uri = row.Field<string>("uri");
                    document.title = row.Field<string>("title");
                    document.terms = GetTermsFromDocumentId(documentId, sortTermsBy);
                    document.topicProbabilities = GetTopicVectorFromDocumentId(documentId);
                }
                return document;
            }
            return null;
        }

        public Term[] GetTermsFromDocumentId(uint documentId, uint sortBy)
        {
            //DocumentId is a zero-indexed value over the document set.  It cannot be larger than the number of documents in the collection
            if (!validConnection || (documentId >= documentCount)) return null;

            DataTable table = null;
            try
            {
                //Assign values to Parameters collection for the stored procedure.
                termsFromDocumentIdSQLAdapter.SelectCommand.Parameters["@documentId"].Value = documentId;
                termsFromDocumentIdSQLAdapter.SelectCommand.Parameters["@sortBy"].Value = sortBy;    //TFID is default (0).  1=In Document Frequency
                termsFromDocumentIdSQLAdapter.SelectCommand.Parameters["@sortOrder"].Value = 0;    //Descending by default (0)

                //Fill the DataSet with the rows that are returned.
                DataSet ds = new DataSet();
                termsFromDocumentIdSQLAdapter.Fill(ds, "TermsFromDocument");
                table = ds.Tables["TermsFromDocument"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            int termCount;
            if ((table != null) && ((termCount = table.Rows.Count) > 0))
            {
                //Populate a vector of terms
                Term[] documentTerms = new Term[termCount];
                int termId = 0;
                foreach (DataRow termRow in table.Rows)
                {
                    documentTerms[termId] = new Term(); 
                    documentTerms[termId].term = termRow.Field<string>("term");
                    documentTerms[termId].tfidf    = termRow.Field<double>("tfidf");
                    documentTerms[termId].frequency= termRow.Field<int>("frequency");
                    documentTerms[termId].corpusFrequency = termRow.Field<int>("corpusFrequency");
                    documentTerms[termId].documentFrequency = termRow.Field<int>("documentFrequency");
                    termId++;
                }
                return documentTerms;
            }
            return null;
        }

        public Term[] GetTermsFromTopicId (uint topicId, uint count)
        {
            //topicId is a zero-indexed value over the topic set.
            if (!validConnection || (topicId >= topicCount)) return null;
            if ((topics != null) && (topics[topicId] != null) && (topics[topicId].terms != null)) return topics[topicId].terms;

            DataTable table = null;
            try
            {
                //Assign values to Parameters collection for the stored procedure.
                termsFromTopicIdSQLAdapter.SelectCommand.Parameters["@topicId"].Value = topicId;
                termsFromTopicIdSQLAdapter.SelectCommand.Parameters["@count"].Value = count;
                termsFromTopicIdSQLAdapter.SelectCommand.Parameters["@mostProbable"].Value = 1;

                //Execute stored proc and fill the DataSet
                DataSet ds = new DataSet();
                termsFromTopicIdSQLAdapter.Fill(ds, "TermsFromTopic");
                table = ds.Tables["TermsFromTopic"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            int termCount;
            if ((table != null) && ((termCount = table.Rows.Count) > 0))
            {
                //Populate a vector of terms 
                Term[] topicTerms = new Term[termCount];
                int termId = 0;
                foreach (DataRow termRow in table.Rows)
                {
                    topicTerms[termId] = new Term();
                    topicTerms[termId].term = termRow.Field<string>("term");
                    topicTerms[termId].probability = termRow.Field<double>("probability");
                    topicTerms[termId].corpusFrequency = termRow.Field<int>("corpusFrequency");
                    topicTerms[termId].documentFrequency = termRow.Field<int>("documentFrequency");
                    topicTerms[termId].frequency = 0;
                    topicTerms[termId].tfidf = 0;
                    termId++;
                }
                topics[topicId].terms = topicTerms;
                return topicTerms;
            }
            return null;
        }

        public Document[] GetDocumentsFromTopicId(uint topicId, uint count)
        {
            if (!validConnection || (topicId >= topicCount)) 
                return null;
            
            if ((topics != null) && (topics[topicId].documents != null)) 
                return topics[topicId].documents;

            DataTable table = null;
            try
            {
                DataSet ds = new DataSet();
                //Assign values to Parameters collection for the stored procedure.
                documentsFromTopicIdSQLAdapter.SelectCommand.Parameters["@topicId"].Value = topicId;
                documentsFromTopicIdSQLAdapter.SelectCommand.Parameters["@count"].Value = count;

                //Fill the DataSet with the rows that are returned.
                documentsFromTopicIdSQLAdapter.Fill(ds, "DocumentsFromTopic");

                table = ds.Tables["DocumentsFromTopic"];
            }
            catch
            {
                //Problem reading results from database.
                return null;
            }

            int docCount;
            if ((table != null) && ((docCount = table.Rows.Count) > 0))
            {
                //Populate a vector of doubles with the 
                Document[] documents = new Document[docCount];
                int documentId = 0;
                foreach (DataRow termRow in table.Rows)
                {
                    documents[documentId] = new Document();
                    documents[documentId].id = termRow.Field<int>("documentId");
                    documents[documentId].uri = termRow.Field<string>("uri");
                    documents[documentId].probability = termRow.Field<double>("probability");
                    documents[documentId].title = termRow.Field<string>("title");
                    documentId++;
                }
                topics[topicId].documents = documents;
                return documents;
            }
            return null;

        }

        public Document[] GetSimilarDocumentsFromDocumentId(uint compareToDocumentId, uint count)
        {
            if (!validConnection || (compareToDocumentId >= documentCount)) return null;

            DataTable table = null;
            try
            {
                //Assign values to Parameters collection for the stored procedure.
                similarDocumentsFromDocumentIdSQLAdapter.SelectCommand.Parameters["@documentId"].Value = compareToDocumentId;
                similarDocumentsFromDocumentIdSQLAdapter.SelectCommand.Parameters["@count"].Value = count;

                //Fill the DataSet with the rows that are returned.
                DataSet ds = new DataSet();
                similarDocumentsFromDocumentIdSQLAdapter.Fill(ds, "SimilarDocuments");
                table = ds.Tables["SimilarDocuments"];
            }
            catch
            {
               //Problem reading results from database.
                return null;
            }

            int docCount;
            if ((table != null) && ((docCount = table.Rows.Count) > 0))
            {
                //Populate a vector of doubles with the 
                Document[] documents = new Document[docCount];
                int documentId = 0;
                foreach (DataRow termRow in table.Rows)
                {
                    documents[documentId] = new Document();
                    documents[documentId].id = termRow.Field<int>("similarDocumentId");
                    documents[documentId].probability = termRow.Field<double>("similarity");
                    documents[documentId].uri = termRow.Field<string>("uri");
                    documents[documentId].title = termRow.Field<string>("title");
                    documentId++;
                }
                return documents;
            }
            return null;
        }

        public Topic GetTopic(uint topicId)
        {
            if (topics != null)
                try
                {
                    return topics[topicId];
                }
                catch
                {
                    return null; //No topic with this id
                }
            return null;
        }

    }
}
