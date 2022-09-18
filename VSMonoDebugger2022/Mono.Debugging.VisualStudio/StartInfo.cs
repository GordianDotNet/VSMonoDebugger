using EnvDTE;
using Mono.Cecil.Cil;
using Mono.Cecil;
using Mono.CompilerServices.SymbolWriter;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mono.Debugging.VisualStudio
{
    public class SessionMarshalling : MarshalByRefObject
    {
        public SoftDebuggerSession Session { get; private set; }

        public StartInfo StartInfo { get; private set; }

        public SessionMarshalling(SoftDebuggerSession session, StartInfo startInfo)
        {
            Session = session;
            StartInfo = startInfo;
        }
    }

    public class DebuggingOptions
    {
        public int? EvaluationTimeout { get; set; }

        public int? MemberEvaluationTimeout { get; set; }

        public int? ModificationTimeout { get; set; }

        public int SocketTimeout { get; set; }
    }

    public interface IStartInfo
    {
        DebuggingOptions Options { get; }

        DebuggerSessionOptions SessionOptions { get; }

        Project StartupProject { get; }
    }

    public class StartInfo : SoftDebuggerStartInfo, IStartInfo
    {
        private const uint ppdb_signature = 1112167234u;

        public DebuggingOptions Options { get; protected set; }

        public DebuggerSessionOptions SessionOptions { get; protected set; }

        public Project StartupProject { get; private set; }

        public StartInfo(SoftDebuggerStartArgs start_args, DebuggingOptions options, Project startupProject)
            : base(start_args)
        {
            StartupProject = startupProject;
            Options = options;
            SessionOptions = CreateDebuggerSessionOptions();
            //GetUserAssemblyNamesAndMaps();
        }

        public static bool IsPortablePdb(string filename)
        {
            try
            {
                using (FileStream input = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (BinaryReader binaryReader = new BinaryReader(input))
                    return binaryReader.ReadUInt32() == 1112167234;
            }
            catch
            {
                return false;
            }
        }

        protected DebuggerSessionOptions CreateDebuggerSessionOptions()
        {
            EvaluationOptions defaultOptions = EvaluationOptions.DefaultOptions;
            defaultOptions.GroupPrivateMembers = true;
            defaultOptions.GroupStaticMembers = true;
            defaultOptions.FlattenHierarchy = false;
            defaultOptions.AllowToStringCalls = false;
            DebuggerSessionOptions debuggerSessionOptions = new DebuggerSessionOptions();
            debuggerSessionOptions.EvaluationOptions = defaultOptions;
            debuggerSessionOptions.ProjectAssembliesOnly = true;
            return debuggerSessionOptions;
        }
    }
}
