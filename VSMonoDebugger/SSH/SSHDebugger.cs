using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SshFileSync;
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
            return Task.Factory.StartNew(async () =>
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
                            errorHelpText.AppendLine($"SSH: Start deployment from '{options.SourceDirectory}' to '{options.DestinationDirectory}'.");
                            sshDeltaCopy.DeployDirectory(options.SourceDirectory, options.DestinationDirectory);
                            errorHelpText.AppendLine($"SSH Deployment was successful.");
                            // We are creating mdb files on local machine with pdb2mdb
                            //var createMdbCommand = sshDeltaCopy.RunSSHCommand($@"find . -regex '.*\(exe\|dll\)' -exec {debugOptions.UserSettings.SSHPdb2mdbCommand} {{}} \;", false);
                            //msgOutput(createMdbCommand.Result);
                        }

                        if (debug)
                        {
                            errorHelpText.AppendLine($"SSH: Stop previous mono processes.");

                            var killCommandTextOld = $"kill $(lsof -i | grep 'mono' | grep '\\*:{debugOptions.UserSettings.SSHMonoDebugPort}' | awk '{{print $2}}')";//$"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                            var killCommandText = debugOptions.PreDebugScript;
                            var killCommand = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            writeLineOutput(killCommand.Result);

                            //errorHelpText.AppendLine($"SSH: Stop previous mono processes. Second try.");

                            //// If lsof is unknown and ps aux has an bug (https://bugs.launchpad.net/linaro-oe/+bug/1192942)
                            //killCommandText = $"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                            //var killCommand2 = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            //writeLineOutput(killCommand2.Result);

                            var monoDebugCommandOld = $"mono --debugger-agent=address=0.0.0.0:{debugOptions.UserSettings.SSHMonoDebugPort},transport=dt_socket,server=y --debug=mdb-optimizations {debugOptions.TargetExeFileName} {debugOptions.StartArguments} &";
                            var monoDebugCommand = debugOptions.DebugScript;

                            errorHelpText.AppendLine($"SSH: Start mono debugger");
                            errorHelpText.AppendLine(monoDebugCommand);

                            var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                            await RunCommandAndRedirectOutput(cmd, writeOutput, redirectOutputOption);

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

        private static async Task RunCommandAndRedirectOutput(Renci.SshNet.SshCommand cmd, Action<string> writeOutput, RedirectOutputOptions redirectOutputOption)
        {
            await Task.Factory.StartNew(() =>
            {
                var asynch = cmd.BeginExecute();

                var taskList = new List<Task>();

                if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectStandardOutput))
                {
                    var stream = cmd.OutputStream;
                    taskList.Add(RedirectStream(writeOutput, asynch, stream));
                }

                if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectErrorOutput))
                {
                    var stream = cmd.ExtendedOutputStream;
                    taskList.Add(RedirectStream(writeOutput, asynch, stream));
                }

                Task.WaitAny(taskList.ToArray());

                cmd.EndExecute(asynch);
            });
        }

        private static Task RedirectStream(Action<string> writeOutput, IAsyncResult asynch, Stream stream)
        {
            return Task.Factory.StartNew(() =>
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
