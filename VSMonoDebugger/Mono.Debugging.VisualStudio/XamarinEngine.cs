using EnvDTE;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters.Binary;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace Mono.Debugging.VisualStudio
{
    [Guid(DebugEngineGuids.XamarinEngineString)]
    public class XamarinEngine : IDebugEngine2, IDebugEngineLaunch2
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected Engine _engine;

        protected SoftDebuggerSession _session;
        protected StartInfo _startInfo;

        public static Project StartupProject { set; get; }

        public XamarinEngine()
        {
            _engine = new Engine();
        }

        private string SerializeDebuggerOptions(string jsonDebugOptions)
        {
            try
            {
                NLogService.TraceEnteringMethod(Logger);
                var debugOptions = DebugOptions.DeserializeFromJson(jsonDebugOptions);

                _session = new SoftDebuggerSession();

                LogMonoDebuggerAssemblyPaths();

                if (debugOptions.UserSettings.EnableVerboseDebugLogging)
                {
                    RegisterEventHandlers();
                }

                var connectionTimeout = 30000;
                var evaluationTimeout = 30000;
                var startupProject = StartupProject;
                var softDebuggerConnectArgs = new SoftDebuggerConnectArgs(debugOptions.TargetExeFileName, debugOptions.GetHostIP(), debugOptions.GetMonoDebugPort());

                // TODO implement programm output via stream
                //softDebuggerConnectArgs.RedirectOutput = true;
                //softDebuggerConnectArgs.OutputPort = ???;
                //_session.VirtualMachine.StandardOutput ???

                softDebuggerConnectArgs.TimeBetweenConnectionAttempts = (int)debugOptions.UserSettings.TimeBetweenConnectionAttemptsInMs;
                softDebuggerConnectArgs.MaxConnectionAttempts = (int)debugOptions.UserSettings.MaxConnectionAttempts;

                _startInfo = new StartInfo(
                    softDebuggerConnectArgs,
                    new DebuggingOptions()
                    {
                        EvaluationTimeout = evaluationTimeout,
                        MemberEvaluationTimeout = evaluationTimeout,
                        ModificationTimeout = evaluationTimeout,
                        SocketTimeout = connectionTimeout
                    },
                    startupProject
                    );

                SessionMarshalling sessionMarshalling = new SessionMarshalling(_session, _startInfo);
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    ObjRef oref = RemotingServices.Marshal(sessionMarshalling);
                    bf.Serialize(ms, oref);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        private void LogMonoDebuggerAssemblyPaths()
        {
            LogAssemblyPath(typeof(Mono.Debugger.Soft.MethodMirror).Assembly);
            LogAssemblyPath(typeof(Mono.Debugging.Client.Breakpoint).Assembly);
            LogAssemblyPath(typeof(Mono.Debugging.Soft.SoftDebuggerSession).Assembly);
            LogAssemblyPath(typeof(Mono.Debugging.VisualStudio.DebuggerSession).Assembly);
            LogAssemblyPath(typeof(Mono.Debugging.VisualStudio.Engine).Assembly);
        }

        private void LogAssemblyPath(System.Reflection.Assembly assembly)
        {
            Log(nameof(LogMonoDebuggerAssemblyPaths), false, $"{assembly.FullName} loaded from {assembly.CodeBase}");
        }

        private void RegisterEventHandlers()
        {
            _session.LogWriter = (stderr, text) =>
            {
                Log(nameof(_session.LogWriter), stderr, text);
            };
            _session.OutputWriter = (stderr, text) =>
            {
                Log(nameof(_session.LogWriter), stderr, text);
            };
            _session.ExceptionHandler = (exception) =>
            {
                return Log(nameof(_session.ExceptionHandler), exception);
            };

            _session.TargetReady += (sender, eventArgs) =>
            {
                var session = sender as SoftDebuggerSession;
                if (session != null)
                {
                }
                Log(nameof(_session.TargetReady), sender, eventArgs);
            };
            _session.TargetExited += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetExited), sender, eventArgs);
            };
            _session.TargetUnhandledException += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetUnhandledException), sender, eventArgs);
            };
            _session.TargetThreadStarted += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetThreadStarted), sender, eventArgs);
            };
            _session.TargetThreadStopped += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetThreadStopped), sender, eventArgs);
            };
            _session.TargetStopped += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetStopped), sender, eventArgs);
            };
            _session.TargetStarted += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetStarted), sender, null);
            };
            _session.TargetSignaled += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetSignaled), sender, eventArgs);
            };
            _session.TargetInterrupted += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetInterrupted), sender, eventArgs);
            };
            _session.TargetExceptionThrown += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetExceptionThrown), sender, eventArgs);
            };
            _session.TargetHitBreakpoint += (sender, eventArgs) =>
            {
                Log(nameof(_session.TargetHitBreakpoint), sender, eventArgs);
            };
            //_session.TargetEvent += (sender, eventArgs) =>
            //{
            //    Log(nameof(_session.TargetEvent), sender, eventArgs);
            //};
        }

        private bool Log(string methodeName, Exception ex)
        {
            Logger.Error(ex, methodeName);
            return true;
        }

        private void Log(string methodeName, bool stderr, string text)
        {
            if (stderr)
            {
                Logger.Error(text.TrimEnd('\n', '\r'));
            }
            else
            {
                Logger.Info(text.TrimEnd('\n', '\r'));
            }
        }

        private void Log(string methodeName, object sender, TargetEventArgs x)
        {
            if (x == null)
            {
                var msg = methodeName;
                Logger.Info(msg);
            }
            else
            {
                string msg = null;
                if (x.Thread != null)
                {
                    msg = $"{methodeName}: Thread=({x.Thread.Id}, {x.Thread.Name}, {x.Thread.Location})";
                }
                else
                {
                    msg = $"{methodeName}";
                }
                //if (x.Process != null)
                //{
                //    msg += $", Process=({x.Process.Id}, {x.Process.Name}, {x.Process.Description})";
                //}
                //if (x.BreakEvent is Mono.Debugging.Client.Breakpoint)
                //{
                //    var bp = x.BreakEvent as Mono.Debugging.Client.Breakpoint;
                //    msg += $", Breakpoint=({bp.Line}, {bp.Column}, {bp.FileName}, {bp.LastTraceValue}, {bp.TraceExpression})";
                //}                
                Logger.Info(msg);                
            }
        }

        //private void _session_TargetEvent(object sender, Client.TargetEventArgs e)
        //{
        //    NLogService.TraceEnteringMethod(Logger);
        //    Debug.WriteLine("TargetEvent: " + e.Type.ToString());
        //}

        #region IDebugEngineLaunch2

        public /*override*/ int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe, string pszArgs, string pszDir, string bstrEnv, string pszOptions, enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 pCallback, out IDebugProcess2 ppProcess)
        {
            NLogService.TraceEnteringMethod(Logger);
            var base64Options = SerializeDebuggerOptions(pszOptions);
            var result = _engine.LaunchSuspended(pszServer, pPort, pszExe, pszArgs, pszDir, bstrEnv, base64Options, dwLaunchFlags, hStdInput, hStdOutput, hStdError, pCallback, out ppProcess);
            
            return result;
        }

        private void _session_TargetStarted(object sender, EventArgs e)
        {
            NLogService.TraceEnteringMethod(Logger);
        }

        public /*override*/ int ResumeProcess(IDebugProcess2 pProcess)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.ResumeProcess(pProcess);
        }

        public /*override*/ int CanTerminateProcess(IDebugProcess2 pProcess)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.CanTerminateProcess(pProcess);
        }

        public /*override*/ int TerminateProcess(IDebugProcess2 pProcess)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.TerminateProcess(pProcess);
        }

        #endregion

        #region IDebugEngine2

        public /*override*/ int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.EnumPrograms(out ppEnum);
        }

        public /*override*/ int Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
        {
            NLogService.TraceEnteringMethod(Logger);

            try
            {
                _session.Run(_startInfo, _startInfo.SessionOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + " - " + ex.StackTrace);
            }

            return _engine.Attach(rgpPrograms, rgpProgramNodes, celtPrograms, pCallback, dwReason);
        }

        public /*override*/ int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);
        }

        public /*override*/ int SetException(EXCEPTION_INFO[] pException)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.SetException(pException);
        }

        public /*override*/ int RemoveSetException(EXCEPTION_INFO[] pException)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.RemoveSetException(pException);
        }

        public /*override*/ int RemoveAllSetExceptions(ref Guid guidType)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.RemoveAllSetExceptions(ref guidType);
        }

        public /*override*/ int GetEngineId(out Guid pguidEngine)
        {
            NLogService.TraceEnteringMethod(Logger);
            var temp = _engine.GetEngineId(out pguidEngine);
            pguidEngine = new Guid(DebugEngineGuids.XamarinEngineString);
            return 0;
        }

        public /*override*/ int DestroyProgram(IDebugProgram2 pProgram)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.DestroyProgram(pProgram);
        }

        public /*override*/ int ContinueFromSynchronousEvent(IDebugEvent2 pEvent)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.ContinueFromSynchronousEvent(pEvent);
        }

        public /*override*/ int SetLocale(ushort wLangID)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.SetLocale(wLangID);
        }

        public /*override*/ int SetRegistryRoot(string pszRegistryRoot)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.SetRegistryRoot(pszRegistryRoot);
        }

        public /*override*/ int SetMetric(string pszMetric, object varValue)
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.SetMetric(pszMetric, varValue);
        }

        public /*override*/ int CauseBreak()
        {
            NLogService.TraceEnteringMethod(Logger);
            return _engine.CauseBreak();
        }

        #endregion

    }
}
