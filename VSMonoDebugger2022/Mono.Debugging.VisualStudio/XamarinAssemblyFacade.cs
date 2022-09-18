using EnvDTE;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters.Binary;

namespace Mono.Debugging.VisualStudio
{
    public static class XamarinAssemblyFacade
    {
        private static System.Reflection.Assembly _visualStudioXamarinAssembly = null;
        private static System.Reflection.Assembly _visualStudioVsixXamarinAssembly = null;

        public static string GetExtensionsXamarinMonoDebuggingPath()
        {
            // TODO Get this path at runtime
            return @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\Xamarin\Mono.Debugging\";
        }

        public static System.Reflection.Assembly LoadVisualStudioAssembly()
        {
            var fullAssemblyFilePath = Path.Combine(GetExtensionsXamarinMonoDebuggingPath(), "Mono.Debugging.VisualStudio.dll");
            _visualStudioXamarinAssembly = _visualStudioXamarinAssembly ?? System.Reflection.Assembly.LoadFrom(fullAssemblyFilePath);
            return _visualStudioXamarinAssembly;
        }

        public static System.Reflection.Assembly LoadVisualStudioVsixAssembly()
        {
            var fullAssemblyFilePath = Path.Combine(GetExtensionsXamarinMonoDebuggingPath(), "Mono.Debugging.VisualStudio.Vsix.dll");
            _visualStudioVsixXamarinAssembly = _visualStudioVsixXamarinAssembly ?? System.Reflection.Assembly.LoadFrom(fullAssemblyFilePath);
            return _visualStudioVsixXamarinAssembly;
        }

        public static IDebugPortSupplier2 CreatePortSupplier()
        {
            var portSupplierClassName = "Mono.Debugging.VisualStudio.PortSupplier";
            var portSupplier = LoadVisualStudioVsixAssembly().CreateInstance(portSupplierClassName);

            return portSupplier as IDebugPortSupplier2 ?? throw new InvalidCastException($"Couldn't cast {portSupplierClassName} to {nameof(IDebugPortSupplier2)}!");
        }

        public static IDebugEngine3 CreateEngine(out IDebugEngineLaunch2 engineLaunch, out IDebugBreakpointFileUpdateNotification110 engineBreakpointFileUpdateNotification)
        {
            var engineClassName = "Mono.Debugging.VisualStudio.Engine";
            var engine = LoadVisualStudioVsixAssembly().CreateInstance(engineClassName);

            engineLaunch = engine as IDebugEngineLaunch2 ?? throw new InvalidCastException($"Couldn't cast {engineClassName} to {nameof(IDebugEngineLaunch2)}!");

            engineBreakpointFileUpdateNotification = engine as IDebugBreakpointFileUpdateNotification110 ?? throw new InvalidCastException($"Couldn't cast {engineClassName} to {nameof(IDebugBreakpointFileUpdateNotification110)}!");

            return engine as IDebugEngine3 ?? throw new InvalidCastException($"Couldn't cast {engineClassName} to {nameof(IDebugEngine3)}!");
        }

        public static string CreateSessionMarshalling(Project startupProject, SoftDebuggerConnectArgs softDebuggerConnectArgs, SoftDebuggerSession session, out DebuggerStartInfo debuggerStartInfo, out DebuggerSessionOptions debuggerSessionOptions)
        {
            // We have to call this code without references assembly

            //var startInfo = new StartInfo(
            //        softDebuggerConnectArgs,
            //        new DebuggingOptions()
            //        {
            //            EvaluationTimeout = evaluationTimeout,
            //            MemberEvaluationTimeout = evaluationTimeout,
            //            ModificationTimeout = evaluationTimeout,
            //            SocketTimeout = connectionTimeout
            //        },
            //        startupProject
            //        );

            //SessionMarshalling sessionMarshalling = new SessionMarshalling(session, startInfo);

            var outputPath = ""; // In StartInfo: ProjectPath + outputPath + ProjectFileName

            var debuggingOptionsClassName = "Mono.Debugging.VisualStudio.DebuggingOptions";
            dynamic debuggingOptions = LoadVisualStudioAssembly().CreateInstance(debuggingOptionsClassName);

            var connectionTimeout = 30000;
            var evaluationTimeout = 30000;
            debuggingOptions.EvaluationTimeout = evaluationTimeout;
            debuggingOptions.MemberEvaluationTimeout = evaluationTimeout;
            debuggingOptions.ModificationTimeout = evaluationTimeout;
            debuggingOptions.SocketTimeout = connectionTimeout;

            var startInfoArgs = new object[]
            {
                softDebuggerConnectArgs,
                debuggingOptions,
                startupProject,
                outputPath
            };

            var startInfoClassName = "Mono.Debugging.VisualStudio.StartInfo";
            dynamic startInfo = LoadVisualStudioAssembly().CreateInstance(startInfoClassName, ignoreCase: false, BindingFlags.Instance | BindingFlags.Public, null, startInfoArgs, null, null);

            var sessionMarshallingArgs = new object[]
            {
                session,
                startInfo
            };

            var sessionMarshallingClassName = "Mono.Debugging.VisualStudio.SessionMarshalling";
            var sessionMarshalling = LoadVisualStudioAssembly().CreateInstance(sessionMarshallingClassName, ignoreCase: false, BindingFlags.Instance | BindingFlags.Public, null, sessionMarshallingArgs, null, null) as MarshalByRefObject;

            debuggerStartInfo = startInfo as DebuggerStartInfo ?? throw new InvalidCastException($"Couldn't cast Mono.Debugging.VisualStudio.StartInfo to {nameof(DebuggerStartInfo)}!");
            debuggerSessionOptions = startInfo.SessionOptions as DebuggerSessionOptions ?? throw new InvalidCastException($"Couldn't cast Mono.Debugging.VisualStudio.StartInfo->SessionOptions to {nameof(DebuggerSessionOptions)}!");

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                ObjRef oref = RemotingServices.Marshal(sessionMarshalling);
                bf.Serialize(ms, oref);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}
