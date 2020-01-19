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

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void TraceEnteringMethod(Logger logger, [CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            (logger ?? Logger).Trace($"Entering: {callerMember} - {Path.GetFileName(callerFilePath)}({callerLineNumber})");
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

            // TODO add better logger options
            var fileTarget = new FileTarget { FileName = LoggerPath };
            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, fileTarget));
            var console = new ColoredConsoleTarget();
            config.AddTarget("file", console);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, console));

            LogManager.Configuration = config;
        }
    }
}
