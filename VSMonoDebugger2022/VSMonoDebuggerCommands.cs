using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using VSMonoDebugger2022;

namespace VSMonoDebugger
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed partial class VSMonoDebuggerCommands
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
            var commandID = new CommandID(PackageGuids.guidVSMonoDebuggerPackageCmdSet, cmdCode);
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
