namespace WrapperForFastLDATuning
{
    using System;
    using Microsoft.Content.Recommendations.Common;

    public static class CrossComponentExecutor
    {
        static CrossComponentExecutor()
        {
            SetDefaultExecutors();
        }

        public static Func<string, string, string> StartProcess { get; set; }

        private static void SetDefaultExecutors()
        {
            StartProcess = AppProcessInitiator.StartProcess;
        }
    }
}
