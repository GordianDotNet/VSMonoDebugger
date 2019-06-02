using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SshFileSync;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.SSH
{
    public class SSHDebugger
    {
        public static Task<Task> DeployAndDebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            writeOutput("Start DeployAndDebug over SSH ...");
            return StartDebuggerAsync(options, debugOptions, true, true, writeOutput, redirectOutputOption);
        }

        public static Task<Task> DeployAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            writeOutput("Start Deploy over SSH ...");
            return StartDebuggerAsync(options, debugOptions, true, false, writeOutput, redirectOutputOption);
        }

        public static Task<Task> DebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            writeOutput("Start DeployAndDebug over SSH ...");
            return StartDebuggerAsync(options, debugOptions, false, true, writeOutput, redirectOutputOption);
        }

        private static Task<Task> StartDebuggerAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, bool deploy, bool debug, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption)
        {
            NLogService.TraceEnteringMethod();

            return Task.Run<Task>(async () =>
            {
                var errorHelpText = new StringBuilder();
                Action<string> writeLineOutput = s => writeOutput(s + Environment.NewLine);

                try
                {
                    errorHelpText.AppendLine($"SSH Login: {options.Username}@{options.Host}:{options.Port} Directory: {options.DestinationDirectory}");

                    using (SshDeltaCopy sshDeltaCopy = new SshDeltaCopy(options))
                    {
                        sshDeltaCopy.LogOutput = writeLineOutput;

                        if (deploy)
                        {
                            NLogService.Logger.Info($"StartDebuggerAsync - deploy");

                            errorHelpText.AppendLine($"SSH: Start deployment from '{options.SourceDirectory}' to '{options.DestinationDirectory}'.");
                            sshDeltaCopy.DeployDirectory(options.SourceDirectory, options.DestinationDirectory);
                            errorHelpText.AppendLine($"SSH Deployment was successful.");
                            // We are creating mdb files on local machine with pdb2mdb
                            //var createMdbCommand = sshDeltaCopy.RunSSHCommand($@"find . -regex '.*\(exe\|dll\)' -exec {debugOptions.UserSettings.SSHPdb2mdbCommand} {{}} \;", false);
                            //msgOutput(createMdbCommand.Result);
                        }

                        if (debug)
                        {
                            NLogService.Logger.Info($"StartDebuggerAsync - debug");

                            errorHelpText.AppendLine($"SSH: Stop previous mono processes.");

                            var killCommandText = debugOptions.PreDebugScript;
                            var killCommand = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            writeLineOutput(killCommand.Result);

                            NLogService.Logger.Info($"Run PreDebugScript: {killCommandText}");

                            if (killCommand.ExitStatus != 0 || !string.IsNullOrWhiteSpace(killCommand.Error))
                            {
                                var error = $"SSH script error in PreDebugScript:\n{killCommand.CommandText}\n{killCommand.Error}";
                                //errorHelpText.AppendLine(error);
                                NLogService.Logger.Error(error);
                            }
                            
                            var monoDebugCommand = debugOptions.DebugScript;

                            errorHelpText.AppendLine($"SSH: Start mono debugger");
                            errorHelpText.AppendLine(monoDebugCommand);

                            NLogService.Logger.Info($"Run DebugScript: {monoDebugCommand}");

                            // TODO if DebugScript fails no error is shown - very bad!
                            writeOutput(errorHelpText.ToString());
                            var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                            await RunCommandAndRedirectOutputAsync(cmd, writeOutput, redirectOutputOption);

                            if (cmd.ExitStatus != 0 || !string.IsNullOrWhiteSpace(cmd.Error))
                            {
                                var error = $"SSH script error in DebugScript:\n{cmd.CommandText}\n{cmd.Error}";
                                //errorHelpText.AppendLine(error);
                                NLogService.Logger.Error(error);

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
                    writeOutput(additionalErrorMessage);
                    throw new Exception(additionalErrorMessage, ex);
                }
            });
        }

        private static async Task RunCommandAndRedirectOutputAsync(Renci.SshNet.SshCommand cmd, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption)
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

        private static Task RedirectStreamAsync(Action<string> writeOutput, IAsyncResult asynch, Stream stream)
        {
            return Task.Run(() =>
            {
                var reader = new StreamReader(stream);
                while (!asynch.IsCompleted)
                {
                    var result = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(result))
                    {
                        writeOutput(result);
                    }
                }
            });
        }
    }
}
