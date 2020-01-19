using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SshFileSync;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.SSH
{
    public interface IDebugger
    {
        Task<bool> DeployRunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);

        Task<bool> DeployAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);

        Task<bool> RunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);
    }

    public class SSHDebugger : IDebugger
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SshDeltaCopy.Options _sshOptions;

        public SSHDebugger(SshDeltaCopy.Options options)
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

                            errorHelpText.AppendLine($"SSH: Stop previous mono processes.");

                            var killCommandText = debugOptions.PreDebugScript;
                            var killCommand = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            writeLineOutput(killCommand.Result);

                            Logger.Info($"Run PreDebugScript: {killCommandText}");

                            if (killCommand.ExitStatus != 0 || !string.IsNullOrWhiteSpace(killCommand.Error))
                            {
                                var error = $"SSH script error in PreDebugScript:\n{killCommand.CommandText}\n{killCommand.Error}";
                                //errorHelpText.AppendLine(error);
                                Logger.Error(error);
                            }
                            
                            var monoDebugCommand = debugOptions.DebugScript;

                            errorHelpText.AppendLine($"SSH: Start mono debugger");
                            errorHelpText.AppendLine(monoDebugCommand);

                            Logger.Info($"Run DebugScript: {monoDebugCommand}");

                            // TODO if DebugScript fails no error is shown - very bad!
                            await writeOutput(errorHelpText.ToString());
                            var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                            await RunCommandAndRedirectOutputAsync(cmd, writeOutput, redirectOutputOption);

                            if (cmd.ExitStatus != 0 || !string.IsNullOrWhiteSpace(cmd.Error))
                            {
                                var error = $"SSH script error in DebugScript:\n{cmd.CommandText}\n{cmd.Error}";
                                //errorHelpText.AppendLine(error);
                                Logger.Error(error);

                                throw new Exception(error);
                            }

                            //var monoDebugCommandResult = await Task.Factory.FromAsync(cmd.BeginExecute(), result => cmd.Result);
                            //msgOutput(monoDebugCommandResult);
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

        private async Task RunCommandAndRedirectOutputAsync(Renci.SshNet.SshCommand cmd, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption)
        {
            await Task.Run(() =>
            {
                var asynch = cmd.BeginExecute();

                var taskList = new List<Task>();

                if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectStandardOutput))
                {
                    var stream = cmd.OutputStream;
                    taskList.Add(RedirectStreamAsync(writeOutput, asynch, stream));
                }

                if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectErrorOutput))
                {
                    var stream = cmd.ExtendedOutputStream;
                    taskList.Add(RedirectStreamAsync(writeOutput, asynch, stream));
                }

                Task.WaitAny(taskList.ToArray());

                cmd.EndExecute(asynch);
            });
        }

        private Task RedirectStreamAsync(Func<string, Task> writeOutput, IAsyncResult asynch, Stream stream)
        {
            return Task.Run(async () =>
            {
                var reader = new StreamReader(stream);
                while (!asynch.IsCompleted)
                {
                    var result = await reader.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(result))
                    {
                        await writeOutput(result);
                    }
                }
            });
        }
    }
}
