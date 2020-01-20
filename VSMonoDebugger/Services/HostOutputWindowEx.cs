using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NLog;

namespace VSMonoDebugger.Services
{
    public static class HostOutputWindowEx
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static VSErrorTextWriter LogInstance { get; internal set; } = new VSErrorTextWriter();

        // Use an extra class so that we have a seperate class which depends on VS interfaces
        private static class VsImpl
        {
            internal static void SetText(string outputMessage)
            {                
                ThreadHelper.ThrowIfNotOnUIThread();

                var outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
                if (outputWindow == null)
                {
                    return;
                }

                IVsOutputWindowPane pane;
                Guid guidDebugOutputPane = VSConstants.GUID_OutWindowDebugPane;
                var hr = outputWindow.GetPane(ref guidDebugOutputPane, out pane);
                if (hr < 0)
                {
                    return;
                }

                hr = pane.OutputString(outputMessage);
                pane.Activate(); // Brings this pane into view
            }
        }

        /// <summary>
        /// Write text to the Debug VS Output window pane directly. This is used to write information before the session create event.
        /// </summary>
        /// <param name="outputMessage"></param>
        public static async System.Threading.Tasks.Task WriteLaunchErrorAsync(string outputMessage)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                VsImpl.SetText(outputMessage);
            }
            catch (Exception ex)
            {
                NLogService.LogError(Logger, ex);
            }
        }

        public static async System.Threading.Tasks.Task WriteLineLaunchErrorAsync(string outputMessage)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                VsImpl.SetText(outputMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                NLogService.LogError(Logger, ex);
            }
        }

        public static void WriteLineLaunchError(string outputMessage)
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
            WriteLineLaunchErrorAsync(outputMessage);
#pragma warning restore VSTHRD110 // Observe result of async calls
        }
    }

    public class VSErrorTextWriter : TextWriter
    {
        public override void WriteLine(string value)
        {
            HostOutputWindowEx.WriteLineLaunchErrorAsync(value);
        }

        public override void Write(char value)
        {
            HostOutputWindowEx.WriteLaunchErrorAsync(value.ToString());
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
