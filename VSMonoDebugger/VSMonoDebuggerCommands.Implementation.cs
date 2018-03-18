using Microsoft.VisualStudio.Shell;
using SshFileSync;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;
using VSMonoDebugger.SSH;
using VSMonoDebugger.Views;

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

                var allDeviceSettings = UserSettingsManager.Instance.Load();
                var settings = allDeviceSettings.CurrentUserSettings;

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
                        monoRemoteSshDebugTask = await SSHDebugger.DeployAndDebugAsync(options, debugOptions, HostOutputWindowEx.WriteLaunchError, settings.RedirectOutputOption);
                    }
                    else
                    {
                        monoRemoteSshDebugTask = await SSHDebugger.DebugAsync(options, debugOptions, HostOutputWindowEx.WriteLaunchError, settings.RedirectOutputOption);
                    }

                    _monoExtension.AttachDebuggerToRunningProcess(debugOptions);
                }
                else
                {
                    monoRemoteSshDebugTask = await SSHDebugger.DeployAsync(options, debugOptions, HostOutputWindowEx.WriteLaunchError, settings.RedirectOutputOption);
                }

                await monoRemoteSshDebugTask;

                return true;
            }
            catch (Exception ex)
            {
                HostOutputWindowEx.WriteLineLaunchError(ex.Message);
                NLogService.Logger.Error(ex);
                // TODO MessageBox
                MessageBox.Show(ex.Message, "MonoRemoteDebugger", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }
    }
}