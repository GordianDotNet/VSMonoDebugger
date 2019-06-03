using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger
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
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(VSMonoDebuggerPackage.PackageGuidString)]
    //[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class VSMonoDebuggerPackage : AsyncPackage
    {
        /// <summary>
        /// VSMonoDebuggerPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "c7b4e82a-beac-493e-90c3-578d0a0e11b1";

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

                await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                UserSettingsManager.Initialize(this);
                VSMonoDebuggerCommands.Initialize(this);
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

                NLogService.Logger.Error(uex);
            }
            catch (Exception ex)
            {
                NLogService.Logger.Error(ex);
            }

            await base.InitializeAsync(cancellationToken, progress);
        }

        #endregion
    }    
}
