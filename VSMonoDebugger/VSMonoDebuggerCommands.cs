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
        private readonly AsyncPackage _asyncServiceProvider;

        /// <summary>
        /// MonoVisualStudioExtension contains all implementation work
        /// </summary>
        private MonoVisualStudioExtension _monoExtension;

        /// <summary>
        /// Initializes a new instance of the <see cref="VSMonoDebuggerCommands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="asyncServiceProvider">Owner package, not null.</param>
        public VSMonoDebuggerCommands(AsyncPackage asyncServiceProvider, OleMenuCommandService menuCommandService, MonoVisualStudioExtension monoVisualStudioExtension)
        {
            _asyncServiceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
            _monoExtension = monoVisualStudioExtension ?? throw new ArgumentNullException(nameof(monoVisualStudioExtension));

            InstallMenu(menuCommandService);
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
    }
}
