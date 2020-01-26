using System;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SshFileSync;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Debuggers
{
    public abstract class SSHDebuggerBase : IDebugger
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SshDeltaCopy.Options _sshOptions;

        public SSHDebuggerBase(SshDeltaCopy.Options options)
        {
            _sshOptions = options;
        }

        public Task<bool> DeployRunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod(Logger);
            writeOutput("Start DeployRunAndDebug over SSH ...");
            return StartDebuggerAsync(_sshOptions, debugOptions, true, true, writeOutput, redirectOutputOption);
        }

        public Task<bool> DeployAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod(Logger);
            writeOutput("Start Deploy over SSH ...");
            return StartDebuggerAsync(_sshOptions, debugOptions, true, false, writeOutput, redirectOutputOption);
        }

        public Task<bool> RunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod(Logger);
            writeOutput("Start RunAndDebug over SSH ...");
            return StartDebuggerAsync(_sshOptions, debugOptions, false, true, writeOutput, redirectOutputOption);
        }

        private Task<bool> StartDebuggerAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, bool deploy, bool debug, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption)
        {
            NLogService.TraceEnteringMethod(Logger);

            return Task.Run<bool>(async () =>
            {
                var errorHelpText = new StringBuilder();
                Action<string> writeLineOutput = s => writeOutput(s + Environment.NewLine).Wait();

                try
                {
                    errorHelpText.AppendLine($"SSH Login: {options.Username}@{options.Host}:{options.Port} Directory: {options.DestinationDirectory}");

                    using (SshDeltaCopy sshDeltaCopy = new SshDeltaCopy(options))
                    {
                        sshDeltaCopy.LogOutput = writeLineOutput;

                        if (deploy)
                        {
                            Logger.Info($"StartDebuggerAsync - deploy");

                            errorHelpText.AppendLine($"SSH: Start deployment from '{options.SourceDirectory}' to '{options.DestinationDirectory}'.");
                            sshDeltaCopy.DeployDirectory(options.SourceDirectory, options.DestinationDirectory);
                            errorHelpText.AppendLine($"SSH Deployment was successful.");
                            // We are creating mdb files on local machine with pdb2mdb
                            //var createMdbCommand = sshDeltaCopy.RunSSHCommand($@"find . -regex '.*\(exe\|dll\)' -exec {debugOptions.UserSettings.SSHPdb2mdbCommand} {{}} \;", false);
                            //msgOutput(createMdbCommand.Result);
                        }

                        if (debug)
                        {
                            Logger.Info($"StartDebuggerAsync - debug");

                            await DebugAsync(debugOptions, writeOutput, redirectOutputOption, errorHelpText, writeLineOutput, sshDeltaCopy);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var additionalErrorMessage = $"SSHDebugger: {ex.Message}\n\nExecuted steps:\n{errorHelpText.ToString()}";
                    await writeOutput(additionalErrorMessage);
                    throw new Exception(additionalErrorMessage, ex);
                }

                return true;
            });
        }

        protected  abstract Task DebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption, StringBuilder errorHelpText, Action<string> writeLineOutput, SshDeltaCopy sshDeltaCopy);
    }
}
