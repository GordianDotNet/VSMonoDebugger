using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.SSH
{
    public class LocalWindowsDebugger : IDebugger
    {
        public Task<bool> DeployRunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod();
            writeOutput("Start DeployRunAndDebug locally ...");
            return StartDebuggerAsync(debugOptions, true, true, writeOutput, redirectOutputOption);
        }

        public Task<bool> DeployAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod();
            writeOutput("Start Deploy locally ...");
            return StartDebuggerAsync(debugOptions, true, false, writeOutput, redirectOutputOption);
        }

        public Task<bool> RunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None)
        {
            NLogService.TraceEnteringMethod();
            writeOutput("Start RunAndDebug locally ...");
            return StartDebuggerAsync(debugOptions, false, true, writeOutput, redirectOutputOption);
        }

        private Task<bool> StartDebuggerAsync(DebugOptions debugOptions, bool deploy, bool debug, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption)
        {
            NLogService.TraceEnteringMethod();

            return Task.Run<bool>(async () =>
            {
                var errorHelpText = new StringBuilder();
                Action<string> writeLineOutput = s => writeOutput(s + Environment.NewLine).Wait();

                try
                {
                    var destinationDirectory = debugOptions.UserSettings.WindowsDeployPath;
                    if (string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        destinationDirectory = debugOptions.OutputDirectory;
                    }
                    else if (!Path.IsPathRooted(destinationDirectory))
                    {
                        destinationDirectory = Path.Combine(debugOptions.OutputDirectory, destinationDirectory);
                    }

                    errorHelpText.AppendLine($"Directory: {destinationDirectory}");

                    if (deploy)
                    {
                        NLogService.Logger.Info($"StartDebuggerAsync - deploy");

                        errorHelpText.AppendLine($"Local: Start deployment from '{debugOptions.OutputDirectory}' to '{destinationDirectory}'.");
                        Directory.CreateDirectory(destinationDirectory);
                        DirectoryCopy(debugOptions.OutputDirectory, destinationDirectory, copySubDirs: true, overwrite: true, writeOutput: writeOutput);
                        errorHelpText.AppendLine($"Local Deployment was successful.");                        
                    }

                    if (debug)
                    {
                        NLogService.Logger.Info($"StartDebuggerAsync - debug");

                        var killCommandText = debugOptions.PreDebugScript;

                        errorHelpText.AppendLine($"Local: Stop previous mono processes with the PreDebugScript");
                        errorHelpText.AppendLine(killCommandText);
                        NLogService.Logger.Info($"Run PreDebugScript: {killCommandText}");

                        PowershellExecuter.RunScript(killCommandText, destinationDirectory, writeLineOutput, redirectOutputOption);

                        // TODO
                        //if (killCommand.ExitStatus != 0 || !string.IsNullOrWhiteSpace(killCommand.Error))
                        //{
                        //    var error = $"SSH script error in PreDebugScript:\n{killCommand.CommandText}\n{killCommand.Error}";
                        //    //errorHelpText.AppendLine(error);
                        //    NLogService.Logger.Error(error);
                        //}

                        var monoDebugCommand = debugOptions.DebugScript;

                        errorHelpText.AppendLine($"Local: Start mono debugger");
                        errorHelpText.AppendLine(monoDebugCommand);
                        NLogService.Logger.Info($"Run DebugScript: {monoDebugCommand}");

                        // TODO if DebugScript fails no error is shown - very bad!
                        await writeOutput(errorHelpText.ToString());
                        PowershellExecuter.RunScript(monoDebugCommand, destinationDirectory, writeLineOutput, redirectOutputOption);

                        // TODO
                        //await RunCommandAndRedirectOutputAsync(cmd, writeOutput, redirectOutputOption);

                        //if (cmd.ExitStatus != 0 || !string.IsNullOrWhiteSpace(cmd.Error))
                        //{
                        //    var error = $"SSH script error in DebugScript:\n{cmd.CommandText}\n{cmd.Error}";
                        //    //errorHelpText.AppendLine(error);
                        //    NLogService.Logger.Error(error);

                        //    throw new Exception(error);
                        //}
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

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite, Func<string, Task> writeOutput)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                try
                {
                    file.CopyTo(temppath, overwrite);
                }
                catch (Exception ex)
                {
                    writeOutput($"Couldn't copy/overwrite file '{temppath}' (source: {file.FullName}) - Ex: {ex.Message}");
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs, overwrite, writeOutput);
                }
            }
        }
    }
}
