
namespace TopicBrowserApp.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Mvc;
    using TopicModelDbLib;
    using System.Configuration;

    public class HomeController : Controller
    {
        private static Dictionary<String, ModelDatabase> modelDatabases = null;

        public HomeController ()
        {
            //read list of Topic Model dbs from web.config.
            //For each, create a modelDb object and add it to a dictionary to allow users to switch between them
            if (modelDatabases == null)
            {
                modelDatabases = new Dictionary<String, ModelDatabase>();
                string modelDatabaseList = ConfigurationManager.AppSettings["modelDatabases"];
                String[] modelDatabasesArray = modelDatabaseList.Split(';');
                foreach (string modelDatabaseName in modelDatabasesArray)
                {
                    //A modelDatabaseName is a pair of strings made up of the SQL server name and the db name, separated by '/'
                    int modelNameSeparatorPosition = modelDatabaseName.IndexOf('=');
                    int nameSeparatorPosition = modelDatabaseName.IndexOf('/');
                    //Make sure the name is valida before creating an entry int the dictionary
                    if ((modelNameSeparatorPosition > 0) && (nameSeparatorPosition > modelNameSeparatorPosition) && (modelDatabaseName.Length > nameSeparatorPosition+2))
                    {
                        string modelName = modelDatabaseName.Substring(0, modelNameSeparatorPosition);
                        string sqlServerName = modelDatabaseName.Substring(modelNameSeparatorPosition+1, (nameSeparatorPosition-modelNameSeparatorPosition-1));
                        string databaseName = modelDatabaseName.Substring(nameSeparatorPosition+1);
                        //Create a ModelDatabase object and add it to dictionary, but delay opening the connection tot eh db until neeeded.
                        modelDatabases.Add(modelName, new ModelDatabase(modelName, sqlServerName, databaseName, false));
                    }
                }
            }
        }

        public ActionResult DocumentView(string modelName, uint id)
        {
            ModelDatabase modelDb;
            if (modelName == null)
                modelDb = modelDatabases.Values.First<ModelDatabase>();
            else
                modelDb = modelDatabases[modelName];
            
            ViewBag.currentModelName = modelName;
            return RenderDocumentList(modelDb, id);
        }

        private ActionResult RenderDocumentList(ModelDatabase modeldb, uint id)
        {
            if (modeldb == null) return null;

            if (modeldb.validConnection == false)
                modeldb.Open();

            if (modeldb.validConnection)
            {
                TopicModelDbLib.Document[] documents = modeldb.GetDocumentsFromTopicId(id, 1000);
                if (documents != null) 
                    return View(documents);
            }
            return null;
        }

        public class TermsList
        {
            public uint topicId;
            public Term[] listOfTerms;
            public string jsonVersion;
        }
        
        public ActionResult Terms(string modelName, uint id)
        {
            ModelDatabase modelDb;
            if (modelName == null)
                modelDb = modelDatabases.Values.First<ModelDatabase>();
            else
                modelDb = modelDatabases[modelName];
            return RenderTermsList(modelDb, id);
        }

        //ToDo: Better ncapsulation.  Generation of JSON needs to move into the document class...after Demo1 ;)
        private ActionResult RenderTermsList(ModelDatabase modeldb, uint id)
        {
            if (modeldb == null) return null;

            if (modeldb.validConnection == false)
                modeldb.Open();

            if (modeldb.validConnection)
            {
                TermsList terms = new TermsList();
                terms.topicId = id;
                terms.listOfTerms = modeldb.GetTermsFromTopicId(id, 100);
                if (terms.listOfTerms != null)
                {
                    int currentSize = 81;
                    double lastProbability = 1;
                    String json = "var topicCloudInfo = [[";

                     
                    foreach (Term t in terms.listOfTerms)
                    {
                        json = json + "{\"text\":\"" + t.term + "\",\"size\":" + Convert.ToString(currentSize = ModelDatabase.sizeScale(t.probability, lastProbability, currentSize)) + "},";
                        lastProbability = t.probability;
                    }
                    terms.jsonVersion = json.Substring(0, json.Length-1) + "]];";
                    return View(terms);
                }
            }
            return null;
        }
                
        public ActionResult Document(string modelName, uint id)
        {
            ModelDatabase modelDb;
            if (modelName == null)
                modelDb = modelDatabases.Values.First<ModelDatabase>();
            else
                modelDb = modelDatabases[modelName];
            return RenderDocument(modelDb, id);
        }

        private ActionResult RenderDocument(ModelDatabase modeldb, uint id)
        {
            if (modeldb == null) return null;

            if (modeldb.validConnection == false)
                modeldb.Open();

            if (modeldb.validConnection)
            {
                Document document = modeldb.GetDocument(id, 0); //0=Sort document terms by TF/DF.  ToDo: need ennumarated type
                document.relatedDocuments = modeldb.GetSimilarDocumentsFromDocumentId(id, 1000);

                if (document.topicProbabilities != null)
                {
                    String topicsTermsJSON = "";
                    String topicsJSON = "";

                    uint topicId = 0;
                    foreach (double topicProbability in document.topicProbabilities)
                    {
                        if (topicProbability > 0.03)
                        {
                            Topic topic = modeldb.GetTopic(topicId);
                            if ((topic != null) && (topic.terms != null))
                            {
                                topicsTermsJSON = topicsTermsJSON + "," + topic.termsJSON;
                                topicsJSON = topicsJSON + "," + Convert.ToString(topicProbability);
                            }
                            else
                            {
                                topicsTermsJSON = topicsTermsJSON + ",[]";
                                topicsJSON = topicsJSON + ",0.01";
                            }
                        }
                        else
                        {
                            topicsTermsJSON = topicsTermsJSON + ",[]";
                            topicsJSON = topicsJSON + ",0.01";
                        }
                        topicId++;
                    }
                    //Remove final comma and close out the array
                    document.termsJSON = "var topicCloudInfo = [" + topicsTermsJSON.Substring(1) + "];"; //Remove leading comma
                    document.topicProbabilitiesJSON = "var data = [" + topicsJSON.Substring(1) + "];";
                }
                return View(document);
            }
            return null;
        }
 
        public ActionResult Index(string modelName)
        {
            ModelDatabase modelDb ;
            if (modelName == null)
                modelDb = modelDatabases.Values.First<ModelDatabase>();
            else
                modelDb = modelDatabases[modelName];

            ViewBag.currentModelName = modelName;
            ViewBag.modelNames = modelDatabases.Keys;
            return RenderTopics(modelDb);
        }
        public ActionResult RenderTopics(ModelDatabase modelDb)
        {
            if (modelDb == null) return null;

            if (modelDb.validConnection == false)
                modelDb.Open();

            if (modelDb.validConnection)
            {
                Dictionary<uint, Topic> topics = modelDb.GetTopics();
                if (topics != null)
                    return View(modelDb);
            }
            return null;
        }

        public ActionResult About()
        {
            ViewBag.Message = "Topic Browser.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Topic Browser.";

            return View();
        }
    }
}