using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace VSMonoDebugger.Services
{
    public static class HostOutputWindowEx
    {
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
        public static void WriteLaunchError(string outputMessage)
        {
            try
            {
                VsImpl.SetText(outputMessage);
            }
            catch (Exception)
            {
            }
        }

        public static void WriteLineLaunchError(string outputMessage)
        {
            try
            {
                VsImpl.SetText(outputMessage + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }

    public class VSErrorTextWriter : TextWriter
    {
        public override void WriteLine(string value)
        {
            HostOutputWindowEx.WriteLineLaunchError(value);
        }

        public override void Write(char value)
        {
            HostOutputWindowEx.WriteLaunchError(value.ToString());
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
