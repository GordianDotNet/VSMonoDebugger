using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using VSMonoDebugger.Services;
using VSMonoDebugger.SSH;
using System.Diagnostics;
using EnvDTE;
using VSMonoDebugger.Views;
using VSMonoDebugger.Settings;
using SshFileSync;
using System.Windows;

namespace VSMonoDebugger
{
    internal sealed partial class VSMonoDebuggerCommands
    {
        private static MonoVisualStudioExtension _monoExtension;

        private void InstallMenu(OleMenuCommandService commandService)
        {
            AddMenuItem(commandService, CommandIds.cmdDeployAndDebugOverSSH, CheckStartupProjects, DeployAndDebugOverSSHClicked);
            AddMenuItem(commandService, CommandIds.cmdDeployOverSSH, CheckStartupProjects, DeployOverSSHClicked);
            AddMenuItem(commandService, CommandIds.cmdDebugOverSSH, CheckStartupProjects, DebugOverSSHClicked);

            AddMenuItem(commandService, CommandIds.cmdOpenLogFile, CheckOpenLogFile, OpenLogFile);
            AddMenuItem(commandService, CommandIds.cmdOpenDebugSettings, null, OpenSSHDebugConfigDlg);
        }

        private void CheckOpenLogFile(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Enabled = File.Exists(NLogService.LoggerPath);
            }
        }

        private void OpenLogFile(object sender, EventArgs e)
        {
            if (File.Exists(NLogService.LoggerPath))
            {
                System.Diagnostics.Process.Start(NLogService.LoggerPath);
            }
            else
            {
                // TODO MessageBox
                MessageBox.Show(
                    $"Logfile {NLogService.LoggerPath} not found!",
                    "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckStartupProjects(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                menuCommand.Enabled = _monoExtension.IsStartupProjectAvailable();
            }
        }
        
        private async void DeployAndDebugOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(true, true);
        }

        private async void DeployOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(true, false);
        }

        private async void DebugOverSSHClicked(object sender, EventArgs e)
        {
            await DeployAndRunCommandOverSSH(false, true);
        }

        private void OpenSSHDebugConfigDlg(object sender, EventArgs e)
        {
            var dlg = new DebugSettings();

            if (dlg.ShowDialog().GetValueOrDefault())
            {
                // Saved
            }
        }

        private async Task<bool> DeployAndRunCommandOverSSH(bool deploy, bool startDebugger)
        {
            // TODO error handling
            // TODO show ssh output stream
            // TODO stop monoRemoteSshDebugTask properly
            try
            {
                if (!deploy && !startDebugger)
                {
                    return true;
                }

                var settings = UserSettingsManager.Instance.Load();

                if (deploy)
                {
                    await _monoExtension.BuildSolutionAsync();
                }

                var debugOptions = _monoExtension.CreateDebugOptions(settings, true);

                var options = new SshDeltaCopy.Options()
                {
                    Host = settings.SSHHostIP,
                    Port = settings.SSHPort,
                    Username = settings.SSHUsername,
                    Password = settings.SSHPassword,
                    SourceDirectory = debugOptions.OutputDirectory,
                    DestinationDirectory = settings.SSHDeployPath,
                    RemoveOldFiles = true,
                    PrintTimings = true,
                    RemoveTempDeleteListFile = true,
                };

                if (deploy)
                {
                    await _monoExtension.ConvertPdb2Mdb(options.SourceDirectory, HostOutputWindowEx.WriteLineLaunchError);
                }

                System.Threading.Tasks.Task monoRemoteSshDebugTask;
                if (startDebugger)
                {
                    if (deploy)
                    {
                        monoRemoteSshDebugTask = await SSHDebugger.DeployAndDebugAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                    }
                    else
                    {
                        monoRemoteSshDebugTask = await SSHDebugger.DebugAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                    }

                    _monoExtension.AttachDebuggerToRunningProcess(debugOptions);
                }
                else
                {
                    monoRemoteSshDebugTask = await SSHDebugger.DeployAsync(options, debugOptions, HostOutputWindowEx.WriteLineLaunchError);
                }

                await monoRemoteSshDebugTask;

                return true;
            }
            catch (Exception ex)
            {
                NLogService.Logger.Error(ex);
                // TODO MessageBox
                MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }
    }
}
