using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace Microsoft.Content.Recommendations.Common
{
    using Microsoft.Content.TopicExtraction;

    public class ModelsDb
    {
        private const string connectionStringTemplate = "server={0};database={1};Trusted_Connection=yes";
        private SqlConnection modelsDbConnection = null;

        public string serverName = null;
        private string databaseName = null;
        private Dictionary<string, double> metrics;

        private SqlDataAdapter modelStatistics;
        public bool validConnection = false;
        
        //Constructor
        public ModelsDb(string sqlServerName, string sqlDatabaseName, Dictionary<string, double> metrics)
        {
            this.serverName = sqlServerName;
            this.databaseName = sqlDatabaseName;
            this.metrics = metrics;
        }

        public bool Open()
        {
            return Open(serverName, databaseName, metrics);
        }

        private bool Open(string sqlServer, string modelsDbName, Dictionary<string, double> metrics)
        {
            if (!string.IsNullOrEmpty(sqlServer) && !string.IsNullOrEmpty(modelsDbName))
            {
                string connectionString = string.Format(connectionStringTemplate, sqlServer, modelsDbName);
                try
                {
                    this.modelsDbConnection = new SqlConnection(connectionString);
                    this.modelStatistics = new SqlDataAdapter("spAddModel", modelsDbConnection);
                    this.modelStatistics.SelectCommand.CommandType = CommandType.StoredProcedure;


                    var parameters = modelStatistics.SelectCommand.Parameters;
                    // Corpus attributes
                    parameters.Add(new SqlParameter("@culture", SqlDbType.NVarChar, 10));
                    parameters.Add(new SqlParameter("@corpus", SqlDbType.NVarChar));
                    parameters.Add(new SqlParameter("@sample", SqlDbType.NVarChar));
                    parameters.Add(new SqlParameter("@documentCount", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@wordCount", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@minWordDocumentFrequency", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@maxRelativeWordDocumentFrequency", SqlDbType.Float));

                    // Model sttributes
                    parameters.Add(new SqlParameter("@topicCount", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@alpha", SqlDbType.Float));
                    parameters.Add(new SqlParameter("@rho", SqlDbType.Float));
                    parameters.Add(new SqlParameter("@minibatch", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@passes", SqlDbType.Int));
                    parameters.Add(new SqlParameter("@initialT", SqlDbType.Float));
                    parameters.Add(new SqlParameter("@powerT", SqlDbType.Float));
                    parameters.Add(new SqlParameter("@modelName", SqlDbType.NVarChar));
                    parameters.Add(new SqlParameter("@metricsFilePath", SqlDbType.NVarChar));

                    /* Metrics */

                    foreach (var metric in metrics)
                    {
                        if (metric.Key == "goodtopics")
                        {
                            parameters.Add(new SqlParameter("@goodTopicCount", SqlDbType.Int));
                        }
                        else
                        {
                            parameters.Add(new SqlParameter("@" + metric.Key, SqlDbType.Float));
                        }


                    }

                    //Return values: modelId and databaseName
                    var returnParameter = new SqlParameter("@dbName", SqlDbType.NVarChar, 250);
                    returnParameter.Direction = ParameterDirection.Output;
                    parameters.Add(returnParameter);

                    returnParameter = new SqlParameter("@modelId", SqlDbType.Int);
                    returnParameter.Direction = ParameterDirection.Output;
                    parameters.Add(returnParameter);

                    validConnection = true;
                }
                catch
                {
                    validConnection = false;    //failure connecting to db or creating SQL adapters for stored procs.
                }
            }
            return validConnection;
        }

        ~ModelsDb()
        {
            // Dispose of the DataAdapters.
            this.modelStatistics.Dispose();

            this.modelsDbConnection.Close(); // Close the connection.
            this.modelsDbConnection.Dispose();
        }

        public void AddModel(LDAConfig ldaConfig, Dictionary<string, double> metrics,  out string modelDbName, out int modelId)
        {

            var parameters = this.modelStatistics.SelectCommand.Parameters;
            
            // Corpus attributes
            parameters["@culture"].Value = "en-us";
            parameters["@corpus"].Value = ldaConfig.Corpus;
            parameters["@sample"].Value = ldaConfig.SampleName;
            parameters["@documentCount"].Value = ldaConfig.ModelStatistics.DocumentCount;
            parameters["@wordCount"].Value = ldaConfig.ModelStatistics.VocabularySize;
            parameters["@minWordDocumentFrequency"].Value = ldaConfig.FeaturizationParameters.MinWordDocumentFrequency;
            parameters["@maxRelativeWordDocumentFrequency"].Value = ldaConfig.FeaturizationParameters.MaxRalativeWordDocumentFrequency;
            
            // Model sttributes
            parameters["@topicCount"].Value = ldaConfig.LDAParameters.NumTopics;
            parameters["@alpha"].Value = ldaConfig.LDAParameters.Alpha;
            parameters["@rho"].Value = ldaConfig.LDAParameters.Rho;
            parameters["@minibatch"].Value = ldaConfig.LDAParameters.Minibatch;
            parameters["@passes"].Value = ldaConfig.LDAParameters.Passes;
            parameters["@initialT"].Value = ldaConfig.LDAParameters.InitialT;
            parameters["@powerT"].Value = ldaConfig.LDAParameters.PowerT;

            // Some meta data
            parameters["@modelName"].Value = ldaConfig.modelName;
            parameters["@metricsFilePath"].Value = ldaConfig.ExtrinsicMetricsProcessed;

            // Metrics

            foreach (var metric in metrics)
            {
                if (metric.Key == "goodtopics")
                {
                    parameters["@goodTopicCount"].Value = (int)metric.Value;
                }
                else
                {
                    parameters["@" + metric.Key].Value = metric.Value;
                }
            }

            // Excecute
            modelStatistics.SelectCommand.Connection.Open();
            this.modelStatistics.SelectCommand.ExecuteReader();
            modelStatistics.SelectCommand.Connection.Close();

            modelDbName = parameters["@dbName"].Value.ToString();
            modelId = int.Parse(parameters["@modelId"].Value.ToString());
        }
    }
}
