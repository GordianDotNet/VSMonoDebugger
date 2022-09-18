using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using System;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;
using VSMonoDebugger;

namespace VSMonoDebugger2022
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.guidVSMonoDebugger2022PackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class VSMonoDebuggerPackage : AsyncPackage
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="VSMonoDebuggerPackage"/> class.
        /// </summary>
        public VSMonoDebuggerPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.           
        }

        #region Package Members

        private MonoVisualStudioExtension _monoVisualStudioExtension;
        private VSMonoDebuggerCommands _monoDebuggerCommands;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            try
            {
                NLogService.Setup($"{nameof(VSMonoDebuggerPackage)}.log");
                DebugEngineInstallService.TryRegisterAssembly();

                // see https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/AsyncPackageMigration
                await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                var menuCommandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

                // TODO replace by services
                UserSettingsManager.Initialize(this);
                _monoVisualStudioExtension = new MonoVisualStudioExtension(this, dte);
                _monoDebuggerCommands = new VSMonoDebuggerCommands(this, menuCommandService, _monoVisualStudioExtension);
            }
            catch (UnauthorizedAccessException uex)
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var package = this as Package;
                VsShellUtilities.ShowMessageBox(
                package,
                "Failed finish installation of VSMonoDebugger - Please run Visual Studio once as Administrator...",
                $"{nameof(VSMonoDebuggerPackage)} - Register mono debug engine",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                Logger.Error(uex);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            await base.InitializeAsync(cancellationToken, progress);

            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }

        #endregion
    }
}
