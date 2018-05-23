using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Content.Recommendations.Common
{
    public class Commands
    {
        public const string Load = "load";

        public const string LoadAll = "loadall";

        public const string Read = "read";

    }

    public class Options
    {
        public const string Config = "config";

        public const string SQLServer = "sqlserver";
        
        public const string DatabaseName = "dbname";

        public const string ModelsDbName = "modelsdb";

        public const string ModelRepositoryPath = "modelrepository";

        public const string ModelMetricsDestination = "dest";

        public const string ModelLocale = "locale";

        public const string MetricsDestFolder = "metricsDestFolder";

    }
}
