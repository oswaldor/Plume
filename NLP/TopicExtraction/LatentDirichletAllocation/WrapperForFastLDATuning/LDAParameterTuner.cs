using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Content.Recommendations.Common;
using WrapperForFastLDATuning;

namespace Microsoft.Content.TopicExtraction
{
    public class LDAParameterTuner
    {
        public string SampleName { get; set; }

        /// <summary>
        /// Config file (path) for
        /// 1). training LDA or
        /// 2). building vocabulary and featurization OR
        /// 3). computing metrics
        /// </summary>
        public string ConfigFilePath { get; set; }

        /// <summary>
        /// Secondary config file (path) for test LDA model.
        /// </summary>
        public string SecondaryConfigFilePath { get; set; }

        public bool NeedComputePerplexity { get; set; }

        /// <summary>
        /// the option for executing the command.
        /// </summary>
        public string CommandOption { get; set; }

        /// <summary>
        /// The name of the (executable) command with which to start a process.
        /// </summary>
        public static string CommandName = "LDAModelBuilder.exe";




        public LDAParameterTuner(string commandOption, string sampleName, string configFilePath, string secondaryConfigFilePath="", bool needComputePerplexity=true)
        {
            this.CommandOption = commandOption;
            this.SampleName = sampleName;
            this.ConfigFilePath = configFilePath;
            this.SecondaryConfigFilePath = secondaryConfigFilePath;
            this.NeedComputePerplexity = needComputePerplexity;
        }

        public int Run()
        {
            StatusMessage.Write(string.Format("Starting '{0}' for config file\r\n\t{1} ...", CommandOption, ConfigFilePath), ConsoleColor.Green);

            string result = ExecuteCommandSync();

            if (result != null)
            {
                StatusMessage.Write(result);                
                return int.Parse(result.Split('\t')[0]);
            }
            else
            {
                StatusMessage.Write(string.Format("The {0} process has failed", CommandOption), ConsoleColor.Red);
                return 1;
            }
        }

        private string ExecuteCommandSync()
        {
            try
            {
                string arguments;
                if (CommandOption.ToLowerInvariant() == "getmetrics")
                {
                    if (!NeedComputePerplexity)
                    {
                        arguments = string.Format("{0} config={1}", CommandOption, ConfigFilePath);
                    }
                    else
                    {
                        // L1-norm for doc vector by default.
                        arguments = string.Format("{0} samplename={1} config={2} norm=l1",
                                                  CommandOption, SampleName, ConfigFilePath);
                    }

                }
                else if (CommandOption.ToLowerInvariant() == "generatedocvectors")
                {
                    // L1-norm and base64 encoding for doc vector by default.
                    arguments = string.Format("{0} config={1} inputfile={2} norm=l1 encoding=base64",
                                              CommandOption, ConfigFilePath, SampleName);
                }
                else
                {
                    arguments = string.Format("{0} samplename={1} config={2} copyfd",
                                               CommandOption, SampleName, ConfigFilePath);
                }

                return CrossComponentExecutor.StartProcess(CommandName, arguments);
            }
            catch (Exception e)
            {
                StatusMessage.Write(string.Format("Exception occurred while executing command for {0}. Details:\r\n{1}", CommandOption, e), ConsoleColor.Red);
                return null;
            }
        }

    }
}
