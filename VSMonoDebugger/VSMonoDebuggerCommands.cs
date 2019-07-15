using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSMonoDebugger
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed partial class VSMonoDebuggerCommands
    {
        #region CommandID from VSMonoDebuggerPackage.vsct
        public sealed class CommandIds
        {
            public const int MonoMainMenu = 0x1000;

            public const int MonoMainMenuGroupLevel1 = 0x1100;
            public const int MonoMainMenuGroupLevel2 = 0x1200;

            public const int cmdDeployAndDebugOverSSH = 0x1001;
            public const int cmdDeployOverSSH = 0x1002;
            public const int cmdDebugOverSSH = 0x1003;
            public const int cmdOpenLogFile = 0x1004;
            public const int cmdOpenDebugSettings = 0x1005;
            public const int cmdAttachToMonoDebuggerWithoutSSH = 0x1006;
            public const int cmdBuildProjectWithMDBFiles = 0x1007;
        }
        #endregion

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("becf5dd2-041f-4b6e-9c6f-bb38538fc1d7");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="VSMonoDebuggerCommands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private VSMonoDebuggerCommands(Package package)
        {
            this.package = package ?? throw new ArgumentNullException("package");

            if (ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                InstallMenu(commandService);
            }
        }
        
        private OleMenuCommand AddMenuItem(OleMenuCommandService mcs, int cmdCode, EventHandler check, EventHandler action)
        {
            var commandID = new CommandID(CommandSet, cmdCode);
            var menuCommand = new OleMenuCommand(action, commandID);
            if (check != null)
            {
                menuCommand.BeforeQueryStatus += check;
            }
            mcs.AddCommand(menuCommand);
            return menuCommand;
        }
        
        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static VSMonoDebuggerCommands Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new VSMonoDebuggerCommands(package);
            _monoExtension = new MonoVisualStudioExtension(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            string title = "VSMonoDebuggerCommands";

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
