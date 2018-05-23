
namespace Microsoft.Content.TopicExtraction
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.Content.Recommendations.Common;
    using Microsoft.Content.Recommendations.LinearAlgebra;
    using Microsoft.Content.TextProcessing;
    // Comment out next line, and uncomment the following one if you want to compiling with the .Net version of VW
    using Microsoft.Research.MachineLearning;    
    // using VW;
    
    /// <summary>
    /// 
    /// </summary>
    public class LDA: IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly int numTopics;

        /// <summary>
        /// 
        /// </summary>
        private bool[] badTopics;

        /// <summary>
        /// 
        /// </summary>
        private readonly VectorFactory topicAllocationsFactory;
        
        /// <summary>
        /// 
        /// </summary>

        private string corpusVocabularyFileName;
        private CorpusVocabulary corpusVocabulary = null;
        
        /// <summary>
        /// 
        /// </summary>
        private readonly IntPtr vwLDAModel; 
        //private readonly VowpalWabbit vwLDAModel = null;
        private bool disposed = false;

        /// <summary>
        /// 
        /// </summary>
        private readonly DocumentVocabularyFactory dvFactory;

        public VectorType RecommendedCompressionType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LDA"/> class.
        /// </summary>
        /// <param name="numTopic"></param>
        /// <param name="modelFileName"></param>
        /// <param name="corpusVocabularyFileName"></param>
        /// <param name="language"></param>
        private LDA(int numTopic, string modelFileName, string corpusVocabularyFileName, int[] badTopicIds, Language language)
        {
            this.numTopics = numTopic;
            this.dvFactory = DocumentVocabularyFactory.NewInstance(language);
            this.topicAllocationsFactory = VectorFactory.NewInstance(VectorType.DenseVector, numTopic);
            this.corpusVocabularyFileName = corpusVocabularyFileName;

            StatusMessage.Write(string.Format("LDA: Initializing Vowpal Wabbit Interface with model file {0}", modelFileName));
            this.vwLDAModel = VowpalWabbitInterface.Initialize(string.Format("-i {0} -t --quiet", modelFileName));
            //this.vwLDAModel = new VowpalWabbit(string.Format(CultureInfo.InvariantCulture, "-i {0} -t --quiet", modelFileName));

            this.badTopics = new bool[this.numTopics];
            Array.Clear(this.badTopics, 0, this.numTopics);

            this.RecommendedCompressionType = VectorType.DenseVector; // Default
            int badTopicCount;
            if ((badTopicIds != null) && ((badTopicCount = badTopicIds.Length) > 0))
            {
                foreach (var topicId in badTopicIds)
                {
                    if (topicId < this.numTopics)
                    {
                        this.badTopics[topicId] = true;
                    }
                }

                var sizeOfSparseVector = VectorBase.SizeOfSparseVectors(numTopic, badTopicCount);
                var sizeOfDenseVector = VectorBase.BytesPerDimension * numTopic;

                this.RecommendedCompressionType = (sizeOfSparseVector < sizeOfDenseVector) ? VectorType.SparseVector : VectorType.DenseVector;
            }
        }

        /// <summary>
        /// Gets a new instance.
        /// </summary>
        /// <param name="numTopic"></param>
        /// <param name="modelFileName"></param>
        /// <param name="corpusVocabularyFileName"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public static LDA NewInstance(int numTopic, string modelFileName, string corpusVocabularyFileName, int[] badTopicIds, Language language)
        {
            return new LDA(numTopic, modelFileName, corpusVocabularyFileName, badTopicIds, language);
        }

        
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // Take us off the Finalization queue to prevent finalization code from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if(!this.disposed)
            {
                // Un comment if compiling with .Net version of VW
                //if (this.vwLDAModel != null)
                //{
                //    this.vwLDAModel.Dispose();
                //}
            }
            disposed = true;         
        }

        // Finalization code.
        // This destructor will run only if the Dispose method  
        // does not get called.
        ~LDA()      
        {
            Dispose(false);
        }

        /// <summary>
        /// Generates the topic vector for the text in the document.
        /// </summary>
        /// <param name="document">The text.</param>
        /// <returns>The LDA topic vector.</returns>
        public VectorBase GetTopicAllocations(string document)
        {
            if (!this.EnsureCorpusVocabulary())
            {
                return null;
            }

            return this.GetTopicAllocations(document, false);
        }

        public VectorBase GetTopicAllocations(DocumentVocabulary document)
        {
            if (!this.EnsureCorpusVocabulary())
            {
                return null;
            }

            return this.GetTopicAllocations(this.corpusVocabulary.SerializeDocumentForVW(document), true);
        }

        /// <summary>
        /// Same as GetTopicVector(), but allows a featurized version of the document to be passed in.
        /// This saves time if the same document set is used multiple times while testing models.
        /// </summary>
        /// <param name="document">The text or a featurized version of the text.</param>
        /// <param name="isFeaturized">Indicates whether the text contains the original document or a featurized version of it</param>
        /// <returns>The LDA topic vector.</returns>
        public VectorBase GetTopicAllocations(string document, bool isFeaturized)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("LDA Object has already been disposed.");
            }

            var featurizedDocument = isFeaturized ? document : this.corpusVocabulary.SerializeDocumentForVW(this.dvFactory.Get(document));

            // return null to indicate can't generate topic vector for this document
            // passing empty featurized content would result unexpected vector
            if (featurizedDocument.IndexOf(' ') == -1)
            {
                return null;
            }

            if (this.vwLDAModel == null)
            {
                throw new Exception ("LDA Model was not properly loaded.");
            }

            var example = VowpalWabbitInterface.ReadExample(this.vwLDAModel, featurizedDocument);
            VowpalWabbitInterface.Learn(this.vwLDAModel, example);

            /* VowpalWabbitTopicPrediction topicPrediction = this.vwLDAModel.Predict<VowpalWabbitTopicPrediction>(featurizedDocument);
            if (topicPrediction == null)
            {
                throw new Exception("LDA Model did not return a proper Topic Prediction vector.");
            }
            
            var topics = topicPrediction.Values;
            if (topics.Length != this.numTopics)
            {
                throw new Exception("LDA Model has an unexpected number of topics.");
            }*/


            var topicAllocations = this.topicAllocationsFactory.Zero();
            for (var i = 0; i < this.numTopics; i++)
            {
                if (!this.badTopics[i])
                {
                    topicAllocations[i] = VowpalWabbitInterface.GetTopicPrediction(example, (IntPtr)i);
                    // topicAllocations[i] = topics[i];
                }
            }

            VowpalWabbitInterface.FinishExample(this.vwLDAModel, example);

            return topicAllocations;
        }

        private bool EnsureCorpusVocabulary()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("LDA Object has already been disposed.");
            }

           
            if (this.corpusVocabulary == null)
            {
                if (File.Exists(this.corpusVocabularyFileName))
                {
                    StatusMessage.Write("LDA.GetTopicAllocations: Loading Corpus Vocabulary");
                    this.corpusVocabulary = CorpusVocabulary.NewInstance(File.ReadLines(this.corpusVocabularyFileName));
                    return true;
                }
                else
                {
                    StatusMessage.Write(string.Format("LDA.GetTopicAllocations: Error. Cannot find Corpus Vocabulary {0}", this.corpusVocabularyFileName));
                }

                return false;
            }

            return true;
        }
    }
}
