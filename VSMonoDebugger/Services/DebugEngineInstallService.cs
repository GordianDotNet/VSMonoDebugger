using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Mono.Debugging.VisualStudio;

namespace VSMonoDebugger.Services
{
    public static class DebugEngineInstallService
    {
        private const string ENGINE_PATH = @"AD7Metrics\Engine\";
        private const string PORTSUPPLIER_PATH = @"AD7Metrics\PortSupplier\";
        private const string CLSID_PATH = @"CLSID\";

        public static void TryRegisterAssembly()
        {
            // TODO move to AdapterRegistration.pkgdef
            // see https://github.com/microsoft/VSDebugAdapterHost/blob/master/src/sample/SampleDebugAdapter.VSIX/AdapterRegistration.pkgdef
            // see https://github.com/Microsoft/VSDebugAdapterHost/wiki
            // see https://github.com/microsoft/VSDebugAdapterHost/wiki/Packaging-a-VS-Code-Debug-Adapter-For-Use-in-VS
            RegistryKey regKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{{{DebugEngineGuids.EngineGuid.ToString()}}}");

            if (regKey != null)
                return; // Already registered

            string location = typeof(XamarinEngine).Assembly.Location;

            string regasm = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe";
            if (!Environment.Is64BitOperatingSystem)
            {
                regasm = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe";
            }

            var regasmProcessStartInfo = new ProcessStartInfo(regasm, location);
            regasmProcessStartInfo.Verb = "runas";
            regasmProcessStartInfo.RedirectStandardOutput = true;
            regasmProcessStartInfo.UseShellExecute = false;
            regasmProcessStartInfo.CreateNoWindow = true;

            System.Diagnostics.Process process = System.Diagnostics.Process.Start(regasmProcessStartInfo);
            while (!process.HasExited)
            {
                string txt = process.StandardOutput.ReadToEnd();
            }

            using (RegistryKey config = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_Configuration))
            {
                RegisterDebugEngine(location, config);
            }            
        }

        public static void RegisterDebugEngine(string engineDllLocation, RegistryKey rootKey)
        {
            using (RegistryKey engine = rootKey.OpenSubKey(ENGINE_PATH, true))
            {
                string engineGuid = DebugEngineGuids.EngineGuid.ToString("B").ToUpper();
                using (RegistryKey engineKey = engine.CreateSubKey(engineGuid))
                {
                    engineKey.SetValue("CLSID", DebugEngineGuids.EngineGuid.ToString("B").ToUpper());
                    engineKey.SetValue("ProgramProvider", DebugEngineGuids.ProgramProviderGuid.ToString("B").ToUpper());
                    engineKey.SetValue("Attach", 1, RegistryValueKind.DWord); // Check 0?
                    engineKey.SetValue("AddressBP", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("AutoSelectPriority", 4, RegistryValueKind.DWord);
                    engineKey.SetValue("CallstackBP", 1, RegistryValueKind.DWord);
                    engineKey.SetValue("Name", DebugEngineGuids.EngineName);
                    engineKey.SetValue("PortSupplier", DebugEngineGuids.ProgramProviderGuid.ToString("B").ToUpper());
                    engineKey.SetValue("AlwaysLoadLocal", 1, RegistryValueKind.DWord);
                    engineKey.SetValue("Disassembly", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("RemotingDebugging", 0, RegistryValueKind.DWord);
                    engineKey.SetValue("Exceptions", 1, RegistryValueKind.DWord); // Check 0?
                }
            }
            using (RegistryKey engine = rootKey.OpenSubKey(PORTSUPPLIER_PATH, true))
            {
                string portSupplierGuid = DebugEngineGuids.ProgramProviderGuid.ToString("B").ToUpper();
                using (RegistryKey portSupplierKey = engine.CreateSubKey(portSupplierGuid))
                {
                    portSupplierKey.SetValue("CLSID", DebugEngineGuids.ProgramProviderGuid.ToString("B").ToUpper());
                    portSupplierKey.SetValue("Name", DebugEngineGuids.EngineName);
                }
            }

            using (RegistryKey clsid = rootKey.OpenSubKey(CLSID_PATH, true))
            {
                using (RegistryKey clsidKey = clsid.CreateSubKey(DebugEngineGuids.EngineGuid.ToString("B").ToUpper()))
                {
                    clsidKey.SetValue("Assembly", Assembly.GetExecutingAssembly().GetName().Name);
                    switch (DebugEngineGuids.UseAD7Engine)
                    {
                        case EngineType.XamarinEngine:
                            clsidKey.SetValue("Class", typeof(XamarinEngine).FullName);
                            break;
                    }
                    clsidKey.SetValue("InprocServer32", @"c:\windows\system32\mscoree.dll");
                    clsidKey.SetValue("CodeBase", engineDllLocation);
                }

                using (RegistryKey programProviderKey = clsid.CreateSubKey(DebugEngineGuids.ProgramProviderGuid.ToString("B").ToUpper()))
                {
                    programProviderKey.SetValue("Assembly", Assembly.GetExecutingAssembly().GetName().Name);
                    switch (DebugEngineGuids.UseAD7Engine)
                    {
                        case EngineType.XamarinEngine:
                            programProviderKey.SetValue("Class", typeof(XamarinPortSupplier).FullName);
                            break;
                    }
                    programProviderKey.SetValue("InprocServer32", @"c:\windows\system32\mscoree.dll");
                    programProviderKey.SetValue("CodeBase", engineDllLocation);
                }
            }
        }
    }
}