using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SshFileSync;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Debuggers
{
    public class SSHDebuggerMono : SSHDebuggerBase
    {
        public SSHDebuggerMono(SshDeltaCopy.Options options) : base(options)
        { }

        protected override async Task DebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption, StringBuilder errorHelpText, Action<string> writeLineOutput, SshDeltaCopy sshDeltaCopy)
        {
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
                    if (string.IsNullOrEmpty(result))
                    {
                        System.Threading.Thread.Sleep(1);
                    }
                    else
                    {
                        await writeOutput(result);
                    }
                }
            });
        }
    }
}
