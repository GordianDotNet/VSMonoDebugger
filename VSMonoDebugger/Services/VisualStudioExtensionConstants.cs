using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;
using Mono.Debugging.VisualStudio;

namespace VSMonoDebugger
{
    class VisualStudioExtensionConstants
    {
        static public int S_OK = 0;
        static public int S_FALSE = 1;
        static public int E_NOTIMPL = unchecked((int)0x80004001);
        static public int E_FAIL = unchecked((int)0x80004005);
    }
}