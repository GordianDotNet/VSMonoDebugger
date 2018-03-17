using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SshFileSync;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.SSH
{
    public class SSHDebugger
    {
        public static Task<Task> DeployAndDebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> msgOutput)
        {
            msgOutput("Start DeployAndDebug over SSH ...");
            return StartDebuggerAsync(options, debugOptions, true, true, msgOutput);
        }

        public static Task<Task> DeployAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> msgOutput)
        {
            msgOutput("Start Deploy over SSH ...");
            return StartDebuggerAsync(options, debugOptions, true, false, msgOutput);
        }

        public static Task<Task> DebugAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, Action<string> msgOutput)
        {
            msgOutput("Start DeployAndDebug over SSH ...");
            return StartDebuggerAsync(options, debugOptions, false, true, msgOutput);
        }

        private static Task<Task> StartDebuggerAsync(SshDeltaCopy.Options options, DebugOptions debugOptions, bool deploy, bool debug, Action<string> msgOutput)
        {
            return Task.Factory.StartNew(async () =>
            {
                var errorHelpText = new StringBuilder();

                try
                {
                    errorHelpText.AppendLine($"SSH Login: {options.Username}@{options.Host}:{options.Port} Directory: {options.DestinationDirectory}");

                    using (SshDeltaCopy sshDeltaCopy = new SshDeltaCopy(options))
                    {
                        sshDeltaCopy.LogOutput = msgOutput;

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
                            errorHelpText.AppendLine($"SSH: Stop previous mono processes. First try.");

                            var killCommandText = $"kill $(lsof -i | grep 'mono' | grep '\\*:{debugOptions.UserSettings.SSHMonoDebugPort}' | awk '{{print $2}}')";//$"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                            var killCommand = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            msgOutput(killCommand.Result);

                            errorHelpText.AppendLine($"SSH: Stop previous mono processes. Second try.");

                            // If lsof is unknown and ps aux has an bug (https://bugs.launchpad.net/linaro-oe/+bug/1192942)
                            killCommandText = $"kill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}')";
                            var killCommand2 = sshDeltaCopy.RunSSHCommand(killCommandText, false);
                            msgOutput(killCommand2.Result);
                            
                            var monoDebugCommand = $"mono --debugger-agent=address={IPAddress.Any}:{debugOptions.UserSettings.SSHMonoDebugPort},transport=dt_socket,server=y --debug=mdb-optimizations {debugOptions.TargetExeFileName} {debugOptions.StartArguments} &";

                            errorHelpText.AppendLine($"SSH: Start mono debugger");
                            errorHelpText.AppendLine(monoDebugCommand);

                            var cmd = sshDeltaCopy.CreateSSHCommand(monoDebugCommand);
                            var monoDebugCommandResult = await Task.Factory.FromAsync(cmd.BeginExecute(), result => cmd.Result);
                            msgOutput(monoDebugCommandResult);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var additionalErrorMessage = $"SSHDebugger: {ex.Message}\n\nExecuted steps:\n{errorHelpText.ToString()}";
                    msgOutput(additionalErrorMessage);
                    throw new Exception(additionalErrorMessage, ex);
                }
            });
        }
    }
}
