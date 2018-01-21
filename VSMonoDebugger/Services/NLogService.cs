using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VSMonoDebugger.Services
{
    public static class NLogService
    {
        public static string LoggerPath { get; private set; }

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static void TraceEnteringMethod([CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            logger.Trace($"Entering: {callerMember} - {Path.GetFileName(callerFilePath)}({callerLineNumber})");

            //MethodBase mth = new StackTrace().GetFrame(1).GetMethod();
            //if (mth.ReflectedType != null)
            //{
            //    string className = mth.ReflectedType.Name;
            //    logger.Trace(className + " (entering) :  " + callerMember);
            //}
        }

        public static void Setup(string logFilename)
        {
            var basePath = new FileInfo(typeof(NLogService).Assembly.Location).Directory.FullName;

            var logPath = Path.Combine(basePath, "Log");

            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            LoggerPath = Path.Combine(logPath, logFilename);

            var config = new LoggingConfiguration();
            var target = new NLog.Targets.DebuggerTarget();
            config.AddTarget("file", target);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, target));

            var fileTarget = new FileTarget { FileName = LoggerPath };
            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, fileTarget));
            var console = new ColoredConsoleTarget();
            config.AddTarget("file", console);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, console));

            LogManager.Configuration = config;

            logger = LogManager.GetCurrentClassLogger();
        }
    }
}
