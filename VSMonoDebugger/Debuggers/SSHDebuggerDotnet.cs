using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SshFileSync;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Debuggers
{
    public class SSHDebuggerDotnet : SSHDebuggerBase
    {
        public SSHDebuggerDotnet(SshDeltaCopy.Options options) : base(options)
        { }

        protected override Task DebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption, StringBuilder errorHelpText, Action<string> writeLineOutput, SshDeltaCopy sshDeltaCopy)
        {
            // will be handled in MonoVisualStudioExtension
            return Task.CompletedTask;
        }
    }
}
