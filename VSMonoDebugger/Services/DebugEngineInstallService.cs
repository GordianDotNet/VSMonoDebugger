using System.Reflection;
using Microsoft.Win32;

namespace VSMonoDebugger.Services
{
    public static class DebugEngineInstallService
    {
        private const string ENGINE_PATH = @"AD7Metrics\Engine\";
        private const string PORTSUPPLIER_PATH = @"AD7Metrics\PortSupplier\";
        private const string CLSID_PATH = @"CLSID\";

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