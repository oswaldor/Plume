
namespace Microsoft.Content.TopicExtraction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Content.Recommendations.Common;
    using Console = Microsoft.Content.Recommendations.Common.Console;

    /// <summary>
    /// Trains an LDA model from an input corpus, using the learning parameters passed in the config object
    /// </summary>
    public static class LDALearner
    {


        public static bool Learn(LDAConfig ldaConfig, bool copyFeaturizedDoc)
        {
            if (File.Exists(ldaConfig.Model))
            {
                StatusMessage.Write("Skipping, model already exists. " + ldaConfig.Model);
                return false;
            }

            var featurizedDocFile = ldaConfig.FeaturizedDocuments;
            if (copyFeaturizedDoc)
            {
                featurizedDocFile = string.Format(@"{0}\{1}", Path.GetDirectoryName(ldaConfig.WordTopicAllocations), Path.GetFileName(ldaConfig.FeaturizedDocuments));
                File.Copy(ldaConfig.FeaturizedDocuments, featurizedDocFile);
            }

            StatusMessage.Write("Running VW to learn LDA...");

            var command = AppDomain.CurrentDomain.BaseDirectory + "vw.exe";

            var args =
                " " + featurizedDocFile +
                " --hash strings" +
                " --lda " + ldaConfig.LDAParameters.NumTopics +
                " --lda_alpha " + ldaConfig.LDAParameters.Alpha +
                " --lda_rho " + ldaConfig.LDAParameters.Rho +
                " --lda_D " + ldaConfig.ModelStatistics.DocumentCount +
                " --minibatch " + ldaConfig.LDAParameters.Minibatch +
                " --power_t " + ldaConfig.LDAParameters.PowerT +
                " --initial_t " + ldaConfig.LDAParameters.InitialT +
                " -b " + (int)Math.Ceiling(Math.Log(ldaConfig.ModelStatistics.VocabularySize, 2.0)) + // Gets size of the hash table used to store the topic allocations for each word.
                " --passes " + ldaConfig.LDAParameters.Passes +
                " -c " +
                " --readable_model " + ldaConfig.WordTopicAllocations +
                " -p " + ldaConfig.DocumentTopicAllocations +
                " -f " + ldaConfig.Model;

            Console.RunCommand(command, args);

            if (copyFeaturizedDoc)
            {
                ConsoleColor color;
                FileManager.DeleteFile(featurizedDocFile, out color);
                FileManager.DeleteFile(featurizedDocFile + ".cache", out color);                
            }
            return true;
        }
    }
}
